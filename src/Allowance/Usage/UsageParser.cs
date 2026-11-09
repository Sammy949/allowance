using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Allowance.Usage;

/// <summary>Turns the output of <c>claude -p /usage --output-format json</c> into numbers. No I/O.</summary>
public static partial class UsageParser
{
    private const int CommandNotFound = 127;

    [GeneratedRegex(@"^\s*(?<label>[^:]+?):\s*<?\s*(?<pct>\d{1,3})%\s+used(?:\W+resets\s+(?<when>.+?)(?:\s*\((?<tz>[^)]+)\))?)?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex UsageLine();

    [GeneratedRegex(@"^(?:(?<mon>[A-Za-z]{3,9})\s+(?<day>\d{1,2})(?:,\s*(?<yr>\d{4}))?,?\s*(?:at\s+)?)?(?<h>\d{1,2})(?::(?<m>\d{2}))?\s*(?<ap>am|pm)$", RegexOptions.IgnoreCase)]
    private static partial Regex ResetTime();

    [GeneratedRegex(@"^Current week \((?<scope>.+)\)$", RegexOptions.IgnoreCase)]
    private static partial Regex WeeklyLabel();

    [GeneratedRegex(@"^\s*(?<title>Last \w+)\W+(?<meta>\d.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex PeriodLine();

    [GeneratedRegex(@"^\s*(?<pct>\d{1,3})%\s+(?<text>\S.*)$")]
    private static partial Regex BreakdownLine();

    /// <summary>Classifies a finished CLI run. A logged-out run also exits 0, so success requires actual usage lines.</summary>
    public static UsageResult Classify(int exitCode, string stdout, string stderr, DateTimeOffset now)
    {
        if (exitCode == CommandNotFound)
        {
            return new UsageResult(null, UsageFailure.CliMissing, FirstLine(stderr));
        }

        var text = ExtractResult(stdout);
        if (text is null)
        {
            var failure = stderr.Contains("wsl", StringComparison.OrdinalIgnoreCase) ? UsageFailure.WslUnavailable : UsageFailure.Unrecognized;
            return new UsageResult(null, failure, FirstLine(stderr.Length > 0 ? stderr : stdout));
        }

        var rows = ParseText(text, now);
        if (rows.Count > 0)
        {
            return new UsageResult(new UsageSnapshot(rows, now, ParseBreakdown(text)), UsageFailure.None);
        }

        var signedOut = text.Contains("Total cost:", StringComparison.OrdinalIgnoreCase)
            || text.Contains("log in", StringComparison.OrdinalIgnoreCase)
            || text.Contains("not logged", StringComparison.OrdinalIgnoreCase);
        return new UsageResult(null, signedOut ? UsageFailure.NotSignedIn : UsageFailure.Unrecognized, FirstLine(text));
    }

    /// <summary>The <c>result</c> text from the CLI's JSON, tolerating anything printed before the JSON.</summary>
    public static string? ExtractResult(string stdout)
    {
        var start = stdout.IndexOf('{');
        if (start < 0)
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(stdout[start..]);
            return doc.RootElement.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.String ? result.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static IReadOnlyList<UsageRow> ParseText(string text, DateTimeOffset now)
    {
        var rows = new List<UsageRow>();
        foreach (var line in text.Split('\n'))
        {
            var match = UsageLine().Match(line.TrimEnd('\r'));
            if (!match.Success)
            {
                continue;
            }

            var label = match.Groups["label"].Value;
            var percent = Math.Clamp(int.Parse(match.Groups["pct"].Value, CultureInfo.InvariantCulture), 0, 100);
            var resets = match.Groups["when"].Success ? ParseReset(match.Groups["when"].Value, match.Groups["tz"].Value, now) : null;

            var week = WeeklyLabel().Match(label);
            if (week.Success)
            {
                rows.Add(new UsageRow(UsageSection.Weekly, Capitalize(week.Groups["scope"].Value), percent, resets));
            }
            else if (label.Contains("session", StringComparison.OrdinalIgnoreCase))
            {
                rows.Add(new UsageRow(UsageSection.Session, label, percent, resets));
            }
            else
            {
                rows.Add(new UsageRow(UsageSection.Weekly, label, percent, resets));
            }
        }

        return rows;
    }

    /// <summary>The CLI's "what's contributing" section, or null if it isn't there. Never fails the fetch.</summary>
    public static UsageBreakdown? ParseBreakdown(string text)
    {
        string? note = null;
        var periods = new List<BreakdownPeriod>();
        List<BreakdownItem>? items = null;
        List<string>? tops = null;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            var period = PeriodLine().Match(line);
            if (period.Success)
            {
                items = [];
                tops = [];
                periods.Add(new BreakdownPeriod(period.Groups["title"].Value, period.Groups["meta"].Value, items, tops));
            }
            else if (line.StartsWith("Approximate", StringComparison.OrdinalIgnoreCase))
            {
                note = line;
            }
            else if (items is not null && BreakdownLine().Match(line) is { Success: true } item)
            {
                items.Add(new BreakdownItem(int.Parse(item.Groups["pct"].Value, CultureInfo.InvariantCulture), item.Groups["text"].Value));
            }
            else if (tops is not null && line.StartsWith("Top ", StringComparison.OrdinalIgnoreCase))
            {
                tops.Add(line);
            }
        }

        return periods.Count > 0 ? new UsageBreakdown(note, periods) : null;
    }

    /// <summary>Parses "Nov 9, 4:50am" or "7pm" in the CLI's zone. The CLI omits the year, so pick the nearest sensible one.</summary>
    public static DateTimeOffset? ParseReset(string when, string timeZone, DateTimeOffset now)
    {
        var match = ResetTime().Match(when.Trim());
        if (!match.Success)
        {
            return null;
        }

        var zone = FindZone(timeZone);
        var zonedNow = TimeZoneInfo.ConvertTime(now, zone);
        var hour = int.Parse(match.Groups["h"].Value, CultureInfo.InvariantCulture) % 12;
        if (match.Groups["ap"].Value.Equals("pm", StringComparison.OrdinalIgnoreCase))
        {
            hour += 12;
        }

        var minute = match.Groups["m"].Success ? int.Parse(match.Groups["m"].Value, CultureInfo.InvariantCulture) : 0;
        if (hour > 23 || minute > 59)
        {
            return null;
        }

        DateTime local;
        if (match.Groups["mon"].Success)
        {
            if (!DateTime.TryParseExact(match.Groups["mon"].Value[..3], "MMM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month))
            {
                return null;
            }

            var year = match.Groups["yr"].Success ? int.Parse(match.Groups["yr"].Value, CultureInfo.InvariantCulture) : zonedNow.Year;
            var day = int.Parse(match.Groups["day"].Value, CultureInfo.InvariantCulture);
            if (day < 1 || day > DateTime.DaysInMonth(year, month.Month))
            {
                return null;
            }

            local = new DateTime(year, month.Month, day, hour, minute, 0, DateTimeKind.Unspecified);
            if (!match.Groups["yr"].Success && local < zonedNow.DateTime.AddDays(-180))
            {
                local = local.AddYears(1);
            }
        }
        else
        {
            local = new DateTime(zonedNow.Year, zonedNow.Month, zonedNow.Day, hour, minute, 0, DateTimeKind.Unspecified);
            if (local < zonedNow.DateTime)
            {
                local = local.AddDays(1);
            }
        }

        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }

    private static TimeZoneInfo FindZone(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return TimeZoneInfo.Local;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Local;
        }
    }

    private static string Capitalize(string value) => value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value[1..];

    private static string FirstLine(string value)
    {
        var line = value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? string.Empty;
        return line.Length > 200 ? line[..200] : line;
    }
}

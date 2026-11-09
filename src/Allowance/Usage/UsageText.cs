using System.Globalization;

namespace Allowance.Usage;

/// <summary>The words the drawer shows, in claude.ai's phrasing.</summary>
public static class UsageText
{
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");

    /// <summary>"Resets at 4:50 AM" today, "Resets Fri 7:00 PM" this week, otherwise "Resets Nov 20".</summary>
    public static string Reset(DateTimeOffset? resetsAt, DateTimeOffset now, TimeZoneInfo zone)
    {
        if (resetsAt is not { } value)
        {
            return string.Empty;
        }

        // The CLI's minute wobbles between runs (6:59pm, then 7pm). claude.ai shows the rounded time.
        var rounded = new DateTimeOffset(Round5(value.UtcDateTime), TimeSpan.Zero);
        var at = TimeZoneInfo.ConvertTime(rounded, zone);
        var today = TimeZoneInfo.ConvertTime(now, zone).Date;
        var days = (at.Date - today).Days;
        return days switch
        {
            <= 0 => "Resets at " + at.ToString("h:mm tt", En),
            < 7 => "Resets " + at.ToString("ddd h:mm tt", En),
            _ => "Resets " + at.ToString("MMM d", En),
        };
    }

    public static string Updated(DateTimeOffset fetchedAt, DateTimeOffset now)
    {
        var age = now - fetchedAt;
        if (age.TotalMinutes < 1)
        {
            return "Updated just now";
        }

        if (age.TotalHours < 1)
        {
            return $"Updated {(int)age.TotalMinutes} min ago";
        }

        if (age.TotalDays < 1)
        {
            return $"Updated {(int)age.TotalHours} hr ago";
        }

        var days = (int)age.TotalDays;
        return days == 1 ? "Updated 1 day ago" : $"Updated {days} days ago";
    }

    public static string Failure(UsageFailure failure, bool hasOldNumbers)
    {
        var reason = failure switch
        {
            UsageFailure.NotSignedIn => "Not signed in to a Claude plan. Run claude auth login in WSL.",
            UsageFailure.CliMissing => "Claude Code wasn't found in WSL.",
            UsageFailure.WslUnavailable => "WSL didn't respond.",
            UsageFailure.TimedOut => "Claude Code took too long to answer.",
            UsageFailure.Unrecognized => "Claude Code's usage output changed, so Allowance can't read it.",
            _ => string.Empty,
        };
        return reason.Length > 0 && hasOldNumbers ? reason + " These are your last numbers." : reason;
    }

    private static DateTime Round5(DateTime utc)
    {
        var ticks = TimeSpan.FromMinutes(5).Ticks;
        return new DateTime((utc.Ticks + (ticks / 2)) / ticks * ticks, DateTimeKind.Utc);
    }
}

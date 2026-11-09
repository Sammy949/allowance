using System.Text.Json;
using Allowance.Usage;
using Xunit;

namespace Allowance.Tests;

public class UsageParserTests
{
    private const string Zone = "America/New_York";

    // Shaped like real CLI output, including the coaching lines that also contain percentages.
    private const string RealText = """
        You are currently using your subscription to power your Claude Code usage

        Current session: 27% used · resets Nov 9, 4:50am (America/New_York)
        Current week (all models): 17% used · resets Nov 13, 7pm (America/New_York)

        What's contributing to your usage?
        Approximate · sessions on this machine only · shares overlap

        Last 24h · 78 requests · 2 sessions
          72%  Start fresh when you switch tasks · run /clear
          23%  Turn off skills you don't use · 16k tokens per request
          Top skills: /some-skill 23%, /other-skill 4%
        """;

    private const string LoggedOutText = """
        Total cost:            $0.0000
        Total duration (API):  0s
        Usage:                 0 input, 0 output, 0 cache read, 0 cache write
        """;

    private static readonly DateTimeOffset Now = new(2026, 11, 9, 2, 0, 0, TimeSpan.FromHours(-5));
    private static readonly TimeZoneInfo Eastern = TimeZoneInfo.FindSystemTimeZoneById(Zone);

    private static string Json(string result) => JsonSerializer.Serialize(new { is_error = false, result });

    [Fact]
    public void ReadsSessionAndWeeklyAndIgnoresCoachingLines()
    {
        var rows = UsageParser.ParseText(RealText, Now);

        Assert.Equal(2, rows.Count);
        Assert.Equal(new UsageRow(UsageSection.Session, "Current session", 27, new DateTimeOffset(2026, 11, 9, 4, 50, 0, TimeSpan.FromHours(-5))), rows[0]);
        Assert.Equal(new UsageRow(UsageSection.Weekly, "All models", 17, new DateTimeOffset(2026, 11, 13, 19, 0, 0, TimeSpan.FromHours(-5))), rows[1]);
    }

    [Fact]
    public void UnknownWeeklyLinesAppearWithoutCodeChanges()
    {
        var rows = UsageParser.ParseText("Current week (Fable): 40% used · resets Nov 13, 7pm (America/New_York)", Now);

        Assert.Equal("Fable", Assert.Single(rows).Label);
        Assert.Equal(UsageSection.Weekly, rows[0].Section);
    }

    [Fact]
    public void AWindowWithNoResetStillShows()
    {
        var row = Assert.Single(UsageParser.ParseText("Current session: 0% used", Now));

        Assert.Equal(0, row.Percent);
        Assert.Null(row.ResetsAt);
    }

    [Theory]
    [InlineData("100% used", 100)]
    [InlineData("<1% used", 1)]
    public void PercentEdges(string used, int expected) =>
        Assert.Equal(expected, Assert.Single(UsageParser.ParseText($"Current session: {used} · resets 7pm (America/New_York)", Now)).Percent);

    [Fact]
    public void ToleratesWindowsLineEndings() =>
        Assert.Equal(2, UsageParser.ParseText(RealText.Replace("\n", "\r\n"), Now).Count);

    [Fact]
    public void ResetInJanuarySeenFromDecemberIsNextYear()
    {
        var december = new DateTimeOffset(2026, 12, 30, 12, 0, 0, TimeSpan.FromHours(-5));
        Assert.Equal(2027, UsageParser.ParseReset("Jan 2, 7pm", Zone, december)!.Value.Year);
    }

    [Fact]
    public void BareTimeAlreadyPassedTodayMeansTomorrow()
    {
        var parsed = UsageParser.ParseReset("1am", Zone, Now)!.Value;
        Assert.Equal(10, parsed.Day);
    }

    [Theory]
    [InlineData("12am", 0)]
    [InlineData("12pm", 12)]
    public void TwelveOClock(string when, int hour) =>
        Assert.Equal(hour, UsageParser.ParseReset($"Nov 20, {when}", Zone, Now)!.Value.Hour);

    [Fact]
    public void UnreadableResetKeepsTheNumber()
    {
        var row = Assert.Single(UsageParser.ParseText("Current session: 5% used · resets soonish (America/New_York)", Now));
        Assert.Equal(5, row.Percent);
        Assert.Null(row.ResetsAt);
    }

    [Fact]
    public void ClassifySuccess()
    {
        var result = UsageParser.Classify(0, Json(RealText), string.Empty, Now);
        Assert.Equal(UsageFailure.None, result.Failure);
        Assert.Equal(2, result.Snapshot!.Rows.Count);
    }

    [Fact]
    public void ClassifyToleratesTextBeforeJson() =>
        Assert.Equal(UsageFailure.None, UsageParser.Classify(0, "Permission allow rule warning\n" + Json(RealText), string.Empty, Now).Failure);

    [Fact]
    public void LoggedOutIsAFailureEvenThoughExitIsZero()
    {
        var result = UsageParser.Classify(0, Json(LoggedOutText), string.Empty, Now);
        Assert.Equal(UsageFailure.NotSignedIn, result.Failure);
        Assert.Null(result.Snapshot);
    }

    [Fact]
    public void MissingCli() =>
        Assert.Equal(UsageFailure.CliMissing, UsageParser.Classify(127, string.Empty, "bash: line 1: claude: command not found", Now).Failure);

    [Fact]
    public void ChangedFormatIsVisibleNotSilent() =>
        Assert.Equal(UsageFailure.Unrecognized, UsageParser.Classify(0, Json("Your limits look fine today"), string.Empty, Now).Failure);

    [Fact]
    public void GarbageOutputIsVisibleNotSilent() =>
        Assert.Equal(UsageFailure.Unrecognized, UsageParser.Classify(0, "{not json", string.Empty, Now).Failure);

    [Theory]
    [InlineData(2026, 11, 9, 4, 49, "Resets at 4:50 AM")]
    [InlineData(2026, 11, 13, 18, 59, "Resets Fri 7:00 PM")]
    [InlineData(2026, 11, 20, 19, 0, "Resets Nov 20")]
    public void ResetWording(int y, int m, int d, int h, int min, string expected)
    {
        var at = new DateTimeOffset(y, m, d, h, min, 0, TimeSpan.FromHours(-5));
        Assert.Equal(expected, UsageText.Reset(at, Now, Eastern));
    }

    [Fact]
    public void NoResetIsEmptyText() => Assert.Equal(string.Empty, UsageText.Reset(null, Now, Eastern));

    [Theory]
    [InlineData(10, "Updated just now")]
    [InlineData(600, "Updated 10 min ago")]
    [InlineData(7200, "Updated 2 hr ago")]
    [InlineData(90000, "Updated 1 day ago")]
    public void UpdatedWording(int secondsAgo, string expected) =>
        Assert.Equal(expected, UsageText.Updated(Now.AddSeconds(-secondsAgo), Now));

    [Fact]
    public void FailureMessageSaysWhenNumbersAreOld()
    {
        Assert.EndsWith("These are your last numbers.", UsageText.Failure(UsageFailure.TimedOut, true));
        Assert.DoesNotContain("last numbers", UsageText.Failure(UsageFailure.TimedOut, false));
        Assert.Equal(string.Empty, UsageText.Failure(UsageFailure.None, true));
    }
}

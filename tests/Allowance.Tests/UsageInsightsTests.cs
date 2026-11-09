using Allowance.Usage;
using Xunit;

namespace Allowance.Tests;

public class UsageInsightsTests
{
    private static readonly DateTimeOffset Reset = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

    private static UsageRow Session(int percent, DateTimeOffset? resets) => new(UsageSection.Session, "Current session", percent, resets);

    [Fact]
    public void SessionPaceUsesFiveHours() => Assert.Equal(0.8, UsagePace.Elapsed(Session(1, Reset), Reset.AddHours(-1))!.Value, 3);

    [Fact]
    public void WeeklyPaceUsesSevenDays() =>
        Assert.Equal(6.0 / 7, UsagePace.Elapsed(new UsageRow(UsageSection.Weekly, "All models", 1, Reset), Reset.AddDays(-1))!.Value, 3);

    [Fact]
    public void PaceIsClampedAndNeedsAReset()
    {
        Assert.Equal(1, UsagePace.Elapsed(Session(1, Reset), Reset.AddHours(1)));
        Assert.Equal(0, UsagePace.Elapsed(Session(1, Reset), Reset.AddHours(-9)));
        Assert.Null(UsagePace.Elapsed(Session(1, null), Reset));
    }

    [Fact]
    public void HighSessionNotifiesOnceAfterReset()
    {
        var tracker = new RefillTracker();
        Assert.Empty(tracker.Observe([Session(90, Reset)], Reset.AddHours(-1)));
        var refill = Assert.Single(tracker.Observe([Session(2, Reset.AddHours(5))], Reset.AddSeconds(30)));
        Assert.Equal("Your session refilled", refill.Title);
        Assert.Equal("You're at 2% used.", refill.Body);
        Assert.Empty(tracker.Observe([Session(2, Reset.AddHours(5))], Reset.AddMinutes(2)));
    }

    [Fact]
    public void LowUsageNeverNotifies()
    {
        var tracker = new RefillTracker();
        tracker.Observe([Session(40, Reset)], Reset.AddHours(-1));
        Assert.Empty(tracker.Observe([Session(2, Reset.AddHours(5))], Reset.AddSeconds(30)));
    }

    [Fact]
    public void StillHighAfterResetIsNotARefillYet()
    {
        var tracker = new RefillTracker();
        tracker.Observe([Session(90, Reset)], Reset.AddHours(-1));
        Assert.Empty(tracker.Observe([Session(90, Reset)], Reset.AddSeconds(30)));
        Assert.Single(tracker.Observe([Session(1, Reset.AddHours(5))], Reset.AddMinutes(2)));
    }

    [Fact]
    public void ClimbingBeforeResetIsNotARefill()
    {
        var tracker = new RefillTracker();
        tracker.Observe([Session(90, Reset)], Reset.AddHours(-2));
        Assert.Empty(tracker.Observe([Session(95, Reset)], Reset.AddHours(-1)));
    }

    [Fact]
    public void AWindowThatDisappearedAfterResetCountsAsRefilled()
    {
        var tracker = new RefillTracker();
        tracker.Observe([Session(90, Reset)], Reset.AddHours(-1));
        Assert.Equal(0, Assert.Single(tracker.Observe([], Reset.AddMinutes(1))).Percent);
    }

    [Fact]
    public void FetchWaitsForResetAndDoesNotHammer()
    {
        var tracker = new RefillTracker();
        tracker.Observe([Session(90, Reset)], Reset.AddHours(-1));
        Assert.False(tracker.ShouldFetch(Reset.AddSeconds(5)));
        Assert.True(tracker.ShouldFetch(Reset.AddSeconds(25)));
        Assert.False(tracker.ShouldFetch(Reset.AddSeconds(40)));
        Assert.True(tracker.ShouldFetch(Reset.AddSeconds(90)));
    }

    [Fact]
    public void StopsWatchingAfterGivingUp()
    {
        var tracker = new RefillTracker();
        tracker.Observe([Session(90, Reset)], Reset.AddHours(-1));
        tracker.Observe([Session(90, Reset)], Reset.AddMinutes(11));
        Assert.False(tracker.ShouldFetch(Reset.AddMinutes(12)));
    }

    [Theory]
    [InlineData("All models", "Your weekly limit refilled")]
    [InlineData("Sonnet only", "Sonnet only: weekly limit refilled")]
    public void WeeklyTitles(string label, string title) => Assert.Equal(title, new Refill(UsageSection.Weekly, label, 0).Title);

    [Fact]
    public void BreakdownReadsPeriodsItemsAndTops()
    {
        const string text = """
            Current session: 27% used · resets 7pm (America/New_York)

            What's contributing to your usage?
            Approximate · sessions on this machine only · shares overlap

            Last 24h · 78 requests · 2 sessions
              72%  Start fresh when you switch tasks · run /clear
              23%  Turn off skills you don't use
              Top skills: /some-skill 23%, /other-skill 4%

            Last 7d · 1596 requests · 13 sessions
              88%  Start fresh when you switch tasks · run /clear
              Top MCP servers: Example 1%
            """;

        var breakdown = UsageParser.ParseBreakdown(text)!;

        Assert.StartsWith("Approximate", breakdown.Note);
        Assert.Equal(2, breakdown.Periods.Count);
        Assert.Equal("Last 24h", breakdown.Periods[0].Title);
        Assert.Equal("78 requests · 2 sessions", breakdown.Periods[0].Meta);
        Assert.Equal(new BreakdownItem(72, "Start fresh when you switch tasks · run /clear"), breakdown.Periods[0].Items[0]);
        Assert.Equal("Top skills: /some-skill 23%, /other-skill 4%", Assert.Single(breakdown.Periods[0].Tops));
        Assert.Equal("Top MCP servers: Example 1%", Assert.Single(breakdown.Periods[1].Tops));
    }

    [Fact]
    public void NoBreakdownSectionIsNull() => Assert.Null(UsageParser.ParseBreakdown("Current session: 27% used"));
}

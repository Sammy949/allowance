namespace Allowance.Usage;

public enum UsageSection
{
    Session,
    Weekly,
}

public sealed record UsageRow(UsageSection Section, string Label, int Percent, DateTimeOffset? ResetsAt);

public sealed record UsageSnapshot(IReadOnlyList<UsageRow> Rows, DateTimeOffset FetchedAt, UsageBreakdown? Breakdown = null);

public sealed record BreakdownItem(int Percent, string Text);

public sealed record BreakdownPeriod(string Title, string Meta, IReadOnlyList<BreakdownItem> Items, IReadOnlyList<string> Tops);

public sealed record UsageBreakdown(string? Note, IReadOnlyList<BreakdownPeriod> Periods);

public enum UsageFailure
{
    None,
    NotSignedIn,
    CliMissing,
    WslUnavailable,
    Unrecognized,
    TimedOut,
}

public sealed record UsageResult(UsageSnapshot? Snapshot, UsageFailure Failure, string? Detail = null);

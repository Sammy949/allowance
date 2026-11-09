namespace Allowance.Usage;

public enum UsageSection
{
    Session,
    Weekly,
}

public sealed record UsageRow(UsageSection Section, string Label, int Percent, DateTimeOffset? ResetsAt);

public sealed record UsageSnapshot(IReadOnlyList<UsageRow> Rows, DateTimeOffset FetchedAt);

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

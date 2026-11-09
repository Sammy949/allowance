namespace Allowance.Usage;

public static class UsagePace
{
    private static readonly TimeSpan SessionWindow = TimeSpan.FromHours(5);
    private static readonly TimeSpan WeeklyWindow = TimeSpan.FromDays(7);

    /// <summary>How much of the row's window has passed, 0 to 1. Null when the reset time is unknown.</summary>
    public static double? Elapsed(UsageRow row, DateTimeOffset now)
    {
        if (row.ResetsAt is not { } resets)
        {
            return null;
        }

        var window = row.Section == UsageSection.Session ? SessionWindow : WeeklyWindow;
        return Math.Clamp(1 - ((resets - now) / window), 0, 1);
    }
}

public sealed record Refill(UsageSection Section, string Label, int Percent)
{
    public string Title => Section == UsageSection.Session
        ? "Your session refilled"
        : Label.Equals("All models", StringComparison.OrdinalIgnoreCase) ? "Your weekly limit refilled" : $"{Label}: weekly limit refilled";

    public string Body => $"You're at {Percent}% used.";
}

/// <summary>
/// Watches windows that were running high and reports when they refill. Pure: the caller supplies readings and time.
/// Works off the wall clock, so a machine that slept through a reset still reports it.
/// </summary>
public sealed class RefillTracker
{
    public const int HighPercent = 80;
    private static readonly TimeSpan FetchDelay = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan RetryEvery = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan GiveUpAfter = TimeSpan.FromMinutes(10);

    // The CLI's reset minute wobbles by one between runs.
    private static readonly TimeSpan ResetWobble = TimeSpan.FromMinutes(5);

    private readonly Dictionary<(UsageSection Section, string Label), Armed> _armed = [];
    private DateTimeOffset _lastFetch = DateTimeOffset.MinValue;

    public IReadOnlyList<Refill> Observe(IReadOnlyList<UsageRow> rows, DateTimeOffset now)
    {
        var refills = new List<Refill>();
        foreach (var (key, armed) in _armed.ToList())
        {
            if (now < armed.ResetsAt - ResetWobble)
            {
                continue;
            }

            var row = rows.FirstOrDefault(r => r.Section == key.Section && r.Label == key.Label);
            if (row is null)
            {
                refills.Add(new Refill(key.Section, key.Label, 0));
                _armed.Remove(key);
            }
            else if (row.Percent < armed.Percent && (row.Percent < HighPercent || row.ResetsAt > armed.ResetsAt + TimeSpan.FromHours(1)))
            {
                refills.Add(new Refill(key.Section, key.Label, row.Percent));
                _armed.Remove(key);
            }
            else if (now > armed.ResetsAt + GiveUpAfter)
            {
                _armed.Remove(key);
            }
        }

        foreach (var row in rows)
        {
            if (row.Percent >= HighPercent && row.ResetsAt is { } resets && resets > now)
            {
                _armed[(row.Section, row.Label)] = new Armed(row.Percent, resets);
            }
        }

        return refills;
    }

    /// <summary>True when a watched window has passed its reset and a fresh reading is due.</summary>
    public bool ShouldFetch(DateTimeOffset now)
    {
        if (now < _lastFetch + RetryEvery || !_armed.Values.Any(a => now >= a.ResetsAt + FetchDelay))
        {
            return false;
        }

        _lastFetch = now;
        return true;
    }

    private sealed record Armed(int Percent, DateTimeOffset ResetsAt);
}

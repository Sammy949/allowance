using System.Text.Json;

namespace Allowance.Usage;

/// <summary>Latest numbers, the last failure, and one fetch at a time. Last good numbers survive restarts.</summary>
internal sealed class UsageStore
{
    private readonly UsageSource _source;
    private readonly string _cachePath = AppPaths.File("last-usage.json");
    private readonly object _gate = new();
    private Task? _running;

    public UsageStore(UsageSource source)
    {
        _source = source;
        Snapshot = LoadCache();
    }

    public event Action? Changed;

    public UsageSnapshot? Snapshot { get; private set; }

    public UsageFailure Failure { get; private set; }

    public bool IsFetching { get; private set; }

    public string? Plan { get; private set; }

    public bool IsOlderThan(TimeSpan age) => Snapshot is null || DateTimeOffset.Now - Snapshot.FetchedAt > age;

    public Task RefreshAsync()
    {
        lock (_gate)
        {
            if (_running is { IsCompleted: false })
            {
                return _running;
            }

            IsFetching = true;
            _running = Task.Run(FetchAsync);
        }

        Changed?.Invoke();
        return _running;
    }

    public async Task LoadPlanAsync()
    {
        Plan = await _source.FetchPlanAsync().ConfigureAwait(false);
        Changed?.Invoke();
    }

    private async Task FetchAsync()
    {
        try
        {
            var result = await _source.FetchAsync().ConfigureAwait(false);
            Failure = result.Failure;
            if (result.Snapshot is { } snapshot)
            {
                Snapshot = snapshot;
                SaveCache(snapshot);
                Log.Write($"usage fetched: {snapshot.Rows.Count} row(s)");
            }
            else
            {
                Log.Write($"usage fetch failed: {result.Failure} ({result.Detail})");
            }
        }
        catch (Exception e)
        {
            Failure = UsageFailure.Unrecognized;
            Log.Write($"usage fetch threw: {e}");
        }
        finally
        {
            IsFetching = false;
            Changed?.Invoke();
        }
    }

    private void SaveCache(UsageSnapshot snapshot)
    {
        try
        {
            using var stream = File.Create(_cachePath);
            using var json = new Utf8JsonWriter(stream);
            json.WriteStartObject();
            json.WriteString("fetchedAt", snapshot.FetchedAt);
            json.WriteStartArray("rows");
            foreach (var row in snapshot.Rows)
            {
                json.WriteStartObject();
                json.WriteNumber("section", (int)row.Section);
                json.WriteString("label", row.Label);
                json.WriteNumber("percent", row.Percent);
                if (row.ResetsAt is { } resets)
                {
                    json.WriteString("resetsAt", resets);
                }

                json.WriteEndObject();
            }

            json.WriteEndArray();
            json.WriteEndObject();
        }
        catch (IOException e)
        {
            Log.Write($"couldn't save usage cache: {e.Message}");
        }
    }

    private UsageSnapshot? LoadCache()
    {
        try
        {
            if (!File.Exists(_cachePath))
            {
                return null;
            }

            using var doc = JsonDocument.Parse(File.ReadAllText(_cachePath));
            var rows = doc.RootElement.GetProperty("rows").EnumerateArray().Select(r => new UsageRow(
                (UsageSection)r.GetProperty("section").GetInt32(),
                r.GetProperty("label").GetString() ?? string.Empty,
                r.GetProperty("percent").GetInt32(),
                r.TryGetProperty("resetsAt", out var resets) ? resets.GetDateTimeOffset() : null)).ToList();
            return new UsageSnapshot(rows, doc.RootElement.GetProperty("fetchedAt").GetDateTimeOffset());
        }
        catch (Exception e) when (e is IOException or JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            Log.Write($"ignoring unreadable usage cache: {e.Message}");
            return null;
        }
    }
}

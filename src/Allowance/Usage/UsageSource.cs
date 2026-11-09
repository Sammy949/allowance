using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Allowance.Usage;

/// <summary>Asks the official Claude Code CLI in WSL for usage. The CLI owns the login, so no credential is ever read here.</summary>
internal sealed class UsageSource
{
    private const string UsageCommand = "claude -p /usage --output-format json --no-session-persistence < /dev/null";
    private const string AuthCommand = "claude auth status --json < /dev/null";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(45);

    /// <summary>Test seam: read this file instead of running the CLI, to exercise each state.</summary>
    public string? FixturePath { get; init; }

    public async Task<UsageResult> FetchAsync()
    {
        if (FixturePath is { } path)
        {
            await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
            return UsageParser.Classify(0, await File.ReadAllTextAsync(path).ConfigureAwait(false), string.Empty, DateTimeOffset.Now);
        }

        var run = await RunAsync(UsageCommand).ConfigureAwait(false);
        return run.Failure is { } failure
            ? new UsageResult(null, failure, run.Stderr)
            : UsageParser.Classify(run.ExitCode, run.Stdout, run.Stderr, DateTimeOffset.Now);
    }

    /// <summary>"Pro", "Max", or null. Reads only the plan field from the CLI's account summary.</summary>
    public async Task<string?> FetchPlanAsync()
    {
        if (FixturePath is not null)
        {
            return "Pro";
        }

        var run = await RunAsync(AuthCommand).ConfigureAwait(false);
        if (run.Failure is not null || run.ExitCode != 0)
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(run.Stdout);
            if (doc.RootElement.TryGetProperty("subscriptionType", out var plan) && plan.GetString() is { Length: > 0 } name)
            {
                return char.ToUpperInvariant(name[0]) + name[1..];
            }
        }
        catch (JsonException)
        {
            // The plan label is decoration: without it the drawer just doesn't show one.
        }

        return null;
    }

    private static async Task<CliRun> RunAsync(string command)
    {
        var info = new ProcessStartInfo("wsl.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        // Run from home so a project's local Claude settings stay out of it.
        foreach (var arg in new[] { "--cd", "~", "-e", "bash", "-lc", command })
        {
            info.ArgumentList.Add(arg);
        }

        Process process;
        try
        {
            process = Process.Start(info) ?? throw new InvalidOperationException("wsl.exe did not start");
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException)
        {
            return new CliRun(-1, string.Empty, e.Message, UsageFailure.WslUnavailable);
        }

        using (process)
        using (var cts = new CancellationTokenSource(Timeout))
        {
            var stdout = process.StandardOutput.ReadToEndAsync(cts.Token);
            var stderr = process.StandardError.ReadToEndAsync(cts.Token);
            try
            {
                await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
                return new CliRun(process.ExitCode, await stdout.ConfigureAwait(false), await stderr.ConfigureAwait(false), null);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (Exception e) when (e is Win32Exception or InvalidOperationException)
                {
                    Log.Write($"couldn't stop timed-out CLI: {e.Message}");
                }

                return new CliRun(-1, string.Empty, "timed out", UsageFailure.TimedOut);
            }
        }
    }

    private sealed record CliRun(int ExitCode, string Stdout, string Stderr, UsageFailure? Failure);
}

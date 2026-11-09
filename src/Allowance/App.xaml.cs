using Allowance.Drawer;
using Allowance.Shell;
using Allowance.Usage;
using Microsoft.UI.Xaml;
using Microsoft.Win32;

namespace Allowance;

public partial class App : Application
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private static readonly string NumbersFlag = AppPaths.File("tray-numbers");
    private static Mutex? _singleInstance;
    private static EventWaitHandle? _openSignal;
    private DrawerWindow? _drawer;
    private NotifyIcon? _icon;
    private UsageStore? _store;
    private Timer? _refillClock;
    private UsageSnapshot? _observed;
    private readonly RefillTracker _refills = new();

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) => Log.Write($"UNHANDLED {e.Exception}");
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _singleInstance = new Mutex(true, @"Local\Allowance.SingleInstance", out var first);
        _openSignal = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\Allowance.Open");
        if (!first)
        {
            // Launching it again opens the drawer: the way in when Windows hides the tray icon.
            _openSignal.Set();
            Exit();
            return;
        }

        var argv = Environment.GetCommandLineArgs();
        Log.Write($"=== Allowance start ({string.Join(' ', argv.Skip(1))}) ===");

        // Test seam: writes the tray icon at each size so legibility can be checked.
        if (ArgAfter(argv, "--render-icons") is { } iconDir)
        {
            foreach (var (size, light) in new[] { (16, false), (20, false), (24, false), (32, false), (20, true) })
            {
                using var sample = TrayNumberIcon.Render(33, 18, size, light);
                sample.Save(Path.Combine(iconDir, $"icon-{size}-{(light ? "light" : "dark")}.png"));
                using var full = TrayNumberIcon.Render(100, 8, size, light);
                full.Save(Path.Combine(iconDir, $"icon100-{size}-{(light ? "light" : "dark")}.png"));
            }

            Exit();
            return;
        }

        var fixture = ArgAfter(argv, "--fixture");
        var store = _store = new UsageStore(new UsageSource { FixturePath = fixture });
        var drawer = _drawer = new DrawerWindow(store) { KeepOpenForTest = argv.Contains("--keep-open") };
        if (ArgAfter(argv, "--theme") is { } theme)
        {
            drawer.ApplyTheme(theme == "light" ? ElementTheme.Light : ElementTheme.Dark);
        }

        var ui = drawer.DispatcherQueue;
        var icon = _icon = new NotifyIcon(StartsWithWindows, () => File.Exists(NumbersFlag));
        icon.OpenRequested += () => ui.TryEnqueue(drawer.Toggle);
        icon.RefreshRequested += () => _ = store.RefreshAsync();
        icon.StartupToggled += ToggleStartup;
        icon.NumbersToggled += ToggleNumbers;
        icon.QuitRequested += () => ui.TryEnqueue(Quit);
        store.Changed += UpdateIcon;
        store.Changed += ObserveRefills;
        Notifier.Register();
        ObserveRefills();
        _refillClock = new Timer(_ => CheckRefills(), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        UpdateIcon();
        icon.Start();

        new Thread(() =>
        {
            while (_openSignal.WaitOne())
            {
                ui.TryEnqueue(drawer.Toggle);
            }
        })
        { IsBackground = true, Name = "Allowance open signal" }.Start();

        _ = store.LoadPlanAsync();
        _ = store.RefreshAsync();

        if (argv.Contains("--open"))
        {
            drawer.Toggle();
        }
    }

    private static string? ArgAfter(string[] argv, string name)
    {
        var index = Array.IndexOf(argv, name);
        return index >= 0 && index + 1 < argv.Length ? argv[index + 1] : null;
    }

    private void ObserveRefills()
    {
        lock (_refills)
        {
            var snapshot = _store!.Snapshot;
            if (snapshot is null || ReferenceEquals(snapshot, _observed))
            {
                return;
            }

            _observed = snapshot;
            foreach (var refill in _refills.Observe(snapshot.Rows, DateTimeOffset.Now))
            {
                Notifier.Show(refill.Title, refill.Body);
            }
        }
    }

    private void CheckRefills()
    {
        lock (_refills)
        {
            if (!_refills.ShouldFetch(DateTimeOffset.Now))
            {
                return;
            }
        }

        _ = _store!.RefreshAsync();
    }

    private void UpdateIcon()
    {
        var snapshot = _store!.Snapshot;
        var session = snapshot?.Rows.FirstOrDefault(r => r.Section == UsageSection.Session);
        var weekly = snapshot?.Rows.FirstOrDefault(r => r.Section == UsageSection.Weekly);
        var tip = snapshot is null
            ? "Allowance"
            : $"Session {session?.Percent}% used, weekly {weekly?.Percent}% used\n{UsageText.Updated(snapshot.FetchedAt, DateTimeOffset.Now)}";
        _icon?.Show(session?.Percent, weekly?.Percent, tip);
    }

    private void ToggleNumbers()
    {
        if (File.Exists(NumbersFlag))
        {
            File.Delete(NumbersFlag);
        }
        else
        {
            File.WriteAllText(NumbersFlag, string.Empty);
        }

        UpdateIcon();
    }

    private static bool StartsWithWindows()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue("Allowance") is not null;
    }

    private static void ToggleStartup()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (StartsWithWindows())
        {
            key.DeleteValue("Allowance", throwOnMissingValue: false);
        }
        else
        {
            key.SetValue("Allowance", $"\"{Environment.ProcessPath}\"");
        }
    }

    private void Quit()
    {
        _icon?.Remove();
        Thread.Sleep(150); // let the icon thread remove the icon before the process ends
        Environment.Exit(0);
    }
}

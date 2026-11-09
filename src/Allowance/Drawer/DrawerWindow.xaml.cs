using System.Numerics;
using Allowance.Tray;
using Allowance.Usage;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using VirtualKey = Windows.System.VirtualKey;
using Windows.UI.ViewManagement;
using WinUIEx;
using static Allowance.Native.Win32;

namespace Allowance.Drawer;

public sealed partial class DrawerWindow : WindowEx
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(5);
    private static readonly long ReopenGuardMs = 600;

    private readonly UsageStore _store;
    private readonly nint _hwnd;
    private readonly DispatcherQueue _ui;
    private readonly DispatcherQueueTimer _clock;
    private readonly UISettings _uiSettings = new();
    private readonly List<UsageRowView> _rowViews = [];
    private string _rowKey = string.Empty;
    private POINT _anchor;
    private bool _open;
    private long _closedAt;

    internal DrawerWindow(UsageStore store)
    {
        _store = store;
        InitializeComponent();
        _ui = DispatcherQueue.GetForCurrentThread();
        _hwnd = PopupChrome.Apply(this, noActivate: false);

        _clock = _ui.CreateTimer();
        _clock.Interval = TimeSpan.FromSeconds(30);
        _clock.Tick += (_, _) => Render();

        store.Changed += () => _ui.TryEnqueue(Render);
        Activated += (_, e) =>
        {
            if (e.WindowActivationState == WindowActivationState.Deactivated && _open && !KeepOpenForTest)
            {
                Close();
            }
        };

        // Show once off-screen without focus so the template exists and the card can be measured before opening.
        AppWindow.Move(new Windows.Graphics.PointInt32(-32000, -32000));
        Root.Loaded += (_, _) =>
        {
            if (!_open)
            {
                AppWindow.Hide();
            }
        };
        AppWindow.Show(false);
        Render();
    }

    internal bool KeepOpenForTest { get; init; }

    internal void ApplyTheme(ElementTheme theme) => Root.RequestedTheme = theme;

    internal void Toggle()
    {
        if (_open)
        {
            Close();
            return;
        }

        // Clicking the tray icon while open first deactivates the drawer. That click meant "close".
        if (Environment.TickCount64 - _closedAt < ReopenGuardMs)
        {
            return;
        }

        GetCursorPos(out _anchor);
        _open = true;
        Render();
        Reposition();
        PlayOpen();
        AppWindow.Show(true);
        Activate();
        SetForegroundWindow(_hwnd);
        RefreshButton.Focus(FocusState.Pointer); // keeps Esc and Tab working, with no ring on a mouse open
        _clock.Start();

        if (_store.IsOlderThan(StaleAfter))
        {
            _ = _store.RefreshAsync();
        }
    }

    private void Close()
    {
        if (!_open)
        {
            return;
        }

        _open = false;
        _closedAt = Environment.TickCount64;
        _clock.Stop();
        AppWindow.Hide();
    }

    private void OnRootKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e) => _ = _store.RefreshAsync();

    private void Render()
    {
        var snapshot = _store.Snapshot;
        var now = DateTimeOffset.Now;
        RenderRows(snapshot, now);

        PlanText.Text = _store.Plan ?? string.Empty;
        UpdatedText.Text = _store.IsFetching ? "Updating" : snapshot is null ? string.Empty : UsageText.Updated(snapshot.FetchedAt, now);

        RefreshGlyph.Visibility = _store.IsFetching ? Visibility.Collapsed : Visibility.Visible;
        RefreshRing.Visibility = _store.IsFetching ? Visibility.Visible : Visibility.Collapsed;
        RefreshRing.IsActive = _store.IsFetching;

        var message = _store.IsFetching && snapshot is null
            ? "Getting your usage"
            : UsageText.Failure(_store.Failure, snapshot is not null);
        MessageText.Text = message;
        MessageText.Visibility = message.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        MessageText.Foreground = (Brush)Root.Resources["MessageBrush"] is { } brush && _store.Failure != UsageFailure.None ? brush : (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];

        // The window hugs the card, which also limits the card: it can only grow if the window is resized first.
        if (_open)
        {
            Reposition();
        }
    }

    private void RenderRows(UsageSnapshot? snapshot, DateTimeOffset now)
    {
        var rows = snapshot?.Rows ?? [];
        var key = string.Join('|', rows.Select(r => $"{r.Section}:{r.Label}"));
        if (key != _rowKey)
        {
            _rowKey = key;
            _rowViews.Clear();
            SessionRows.Children.Clear();
            WeeklyRows.Children.Clear();
            foreach (var row in rows)
            {
                var view = new UsageRowView();
                _rowViews.Add(view);
                (row.Section == UsageSection.Session ? SessionRows : WeeklyRows).Children.Add(view);
            }
        }

        // Updating views in place lets the bars animate to their new value.
        for (var i = 0; i < rows.Count; i++)
        {
            _rowViews[i].Show(rows[i], now);
        }

        var hasWeekly = rows.Any(r => r.Section == UsageSection.Weekly);
        WeeklyHeading.Visibility = hasWeekly ? Visibility.Visible : Visibility.Collapsed;
        WeeklyRows.Visibility = hasWeekly ? Visibility.Visible : Visibility.Collapsed;
        SessionRows.Visibility = rows.Any(r => r.Section == UsageSection.Session) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Reposition()
    {
        var scale = Placement.ScaleAt(_anchor);
        Card.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var width = (int)Math.Ceiling(Card.DesiredSize.Width * scale);
        var height = (int)Math.Ceiling(Card.DesiredSize.Height * scale);

        // The window's own transparent margin is the gap from the cursor.
        var (x, y, _) = Placement.NearCursor(_anchor, width, height, 0);
        AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, y, width, height));
    }

    /// <summary>Spare Hand's pop. Scale only, so the content is on screen from the first frame.</summary>
    private void PlayOpen()
    {
        if (!_uiSettings.AnimationsEnabled)
        {
            return;
        }

        Card.ScaleTransition = null;
        Card.CenterPoint = new Vector3((float)Card.ActualWidth / 2, (float)Card.ActualHeight, 0);
        Card.Scale = new Vector3(0.96f, 0.96f, 1);
        _ui.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            Card.ScaleTransition = new Vector3Transition { Duration = TimeSpan.FromMilliseconds(150) };
            Card.Scale = Vector3.One;
        });
    }
}

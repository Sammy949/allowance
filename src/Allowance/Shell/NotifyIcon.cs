using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Allowance.Shell;

/// <summary>
/// The notification-area icon, the logo or the two percentages: left-click toggles the drawer, right-click shows a native menu. Runs on its own thread
/// with a message-only window, and re-adds itself when Explorer restarts.
/// </summary>
internal sealed class NotifyIcon
{
    private const uint WM_APP_NOTIFY = 0x8001, WM_APP_REDRAW = 0x8002, WM_CONTEXTMENU = 0x007B, NIN_SELECT = 0x0400, NIN_KEYSELECT = 0x0401;
    private const uint WM_SETTINGCHANGE = 0x001A, WM_DPICHANGED = 0x02E0, WM_CLOSE = 0x0010, WM_DESTROY = 0x0002;
    private const uint NIM_ADD = 0, NIM_MODIFY = 1, NIM_DELETE = 2, NIM_SETVERSION = 4;
    private const uint NIF_MESSAGE = 0x1, NIF_ICON = 0x2, NIF_TIP = 0x4, NIF_SHOWTIP = 0x80;
    private const int CmdOpen = 1, CmdRefresh = 2, CmdStartup = 3, CmdQuit = 4, CmdNumbers = 5;
    private const string WindowClass = "AllowanceNotifyIcon";

    // Static so the delegate Windows holds can never be collected.
    private static readonly WndProc StaticProc = Dispatch;
    private static NotifyIcon? _instance;

    private readonly Func<bool> _startsWithWindows;
    private readonly Func<bool> _showNumbers;
    private nint _hwnd;
    private nint _icon;
    private uint _taskbarCreated;
    private volatile string _tip = "Allowance";
    private int? _session, _weekly;

    public NotifyIcon(Func<bool> startsWithWindows, Func<bool> showNumbers)
    {
        _startsWithWindows = startsWithWindows;
        _showNumbers = showNumbers;
    }

    public event Action? OpenRequested;

    public event Action? RefreshRequested;

    public event Action? StartupToggled;

    public event Action? NumbersToggled;

    public event Action? QuitRequested;

    public void Start()
    {
        _instance = this;
        var thread = new Thread(Run) { IsBackground = true, Name = "Allowance notify icon" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    public void Remove()
    {
        if (_hwnd != 0)
        {
            PostMessage(_hwnd, WM_CLOSE, 0, 0);
        }
    }

    public void Show(int? session, int? weekly, string tip)
    {
        _session = session;
        _weekly = weekly;
        _tip = tip.Length > 127 ? tip[..127] : tip;
        if (_hwnd != 0)
        {
            PostMessage(_hwnd, WM_APP_REDRAW, 0, 0);
        }
    }

    private void Run()
    {
        var instance = GetModuleHandle(null);
        var wc = new WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(StaticProc),
            lpszClassName = WindowClass,
            hInstance = instance,
        };
        RegisterClassEx(ref wc);
        _hwnd = CreateWindowEx(0, WindowClass, "Allowance", 0, 0, 0, 0, 0, -3 /* HWND_MESSAGE */, 0, instance, 0);
        _taskbarCreated = RegisterWindowMessage("TaskbarCreated");
        Add();

        while (GetMessage(out var msg, 0, 0, 0) > 0)
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }
    }

    private void Add()
    {
        _icon = RenderIcon();
        var data = Data(NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_SHOWTIP);
        var added = Shell_NotifyIcon(NIM_ADD, ref data);
        data.uVersion = 4; // NOTIFYICON_VERSION_4: richer callbacks, keyboard support
        Shell_NotifyIcon(NIM_SETVERSION, ref data);
        Log.Write($"notify icon added={added}");
    }

    private void Redraw()
    {
        var old = _icon;
        _icon = RenderIcon();
        var data = Data(NIF_ICON | NIF_TIP | NIF_SHOWTIP);
        Shell_NotifyIcon(NIM_MODIFY, ref data);
        if (old != 0)
        {
            DestroyIcon(old);
        }
    }

    private NOTIFYICONDATA Data(uint flags) => new()
    {
        cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
        hWnd = _hwnd,
        uID = 1,
        uFlags = flags,
        uCallbackMessage = WM_APP_NOTIFY,
        hIcon = _icon,
        szTip = _tip,
    };

    private static nint Dispatch(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        // An exception escaping into Windows would fail-fast the whole process.
        try
        {
            return _instance?.Proc(hwnd, msg, wParam, lParam) ?? DefWindowProc(hwnd, msg, wParam, lParam);
        }
        catch (Exception e)
        {
            Log.Write($"notify icon message 0x{msg:X} failed: {e.Message}");
            return 0;
        }
    }

    private nint Proc(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        if (msg == WM_APP_NOTIFY)
        {
            var evt = (uint)(lParam & 0xFFFF);

            // Version 4 also sends WM_LBUTTONUP for one click; reacting to both would open and immediately close.
            if (evt is NIN_SELECT or NIN_KEYSELECT)
            {
                OpenRequested?.Invoke();
            }
            else if (evt == WM_CONTEXTMENU)
            {
                ShowMenu((short)(wParam & 0xFFFF), (short)((wParam >> 16) & 0xFFFF));
            }

            return 0;
        }

        if (msg == _taskbarCreated)
        {
            Add();
            return 0;
        }

        if (msg is WM_APP_REDRAW or WM_SETTINGCHANGE or WM_DPICHANGED)
        {
            Redraw();
        }

        if (msg == WM_CLOSE)
        {
            var data = Data(0);
            Shell_NotifyIcon(NIM_DELETE, ref data);
            DestroyWindow(hwnd);
            return 0;
        }

        if (msg == WM_DESTROY)
        {
            PostQuitMessage(0);
            return 0;
        }

        return DefWindowProc(hwnd, msg, wParam, lParam);
    }

    private void ShowMenu(int x, int y)
    {
        var menu = CreatePopupMenu();
        AppendMenu(menu, 0, CmdOpen, "Open");
        AppendMenu(menu, 0, CmdRefresh, "Refresh");
        AppendMenu(menu, 0x800 /* MF_SEPARATOR */, 0, null);
        AppendMenu(menu, _showNumbers() ? 0x8u /* MF_CHECKED */ : 0, CmdNumbers, "Show numbers in tray");
        AppendMenu(menu, _startsWithWindows() ? 0x8u /* MF_CHECKED */ : 0, CmdStartup, "Start with Windows");
        AppendMenu(menu, 0x800, 0, null);
        AppendMenu(menu, 0, CmdQuit, "Quit Allowance");
        SetMenuDefaultItem(menu, CmdOpen, 0);

        // Required so the menu closes when the user clicks elsewhere.
        SetForegroundWindow(_hwnd);
        var cmd = TrackPopupMenuEx(menu, 0x0100 /* TPM_RETURNCMD */ | 0x0080 /* TPM_NONOTIFY */ | 0x0020 /* TPM_BOTTOMALIGN */, x, y, _hwnd, 0);
        DestroyMenu(menu);

        switch (cmd)
        {
            case CmdOpen: OpenRequested?.Invoke(); break;
            case CmdRefresh: RefreshRequested?.Invoke(); break;
            case CmdStartup: StartupToggled?.Invoke(); break;
            case CmdNumbers: NumbersToggled?.Invoke(); break;
            case CmdQuit: QuitRequested?.Invoke(); break;
        }
    }

    private nint RenderIcon()
    {
        var size = GetSystemMetricsForDpi(49 /* SM_CXSMICON */, GetDpiForSystem());
        if (!_showNumbers())
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Allowance.ico");
            var logo = LoadImage(0, path, 1 /* IMAGE_ICON */, size, size, 0x10 /* LR_LOADFROMFILE */);
            Log.Write($"tray icon: logo at {size}px, loaded={logo != 0}");
            if (logo != 0)
            {
                return logo;
            }
        }
        else
        {
            Log.Write($"tray icon: numbers at {size}px");
        }

        using var bitmap = TrayNumberIcon.Render(_session, _weekly, size, TaskbarIsLight());
        return bitmap.GetHicon();
    }

    private static bool TaskbarIsLight() =>
        Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "SystemUsesLightTheme", 0) is 1;

    private delegate nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public uint cbSize;
        public nint hWnd;
        public uint uID, uFlags, uCallbackMessage;
        public nint hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public uint dwState, dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public uint uVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public nint hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEX
    {
        public uint cbSize, style;
        public nint lpfnWndProc;
        public int cbClsExtra, cbWndExtra;
        public nint hInstance, hIcon, hCursor, hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public nint hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public nint hwnd;
        public uint message;
        public nint wParam, lParam;
        public uint time;
        public int x, y;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern bool Shell_NotifyIcon(uint msg, ref NOTIFYICONDATA data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern ushort RegisterClassEx(ref WNDCLASSEX wc);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint CreateWindowEx(uint ex, string cls, string name, uint style, int x, int y, int w, int h, nint parent, nint menu, nint inst, nint param);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(nint hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterWindowMessage(string name);
    [DllImport("user32.dll")] private static extern int GetMessage(out MSG msg, nint hwnd, uint min, uint max);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref MSG msg);
    [DllImport("user32.dll")] private static extern nint DispatchMessage(ref MSG msg);
    [DllImport("user32.dll")] private static extern nint DefWindowProc(nint hwnd, uint msg, nint w, nint l);
    [DllImport("user32.dll")] private static extern bool PostMessage(nint hwnd, uint msg, nint w, nint l);
    [DllImport("user32.dll")] private static extern void PostQuitMessage(int code);
    [DllImport("user32.dll")] private static extern nint CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool AppendMenu(nint menu, uint flags, nint id, string? text);
    [DllImport("user32.dll")] private static extern bool SetMenuDefaultItem(nint menu, uint item, uint byPos);
    [DllImport("user32.dll")] private static extern int TrackPopupMenuEx(nint menu, uint flags, int x, int y, nint hwnd, nint tpm);
    [DllImport("user32.dll")] private static extern bool DestroyMenu(nint menu);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(nint icon);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint LoadImage(nint inst, string name, uint type, int cx, int cy, uint load);
    [DllImport("user32.dll")] private static extern uint GetDpiForSystem();
    [DllImport("user32.dll")] private static extern int GetSystemMetricsForDpi(int index, uint dpi);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? name);
}

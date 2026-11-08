using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.UI.Dispatching;

namespace AVEXOS.App;

/// <summary>
/// Manages a system tray (notification area) icon with a right-click context menu.
/// Uses Shell_NotifyIconW via P/Invoke since WinUI 3 has no built-in tray support.
/// A dedicated STA thread hosts the hidden message-only window.
/// </summary>
[SupportedOSPlatform("windows10.0.17763.0")]
public sealed class TrayIconService : IDisposable
{
    // ── Win32 constants ──────────────────────────────────────────────
    private const uint NIM_ADD        = 0x00000000;
    private const uint NIM_MODIFY     = 0x00000001;
    private const uint NIM_DELETE     = 0x00000002;
    private const uint NIM_SETVERSION = 0x00000004;

    private const uint NIF_MESSAGE = 0x00000001;
    private const uint NIF_ICON    = 0x00000002;
    private const uint NIF_TIP     = 0x00000004;
    private const uint NIF_SHOWTIP = 0x00000080;

    private const uint NOTIFYICON_VERSION_4 = 4;

    private const uint WM_TRAYICON      = 0x8001;
    private const uint WM_RBUTTONUP     = 0x0205;
    private const uint WM_LBUTTONUP     = 0x0202;
    private const uint WM_LBUTTONDBLCLK = 0x0203;
    private const uint WM_COMMAND       = 0x0111;

    private const uint TPM_RIGHTBUTTON = 0x0002;
    private const uint TPM_NONOTIFY    = 0x0080;
    private const uint TPM_RETURNCMD   = 0x0100;

    private const uint MF_STRING    = 0x00000000;
    private const uint MF_SEPARATOR = 0x00000800;
    private const uint MF_POPUP     = 0x00000010;
    private const uint MF_CHECKED   = 0x00000008;

    private const uint IMAGE_ICON      = 1;
    private const uint LR_LOADFROMFILE = 0x0010;
    private const uint LR_DEFAULTSIZE  = 0x0040;

    private const uint WS_EX_TOOLWINDOW = 0x00000080;
    private const uint WS_POPUP         = 0x80000000;

    // ── Menu command IDs ─────────────────────────────────────────────
    private const int CMD_OPEN           = 1001;
    private const int CMD_DARK_MODE      = 1002;
    private const int CMD_LIGHT_MODE     = 1003;
    private const int CMD_NEXT_WALLPAPER = 1004;
    private const int CMD_SETTINGS       = 1005;
    private const int CMD_EXIT           = 1006;
    private const int CMD_PRESENTATION   = 1007;

    // ── P/Invoke: structs ────────────────────────────────────────────
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATAW
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WNDCLASSEXW
    {
        public uint cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszMenuName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x; public int y; }

    // ── P/Invoke: shell32 ────────────────────────────────────────────
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool Shell_NotifyIconW(uint dwMessage, ref NOTIFYICONDATAW pnid);

    // ── P/Invoke: user32 ─────────────────────────────────────────────
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassExW(ref WNDCLASSEXW lpwcx);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowExW(
        uint dwExStyle, string lpClassName, string lpWindowName,
        uint dwStyle, int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProcW(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadImageW(IntPtr hInst, string lpszName, uint uType,
        int cxDesired, int cyDesired, uint fuLoad);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadIconW(IntPtr hInstance, IntPtr lpIconName);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    private static extern bool GetMessageW(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessageW(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern void PostQuitMessage(int nExitCode);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenuW(IntPtr hMenu, uint uFlags, IntPtr uIDNewItem, string? lpNewItem);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y,
        int nReserved, IntPtr hWnd, IntPtr prcRect);

    [DllImport("user32.dll")]
    private static extern bool PostMessageW(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    // ── P/Invoke: kernel32 ───────────────────────────────────────────
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string? lpModuleName);

    // ── Delegate (prevent GC of native callback) ─────────────────────
    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    // ── Fields ───────────────────────────────────────────────────────
    private readonly DispatcherQueue _dispatcher;
    private readonly Action _onOpen;
    private readonly Action<string> _onSwitchTheme;
    private readonly Action _onNextWallpaper;
    private readonly Action _onSettings;
    private readonly Action _onExit;
    private readonly Action? _onTogglePresentationMode;
    private readonly Func<string> _getCurrentThemeMode;

    private IntPtr _hiddenHwnd;
    private IntPtr _iconHandle;
    private readonly uint _iconId = 1;
    private bool _disposed;
    private bool _iconCreated;
    private bool _presentationModeActive;
    private WndProcDelegate? _wndProc;
    private Thread? _messageThread;

    public TrayIconService(
        DispatcherQueue dispatcher,
        Action onOpen,
        Action<string> onSwitchTheme,
        Action onNextWallpaper,
        Action onSettings,
        Action onExit,
        Func<string> getCurrentThemeMode,
        Action? onTogglePresentationMode = null)
    {
        _dispatcher = dispatcher;
        _onOpen = onOpen;
        _onSwitchTheme = onSwitchTheme;
        _onNextWallpaper = onNextWallpaper;
        _onSettings = onSettings;
        _onExit = onExit;
        _getCurrentThemeMode = getCurrentThemeMode;
        _onTogglePresentationMode = onTogglePresentationMode;
    }

    /// <summary>
    /// Updates the tray icon tooltip and internal state to reflect presentation mode.
    /// </summary>
    public void UpdatePresentationModeState(bool isActive)
    {
        _presentationModeActive = isActive;
        UpdateTrayTooltip(isActive ? "Presentation Mode Active" : "AVEX OS Control Panel");
    }

    private void UpdateTrayTooltip(string tooltip)
    {
        if (!_iconCreated || _hiddenHwnd == IntPtr.Zero) return;
        var nid = new NOTIFYICONDATAW
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
            hWnd = _hiddenHwnd,
            uID = _iconId,
            uFlags = NIF_TIP,
            szTip = tooltip
        };
        Shell_NotifyIconW(NIM_MODIFY, ref nid);
    }

    /// <summary>
    /// Creates the hidden message window, loads the icon, and registers the tray icon.
    /// Spawns a dedicated STA thread for the Win32 message loop.
    /// </summary>
    public void Initialize()
    {
        if (_iconCreated) return;

        var ready = new ManualResetEventSlim(false);
        _messageThread = new Thread(() => MessageThreadProc(ready))
        {
            IsBackground = true,
            Name = "TrayIconMessageThread"
        };
        _messageThread.SetApartmentState(ApartmentState.STA);
        _messageThread.Start();
        ready.Wait(TimeSpan.FromSeconds(5));
    }

    // ── Message thread ───────────────────────────────────────────────
    private void MessageThreadProc(ManualResetEventSlim ready)
    {
        _wndProc = WndProc;
        var hInstance = GetModuleHandleW(null);
        const string className = "AVEXOS_TrayWnd";

        var wc = new WNDCLASSEXW
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = hInstance,
            lpszClassName = className
        };
        RegisterClassExW(ref wc);

        // Message-only window (HWND_MESSAGE = -3)
        _hiddenHwnd = CreateWindowExW(
            WS_EX_TOOLWINDOW, className, "AVEXOS Tray",
            WS_POPUP, 0, 0, 0, 0,
            (IntPtr)(-3), IntPtr.Zero, hInstance, IntPtr.Zero);

        _iconHandle = LoadIconFromFile();
        AddTrayIcon();

        ready.Set();

        // Standard Win32 message loop
        MSG msg;
        while (GetMessageW(out msg, IntPtr.Zero, 0, 0))
        {
            TranslateMessage(ref msg);
            DispatchMessageW(ref msg);
        }

        // Cleanup after PostQuitMessage
        RemoveTrayIcon();
        if (_iconHandle != IntPtr.Zero) { DestroyIcon(_iconHandle); _iconHandle = IntPtr.Zero; }
        if (_hiddenHwnd != IntPtr.Zero) { DestroyWindow(_hiddenHwnd); _hiddenHwnd = IntPtr.Zero; }
    }

    // ── Window procedure ─────────────────────────────────────────────
    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WM_TRAYICON:
                // NOTIFYICON_VERSION_4: LOWORD(lParam) = actual mouse message
                uint trayMsg = (uint)lParam.ToInt64() & 0xFFFF;
                switch (trayMsg)
                {
                    case WM_RBUTTONUP:
                        ShowContextMenu();
                        return IntPtr.Zero;
                    case WM_LBUTTONUP:
                    case WM_LBUTTONDBLCLK:
                        _dispatcher.TryEnqueue(() => _onOpen());
                        return IntPtr.Zero;
                }
                break;

            case WM_COMMAND:
                HandleMenuCommand((int)wParam.ToInt64());
                return IntPtr.Zero;
        }

        return DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    // ── Tray icon management ─────────────────────────────────────────
    private void AddTrayIcon()
    {
        var nid = new NOTIFYICONDATAW
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
            hWnd = _hiddenHwnd,
            uID = _iconId,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_SHOWTIP,
            uCallbackMessage = WM_TRAYICON,
            hIcon = _iconHandle,
            szTip = "AVEX OS Control Panel",
            uVersion = NOTIFYICON_VERSION_4
        };
        Shell_NotifyIconW(NIM_ADD, ref nid);
        Shell_NotifyIconW(NIM_SETVERSION, ref nid);
        _iconCreated = true;
    }

    private void RemoveTrayIcon()
    {
        if (!_iconCreated) return;
        var nid = new NOTIFYICONDATAW
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
            hWnd = _hiddenHwnd,
            uID = _iconId
        };
        Shell_NotifyIconW(NIM_DELETE, ref nid);
        _iconCreated = false;
    }

    // ── Context menu ─────────────────────────────────────────────────
    private void ShowContextMenu()
    {
        SetForegroundWindow(_hiddenHwnd);

        var hMenu = CreatePopupMenu();
        var hThemeSub = CreatePopupMenu();

        string currentMode = "";
        try { currentMode = _getCurrentThemeMode(); } catch { }

        AppendMenuW(hMenu, MF_STRING, (IntPtr)CMD_OPEN, "Open Control Panel");
        AppendMenuW(hMenu, MF_SEPARATOR, IntPtr.Zero, null);

        AppendMenuW(hThemeSub, MF_STRING | (currentMode == "dark" ? MF_CHECKED : 0),
            (IntPtr)CMD_DARK_MODE, "Dark Mode");
        AppendMenuW(hThemeSub, MF_STRING | (currentMode == "light" ? MF_CHECKED : 0),
            (IntPtr)CMD_LIGHT_MODE, "Light Mode");
        AppendMenuW(hMenu, MF_STRING | MF_POPUP, hThemeSub, "Switch Theme");

        AppendMenuW(hMenu, MF_SEPARATOR, IntPtr.Zero, null);
        AppendMenuW(hMenu, MF_STRING, (IntPtr)CMD_NEXT_WALLPAPER, "Next Wallpaper");
        var presLabel = _presentationModeActive ? "Disable Presentation Mode" : "Enable Presentation Mode";
        AppendMenuW(hMenu, MF_STRING | (_presentationModeActive ? MF_CHECKED : 0), (IntPtr)CMD_PRESENTATION, presLabel);
        AppendMenuW(hMenu, MF_STRING, (IntPtr)CMD_SETTINGS, "Settings");
        AppendMenuW(hMenu, MF_SEPARATOR, IntPtr.Zero, null);
        AppendMenuW(hMenu, MF_STRING, (IntPtr)CMD_EXIT, "Exit");

        GetCursorPos(out POINT pt);
        int cmd = TrackPopupMenu(hMenu,
            TPM_RIGHTBUTTON | TPM_NONOTIFY | TPM_RETURNCMD,
            pt.x, pt.y, 0, _hiddenHwnd, IntPtr.Zero);

        DestroyMenu(hMenu);
        DestroyMenu(hThemeSub);

        if (cmd > 0)
            PostMessageW(_hiddenHwnd, WM_COMMAND, (IntPtr)cmd, IntPtr.Zero);
    }

    private void HandleMenuCommand(int cmdId)
    {
        switch (cmdId)
        {
            case CMD_OPEN:
                _dispatcher.TryEnqueue(() => _onOpen());
                break;
            case CMD_DARK_MODE:
                _dispatcher.TryEnqueue(() => _onSwitchTheme("dark"));
                break;
            case CMD_LIGHT_MODE:
                _dispatcher.TryEnqueue(() => _onSwitchTheme("light"));
                break;
            case CMD_NEXT_WALLPAPER:
                _dispatcher.TryEnqueue(() => _onNextWallpaper());
                break;
            case CMD_SETTINGS:
                _dispatcher.TryEnqueue(() => _onSettings());
                break;
            case CMD_PRESENTATION:
                _dispatcher.TryEnqueue(() => _onTogglePresentationMode?.Invoke());
                break;
            case CMD_EXIT:
                _dispatcher.TryEnqueue(() => _onExit());
                break;
        }
    }

    // ── Icon loading ─────────────────────────────────────────────────
    private IntPtr LoadIconFromFile()
    {
        var candidates = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, "assets", "avex-logo.ico"),
            Path.Combine(AppContext.BaseDirectory, "avex-logo.ico"),
        };

        // Also check the source-tree assets folder (useful during development)
        var projectAssets = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "assets"));
        if (Directory.Exists(projectAssets))
            candidates.Add(Path.Combine(projectAssets, "avex-logo.ico"));

        foreach (var path in candidates)
        {
            if (!File.Exists(path)) continue;
            var hIcon = LoadImageW(IntPtr.Zero, path, IMAGE_ICON, 32, 32,
                LR_LOADFROMFILE | LR_DEFAULTSIZE);
            if (hIcon != IntPtr.Zero) return hIcon;
        }

        // Fallback: standard Windows application icon
        return LoadIconW(IntPtr.Zero, (IntPtr)0x7F00); // IDI_APPLICATION
    }

    // ── Disposal ─────────────────────────────────────────────────────
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_hiddenHwnd != IntPtr.Zero)
            PostQuitMessage(0);

        _messageThread?.Join(TimeSpan.FromSeconds(2));
    }
}

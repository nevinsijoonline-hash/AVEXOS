using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using AVEXOS.Core;
using Microsoft.UI.Dispatching;

namespace AVEXOS.App;

/// <summary>
/// Registers global (system-wide) hotkeys via the Win32 RegisterHotKey API.
/// A hidden message-only window on a background thread receives WM_HOTKEY
/// notifications and dispatches the bound action on the UI thread.
/// Shortcut bindings are persisted through <see cref="ISettingsStore"/>.
/// </summary>
public sealed class KeyboardShortcutService : IDisposable
{
    private const string SettingsKey = "keyboard.shortcuts";
    private const int WM_HOTKEY = 0x0312;
    private const int QS_ALLINPUT = 0x04FF;

    // ---- state -----------------------------------------------------------

    private readonly ISettingsStore _settings;
    private readonly DispatcherQueue _dispatcher;
    private readonly ConcurrentDictionary<HotkeyAction, string> _shortcuts = new();

    // Background-thread infrastructure
    private Thread? _thread;
    private volatile bool _running;
    private IntPtr _msgHwnd;
    private readonly ManualResetEventSlim _initEvent = new();
    private WndProc? _wndProc;                 // prevent GC of delegate

    // Pending work enqueued before the background thread is ready
    private readonly ConcurrentQueue<Action> _pending = new();
    private readonly object _regLock = new();  // serialises RegisterHotKey calls
    private int _nextId = 9000;
    private readonly Dictionary<int, HotkeyAction> _idToAction = new();

    // ---- public callbacks ------------------------------------------------

    /// <summary>Set by the app layer to navigate to the Settings page.</summary>
    public Action? NavigateToSettings { get; set; }

    /// <summary>Set by the app layer to toggle dark/light mode.</summary>
    public Action? ToggleThemeAction { get; set; }

    /// <summary>Set by the app layer to advance the wallpaper.</summary>
    public Action? NextWallpaperAction { get; set; }

    /// <summary>Set by the app layer to toggle presentation mode.</summary>
    public Action? TogglePresentationModeAction { get; set; }

    /// <summary>Set by the app layer to toggle focus mode.</summary>
    public Action? ToggleFocusModeAction { get; set; }

    // ---- construction ----------------------------------------------------

    public KeyboardShortcutService(ISettingsStore settings)
    {
        _settings = settings;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        LoadShortcuts();
    }

    // ---- public API ------------------------------------------------------

    /// <summary>Read-only view of the current shortcut bindings.</summary>
    public IReadOnlyDictionary<HotkeyAction, string> Shortcuts => _shortcuts;

    /// <summary>
    /// Start the background message loop and register all hotkeys.
    /// Must be called once from the UI thread (typically in App.OnLaunched).
    /// </summary>
    public void Start()
    {
        if (_running) return;
        _running = true;

        _thread = new Thread(RunMessageLoop) { IsBackground = true, Name = "HotkeyThread" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();

        _initEvent.Wait(TimeSpan.FromSeconds(5));

        // Register everything that was loaded from settings
        lock (_regLock)
        {
            foreach (var (action, shortcut) in _shortcuts)
            {
                RegisterOnBgThread(action, shortcut);
            }
        }
    }

    /// <summary>
    /// Update a single shortcut binding.  Persists the change, tears down
    /// all registrations, and re-registers with the new set.
    /// Returns an error message on failure, or null on success.
    /// </summary>
    public string? SetShortcut(HotkeyAction action, string shortcut)
    {
        if (!ShortcutParser.IsValid(shortcut))
            return "Invalid shortcut. Include at least one modifier (Ctrl, Shift, Alt) and a key.";

        if (ShortcutParser.IsSystemReserved(shortcut))
            return $"\"{shortcut}\" is reserved by Windows and cannot be used.";

        if (ShortcutParser.ConflictsWith(shortcut, _shortcuts, action))
            return $"\"{shortcut}\" is already assigned to another action.";

        lock (_regLock)
        {
            UnregisterAllInternal();
            _shortcuts[action] = shortcut;
            SaveShortcuts();
            RegisterAllInternal();
        }
        return null;
    }

    /// <summary>Reset every shortcut to its factory default.</summary>
    public void ResetToDefaults()
    {
        lock (_regLock)
        {
            UnregisterAllInternal();
            _shortcuts.Clear();
            foreach (var (k, v) in ShortcutParser.GetDefaults())
                _shortcuts[k] = v;
            SaveShortcuts();
            RegisterAllInternal();
        }
    }

    /// <summary>Return the display string for an action.</summary>
    public string GetShortcutDisplay(HotkeyAction action)
        => _shortcuts.TryGetValue(action, out var s) ? s : "?";

    // ---- persistence -----------------------------------------------------

    private void LoadShortcuts()
    {
        var json = _settings.Get(SettingsKey);
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (dict is not null)
                {
                    foreach (var (name, value) in dict)
                    {
                        if (Enum.TryParse<HotkeyAction>(name, true, out var action))
                            _shortcuts[action] = value;
                    }
                }
            }
            catch { /* corrupt data – fall through to defaults */ }
        }

        // Fill in any actions that were not stored yet
        foreach (var (action, def) in ShortcutParser.GetDefaults())
            _shortcuts.TryAdd(action, def);
    }

    private void SaveShortcuts()
    {
        var dict = new Dictionary<string, string>();
        foreach (var (action, shortcut) in _shortcuts)
            dict[action.ToString()] = shortcut;
        _settings.Set(SettingsKey, JsonSerializer.Serialize(dict));
    }

    // ---- Win32 hotkey registration ---------------------------------------

    private void RegisterAllInternal()
    {
        foreach (var (action, shortcut) in _shortcuts)
            RegisterOnBgThread(action, shortcut);
    }

    private void UnregisterAllInternal()
    {
        foreach (var id in _idToAction.Keys.ToList())
        {
            UnregisterHotKey(_msgHwnd, id);
        }
        _idToAction.Clear();
    }

    /// <summary>
    /// Queue a RegisterHotKey call to be executed on the background thread
    /// (which owns the message-only window).
    /// </summary>
    private void RegisterOnBgThread(HotkeyAction action, string shortcut)
    {
        if (!ShortcutParser.TryParse(shortcut, out var mod, out var vk))
            return;

        if (_msgHwnd == IntPtr.Zero)
        {
            // Background thread not ready yet – queue for later
            _pending.Enqueue(() => RegisterHotkeyOnThread(action, mod, vk));
            return;
        }

        RegisterHotkeyOnThread(action, mod, vk);
    }

    private void RegisterHotkeyOnThread(HotkeyAction action, uint mod, byte vk)
    {
        var id = Interlocked.Increment(ref _nextId);
        if (RegisterHotKey(_msgHwnd, id, mod, vk))
        {
            _idToAction[id] = action;
        }
        else
        {
            var err = Marshal.GetLastWin32Error();
            App.Trace($"RegisterHotKey failed for {action} ({shortcut(mod, vk)}): error {err}");
        }

        static string shortcut(uint m, byte k) => ShortcutParser.Format(m, k);
    }

    // ---- background message loop -----------------------------------------

    private void RunMessageLoop()
    {
        _wndProc = WndProcDelegate;

        // 1. Register window class
        var className = "AVEXOS_HotkeyMsgWnd_" + Process.GetCurrentProcess().Id;
        var wc = new WndClassEx
        {
            cbSize = (uint)Marshal.SizeOf<WndClassEx>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = GetModuleHandle(null),
            lpszClassName = className,
        };
        RegisterClassEx(ref wc);

        // 2. Create message-only window (parent = HWND_MESSAGE = -3)
        _msgHwnd = CreateWindowEx(
            0, className, "AVEXOS Hotkeys", 0, 0, 0, 0, 0,
            new IntPtr(-3), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

        if (_msgHwnd == IntPtr.Zero)
        {
            App.Trace("CreateWindowEx failed for hotkey message window: " + Marshal.GetLastWin32Error());
            _initEvent.Set();
            return;
        }

        // 3. Process anything that was queued before we were ready
        while (_pending.TryDequeue(out var action))
            action();

        _initEvent.Set();

        // 4. Message loop – uses MsgWaitForMultipleObjects so we can also
        //    wake up when new registrations are enqueued (via _pending).
        var handles = new IntPtr[1]; // placeholder; we use MsgWaitForMultipleObjects
        while (_running)
        {
            // Wait for a message OR a queue-apc-style signal (timeout 100 ms)
            int waitResult = MsgWaitForMultipleObjects(0, IntPtr.Zero, false, 100, QS_ALLINPUT);

            while (PeekMessage(out var msg, _msgHwnd, 0, 0, 1 /*PM_REMOVE*/))
            {
                if (msg.message == WM_HOTKEY)
                {
                    int hotkeyId = (int)msg.wParam;
                    if (_idToAction.TryGetValue(hotkeyId, out var firedAction))
                    {
                        var captured = firedAction;
                        _dispatcher.TryEnqueue(() => ExecuteAction(captured));
                    }
                }
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }

            // Also drain any late registrations
            while (_pending.TryDequeue(out var pendingAction))
                pendingAction();
        }

        // Cleanup
        UnregisterAllInternal();
        DestroyWindow(_msgHwnd);
        UnregisterClassW(className, GetModuleHandle(null));
        _msgHwnd = IntPtr.Zero;
    }

    private void ExecuteAction(HotkeyAction action)
    {
        try
        {
            switch (action)
            {
                case HotkeyAction.ToggleTheme:
                    ToggleThemeAction?.Invoke();
                    break;
                case HotkeyAction.NextWallpaper:
                    NextWallpaperAction?.Invoke();
                    break;
                case HotkeyAction.OpenPanel:
                    // Bring the main window to the foreground
                    var mainWnd = App.Main;
                    if (mainWnd is not null)
                    {
                        mainWnd.Activate();
                    }
                    break;
                case HotkeyAction.OpenSettings:
                    NavigateToSettings?.Invoke();
                    break;
                case HotkeyAction.TogglePresentationMode:
                    TogglePresentationModeAction?.Invoke();
                    break;
                case HotkeyAction.ToggleFocusMode:
                    ToggleFocusModeAction?.Invoke();
                    break;
            }
        }
        catch (Exception ex)
        {
            App.Trace($"Hotkey action {action} failed: {ex}");
        }
    }

    private IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        // All real work is done via PeekMessage in the loop; just forward.
        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    // ---- IDisposable -----------------------------------------------------

    public void Dispose()
    {
        if (!_running) return;
        _running = false;
        _thread?.Join(TimeSpan.FromSeconds(2));
        _initEvent.Dispose();
    }

    // ======================================================================
    // P/Invoke declarations
    // ======================================================================

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WndClassEx
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
        public string? lpszMenuName;
        public string? lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Msg
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int pt_x;
        public int pt_y;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassEx(ref WndClassEx lpwcx);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(
        uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle,
        int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterClassW(string lpClassName, IntPtr hInstance);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PeekMessage(out Msg lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TranslateMessage(ref Msg lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref Msg lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int MsgWaitForMultipleObjects(int nCount, IntPtr pHandles, [MarshalAs(UnmanagedType.Bool)] bool fWaitAll, int dwMilliseconds, uint dwWakeMask);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}

using System.Management;
using System.Text.Json;
using AVEXOS.Core;

namespace AVEXOS.WindowsIntegration;

public sealed class AutoBrightnessService : IAutoBrightnessService
{
    private const string ConfigKey = "autobrightness.config";
    private const string EnabledKey = "autobrightness.enabled";
    private const int TimerIntervalMs = 60_000; // 1 minute

    private readonly ISettingsStore _settings;
    private readonly IClock _clock;
    private readonly Action<int> _brightnessApplier;
    private AutoBrightnessConfig _config;
    private System.Threading.Timer? _timer;
    private int _currentBrightness = -1;
    private bool _disposed;

    public bool IsEnabled => _config.IsEnabled;

    public AutoBrightnessService(ISettingsStore settings, IClock clock)
        : this(settings, clock, ApplyBrightnessViaWmi)
    {
    }

    /// <summary>Testable constructor that accepts a custom brightness applier.</summary>
    public AutoBrightnessService(ISettingsStore settings, IClock clock, Action<int> brightnessApplier)
    {
        _settings = settings;
        _clock = clock;
        _brightnessApplier = brightnessApplier;
        _config = LoadConfig();
    }

    public AutoBrightnessConfig GetConfig() => _config;

    public void SaveConfig(AutoBrightnessConfig config)
    {
        config.Validate();
        _config = config;
        var json = JsonSerializer.Serialize(_config);
        _settings.Set(ConfigKey, json);
        _settings.Set(EnabledKey, _config.IsEnabled.ToString());

        if (_config.IsEnabled)
            Start();
        else
            Stop();
    }

    public void Enable()
    {
        _config.IsEnabled = true;
        SaveConfig(_config);
    }

    public void Disable()
    {
        _config.IsEnabled = false;
        SaveConfig(_config);
    }

    public int GetCurrentBrightness() => _currentBrightness;

    public void ApplyBrightness(int level)
    {
        level = Math.Clamp(level, 0, 100);
        try
        {
            _brightnessApplier(level);
            _currentBrightness = level;
        }
        catch
        {
            // Gracefully fail if brightness control is not available (e.g. desktop without DDC/CI)
        }
    }

    public void Start()
    {
        if (_timer is not null) return;
        // Check immediately, then every minute
        _timer = new System.Threading.Timer(_ => Tick(), null, 0, TimerIntervalMs);
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    /// <summary>Called by the timer. Calculates and applies brightness for the current time.</summary>
    public void Tick()
    {
        if (!_config.IsEnabled) return;
        var now = _clock.UtcNow.ToLocalTime();
        var target = CalculateBrightness(now);
        ApplyBrightness(target);
    }

    /// <summary>
    /// Calculate the target brightness for a given local time.
    /// Model:
    ///   Day period: [DayStartTime, NightStartTime) -> DayBrightness
    ///   Transition: [NightStartTime, NightStartTime + TransitionMinutes) -> lerp day->night
    ///   Night period: [NightStartTime + TransitionMinutes, DayStartTime) -> NightBrightness
    /// </summary>
    public int CalculateBrightness(DateTimeOffset localTime)
    {
        var nowMinutes = (int)localTime.TimeOfDay.TotalMinutes;
        var dayStartMin = (int)_config.DayStartTime.TotalMinutes;
        var nightStartMin = (int)_config.NightStartTime.TotalMinutes;
        var transitionMin = _config.TransitionMinutes;
        var dayBrightness = _config.DayBrightness;
        var nightBrightness = _config.NightBrightness;
        var nightEndMin = (nightStartMin + transitionMin) % (24 * 60);

        // Check each period using circular time comparison
        if (IsInPeriod(nowMinutes, dayStartMin, nightStartMin))
            return dayBrightness;

        if (transitionMin > 0 && IsInPeriod(nowMinutes, nightStartMin, nightEndMin))
        {
            // In transition period - calculate progress
            var elapsed = CircularDiff(nowMinutes, nightStartMin);
            double progress = Math.Clamp((double)elapsed / transitionMin, 0.0, 1.0);
            return Lerp(dayBrightness, nightBrightness, progress);
        }

        // Otherwise we're in the night period
        return nightBrightness;
    }

    /// <summary>Check if current minute is in the circular period [start, end).</summary>
    private static bool IsInPeriod(int now, int start, int end)
    {
        if (start < end)
            return now >= start && now < end;
        else // wraps midnight
            return now >= start || now < end;
    }

    /// <summary>Circular difference: how many minutes from start to now (clockwise).</summary>
    private static int CircularDiff(int now, int start)
    {
        var diff = now - start;
        if (diff < 0) diff += 24 * 60;
        return diff;
    }

    private static int Lerp(int a, int b, double t)
    {
        return (int)Math.Round(a + (b - a) * t);
    }

    private AutoBrightnessConfig LoadConfig()
    {
        var json = _settings.Get(ConfigKey);
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                var config = JsonSerializer.Deserialize<AutoBrightnessConfig>(json);
                if (config is not null)
                {
                    var enabledStr = _settings.Get(EnabledKey);
                    config.IsEnabled = string.Equals(enabledStr, "True", StringComparison.OrdinalIgnoreCase);
                    return config;
                }
            }
            catch { /* corrupt data, return default */ }
        }
        return new AutoBrightnessConfig();
    }

    /// <summary>Apply brightness via WMI (Win32_VideoController / WmiMonitorBrightnessMethods).</summary>
    private static void ApplyBrightnessViaWmi(int level)
    {
        try
        {
            using var mc = new ManagementClass(@"root\WMI", "WmiMonitorBrightnessMethods", null);
            using var instances = mc.GetInstances();
            foreach (ManagementObject instance in instances)
            {
                instance.InvokeMethod("WmiSetBrightness", new object[] { 0, (byte)level });
            }
        }
        catch
        {
            // WMI not available - try dxva2 fallback for external monitors
            ApplyBrightnessViaDxva2(level);
        }
    }

    /// <summary>Fallback: use dxva2.dll SetMonitorBrightness for external monitors with DDC/CI.</summary>
    private static void ApplyBrightnessViaDxva2(int level)
    {
        try
        {
            var hMonitor = NativeMethods.GetPrimaryMonitorHandle();
            if (hMonitor == IntPtr.Zero) return;

            uint count;
            if (!NativeMethods.GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, out count) || count == 0)
                return;

            var monitors = new NativeMethods.PHYSICAL_MONITOR[count];
            if (!NativeMethods.GetPhysicalMonitorsFromHMONITOR(hMonitor, count, monitors))
                return;

            try
            {
                foreach (var m in monitors)
                {
                    NativeMethods.SetMonitorBrightness(m.hPhysicalMonitor, (uint)level);
                }
            }
            finally
            {
                NativeMethods.DestroyPhysicalMonitors(count, monitors);
            }
        }
        catch
        {
            // Not a laptop with brightness control, or DDC/CI not supported - silently fail
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}

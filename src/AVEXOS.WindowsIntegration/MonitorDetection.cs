using System.Runtime.InteropServices;
using AVEXOS.Core;

namespace AVEXOS.WindowsIntegration;

/// <summary>
/// Detects connected display monitors using Win32 API.
/// </summary>
public static class MonitorDetection
{
    /// <summary>
    /// Enumerates all connected monitors and returns their info.
    /// </summary>
    public static IReadOnlyList<MonitorInfo> GetMonitors()
    {
        var monitors = new List<MonitorInfo>();

        bool Callback(IntPtr hMonitor, IntPtr hdcMonitor, ref NativeMethods.RECT lprcMonitor, IntPtr dwData)
        {
            var mi = new NativeMethods.MONITORINFOEX();
            mi.cbSize = Marshal.SizeOf(mi);

            if (NativeMethods.GetMonitorInfo(hMonitor, ref mi))
            {
                var deviceId = mi.szDevice;
                var friendlyName = GetFriendlyName(deviceId, monitors.Count);
                var isPrimary = (mi.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0;

                monitors.Add(new MonitorInfo(
                    DeviceId: deviceId,
                    DisplayName: friendlyName,
                    Width: mi.rcMonitor.Right - mi.rcMonitor.Left,
                    Height: mi.rcMonitor.Bottom - mi.rcMonitor.Top,
                    Left: mi.rcMonitor.Left,
                    Top: mi.rcMonitor.Top,
                    IsPrimary: isPrimary
                ));
            }

            return true; // continue enumeration
        }

        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, Callback, IntPtr.Zero);

        // Sort: primary first, then by left position
        return monitors
            .OrderByDescending(m => m.IsPrimary)
            .ThenBy(m => m.Left)
            .ToList();
    }

    /// <summary>
    /// Gets the primary monitor from the list, or null if none detected.
    /// </summary>
    public static MonitorInfo? GetPrimaryMonitor(IReadOnlyList<MonitorInfo> monitors)
    {
        return monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors.FirstOrDefault();
    }

    private static string GetFriendlyName(string deviceId, int index)
    {
        // deviceId is typically like "\\.\DISPLAY1"
        // Try to extract a friendlier name
        var name = deviceId.Replace(@"\\.\", "");
        if (string.IsNullOrWhiteSpace(name))
        {
            name = $"Monitor {index + 1}";
        }
        return name;
    }
}

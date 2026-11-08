using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using AVEXOS.Core;

namespace AVEXOS.WindowsIntegration;

public sealed class AppDiscoveryService : IAppDiscoveryService
{
    private static readonly string[] UninstallPaths =
    {
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
    };

    private static readonly HashSet<string> SkipReleaseTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Update", "Hotfix", "Security Update", "Service Pack"
    };

    public async Task<IReadOnlyList<DiscoveredApplication>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<DiscoveredApplication>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var views = new[] { RegistryView.Registry64, RegistryView.Registry32 };
        foreach (var view in views)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            foreach (var subPath in UninstallPaths)
            {
                using var subKey = baseKey.OpenSubKey(subPath);
                if (subKey is null) continue;
                foreach (var name in subKey.GetSubKeyNames())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    using var appKey = subKey.OpenSubKey(name);
                    if (appKey is null) continue;

                    var displayName = appKey.GetValue("DisplayName") as string;
                    if (string.IsNullOrWhiteSpace(displayName)) continue;

                    var systemComponent = Convert.ToInt32(appKey.GetValue("SystemComponent") ?? 0);
                    if (systemComponent == 1) continue;

                    var parentKeyName = appKey.GetValue("ParentKeyName") as string;
                    if (!string.IsNullOrWhiteSpace(parentKeyName)) continue;

                    var releaseType = appKey.GetValue("ReleaseType") as string;
                    if (releaseType is not null && SkipReleaseTypes.Contains(releaseType)) continue;

                    var publisher = appKey.GetValue("Publisher") as string ?? "";
                    var version = appKey.GetValue("DisplayVersion") as string ?? appKey.GetValue("Version") as string ?? "";
                    var installLocation = appKey.GetValue("InstallLocation") as string ?? "";
                    var displayIcon = appKey.GetValue("DisplayIcon") as string ?? "";

                    var id = ComputeId(displayName, publisher, version, name);
                    var key = $"{displayName}|{publisher}|{version}";
                    if (!seen.Add(key)) continue;

                    var type = string.IsNullOrWhiteSpace(installLocation) ? "Unknown" : "Desktop";
                    var source = view == RegistryView.Registry64 ? "Registry (HKLM64)" : "Registry (HKLM32)";

                    var capability = CapabilityRegistry.Resolve(displayName);

                    results.Add(new DiscoveredApplication(
                        id, displayName, publisher, version, installLocation, type, source,
                        capability.Level, capability.Reason));
                }
            }
        }

        // HKCU
        using var hkcu = Registry.CurrentUser.OpenSubKey(UninstallPaths[0]);
        if (hkcu is not null)
        {
            foreach (var name in hkcu.GetSubKeyNames())
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var appKey = hkcu.OpenSubKey(name);
                if (appKey is null) continue;
                var displayName = appKey.GetValue("DisplayName") as string;
                if (string.IsNullOrWhiteSpace(displayName)) continue;
                var systemComponent = Convert.ToInt32(appKey.GetValue("SystemComponent") ?? 0);
                if (systemComponent == 1) continue;
                var parentKeyName = appKey.GetValue("ParentKeyName") as string;
                if (!string.IsNullOrWhiteSpace(parentKeyName)) continue;
                var publisher = appKey.GetValue("Publisher") as string ?? "";
                var version = appKey.GetValue("DisplayVersion") as string ?? appKey.GetValue("Version") as string ?? "";
                var installLocation = appKey.GetValue("InstallLocation") as string ?? "";
                var id = ComputeId(displayName, publisher, version, name);
                var key = $"{displayName}|{publisher}|{version}";
                if (!seen.Add(key)) continue;
                var type = string.IsNullOrWhiteSpace(installLocation) ? "Unknown" : "Desktop";
                var capability = CapabilityRegistry.Resolve(displayName);
                results.Add(new DiscoveredApplication(id, displayName, publisher, version, installLocation, type, "Registry (HKCU)", capability.Level, capability.Reason));
            }
        }

        return results
            .OrderBy(a => a.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Take(80)
            .ToList();
    }

    private static string ComputeId(string name, string publisher, string version, string registryName)
    {
        var input = $"{name}|{publisher}|{version}|{registryName}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return "app-" + Convert.ToHexString(bytes)[..12].ToLowerInvariant();
    }
}

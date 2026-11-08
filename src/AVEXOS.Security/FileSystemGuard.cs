using AVEXOS.Core;

namespace AVEXOS.Security;

public sealed class FileSystemGuard : IFileSystemGuard
{
    private readonly IPathPolicy _policy;

    public FileSystemGuard(IPathPolicy policy) => _policy = policy;

    public GuardResult EnsureWritable(string path)
    {
        string canonical;
        try
        {
            canonical = _policy.Canonicalize(path);
        }
        catch (Exception ex)
        {
            return GuardResult.Deny(path, $"Invalid path: {ex.Message}");
        }

        if (!_policy.IsAllowed(canonical, out var reason))
        {
            return GuardResult.Deny(canonical, reason);
        }

        var parent = Path.GetDirectoryName(canonical);
        if (string.IsNullOrEmpty(parent))
        {
            return GuardResult.Deny(canonical, "No parent directory.");
        }

        string parentCanonical;
        try
        {
            parentCanonical = _policy.Canonicalize(parent);
        }
        catch (Exception ex)
        {
            return GuardResult.Deny(canonical, $"Invalid parent path: {ex.Message}");
        }

        if (!_policy.IsAllowed(parentCanonical, out var parentReason))
        {
            return GuardResult.Deny(canonical, $"Parent rejected: {parentReason}");
        }

        return GuardResult.Allow(canonical);
    }

    public GuardResult EnsureDirectory(string path)
    {
        var result = EnsureWritable(path);
        if (!result.Allowed)
        {
            return result;
        }

        try
        {
            Directory.CreateDirectory(result.CanonicalPath);
            return result;
        }
        catch (Exception ex)
        {
            return GuardResult.Deny(result.CanonicalPath, $"Could not create directory: {ex.Message}");
        }
    }
}

/// <summary>Registry writes are restricted to a fixed, user-scope allowlist.</summary>
public static class RegistryPolicy
{
    // HKCU\Software\Classes is absent on purpose: spec rule 22 forbids touching file associations.
    private static readonly string[] AllowedWriteRoots =
    {
        @"HKEY_CURRENT_USER\Control Panel\Desktop",
        @"HKEY_CURRENT_USER\Control Panel\Cursors",
        @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
        @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\DriveIcons",
        @"HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM",
        @"HKEY_CURRENT_USER\AppEvents\Schemes"
    };

    public static bool IsWritable(string keyPath, out string reason)
    {
        if (keyPath.StartsWith(@"HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase) ||
            keyPath.StartsWith(@"HKLM", StringComparison.OrdinalIgnoreCase))
        {
            reason = "Machine-wide registry writes are not permitted.";
            return false;
        }

        foreach (var root in AllowedWriteRoots)
        {
            if (keyPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                reason = "Allowed user-scope key.";
                return true;
            }
        }

        reason = "Registry key is outside the allowed user-scope list.";
        return false;
    }
}

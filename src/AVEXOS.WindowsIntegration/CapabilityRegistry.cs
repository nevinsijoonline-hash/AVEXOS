using AVEXOS.Core;

namespace AVEXOS.WindowsIntegration;

public static class CapabilityRegistry
{
    private static readonly (string Pattern, CapabilityLevel Level, string Reason)[] Known =
    {
        ("windows terminal", CapabilityLevel.A, "Documented settings.json theme support."),
        ("visual studio code", CapabilityLevel.A, "Documented settings.json and extension theming."),
        ("avex", CapabilityLevel.A, "AVEX OS native surface.")
    };

    public sealed record Capability(CapabilityLevel Level, string Reason);

    public static Capability Resolve(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return new Capability(CapabilityLevel.D, "Unknown application.");

        var lower = displayName.ToLowerInvariant();
        foreach (var (pattern, level, reason) in Known)
        {
            if (lower.Contains(pattern))
                return new Capability(level, reason);
        }

        return new Capability(CapabilityLevel.D, "No documented theming API. Follows system light/dark only.");
    }
}

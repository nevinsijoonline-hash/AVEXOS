namespace AVEXOS.Security;

/// <summary>
/// Deny-by-default path policy. Canonicalises before evaluating, so junctions,
/// symlinks, traversal, device prefixes, alternate data streams, 8.3 names,
/// trailing dots and case variations all resolve to the same decision.
/// </summary>
public sealed class ProtectedPathPolicy : AVEXOS.Core.IPathPolicy
{
    private readonly string[] _protectedRoots;
    private readonly string[] _allowedRoots;

    public ProtectedPathPolicy(IEnumerable<string>? allowedRoots = null)
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        _protectedRoots = new[]
        {
            windows,
            programFiles,
            programFilesX86,
            Path.Combine(programData, "Microsoft"),
            @"C:\Recovery",
            @"C:\System Volume Information",
            @"C:\$Recycle.Bin",
            @"C:\Boot",
            @"C:\EFI"
        }.Where(r => !string.IsNullOrWhiteSpace(r)).ToArray();

        _allowedRoots = (allowedRoots ?? new[]
        {
            AVEXOS.Core.AvexPaths.Root,
            AVEXOS.Core.AvexPaths.RoamingRoot
        }).Where(r => !string.IsNullOrWhiteSpace(r)).ToArray();
    }

    public IReadOnlyList<string> ProtectedRoots => _protectedRoots;
    public IReadOnlyList<string> AllowedRoots => _allowedRoots;

    public bool IsAllowed(string canonicalPath, out string reason)
    {
        var path = canonicalPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        foreach (var root in _protectedRoots)
        {
            if (IsUnder(path, root))
            {
                reason = $"Protected system location ({root}).";
                return false;
            }
        }

        var volumeRoot = Path.GetPathRoot(path)?.TrimEnd(Path.DirectorySeparatorChar);
        if (!string.IsNullOrEmpty(volumeRoot) && string.Equals(path, volumeRoot, StringComparison.OrdinalIgnoreCase))
        {
            reason = "Volume roots cannot be modified.";
            return false;
        }

        foreach (var root in _allowedRoots)
        {
            if (IsUnder(path, root))
            {
                reason = "Allowed: inside AVEX OS data root.";
                return true;
            }
        }

        reason = "Outside the AVEX OS data roots.";
        return false;
    }

    public string Canonicalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path is empty.", nameof(path));
        }

        var value = path.Trim().Trim('"');

        if (value.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
        {
            value = @"\\" + value[8..];
        }
        else if (value.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase) ||
                 value.StartsWith(@"\\.\", StringComparison.OrdinalIgnoreCase))
        {
            value = value[4..];
        }

        value = StripAlternateDataStream(value);

        string full;
        try
        {
            full = Path.GetFullPath(value);
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Path is not valid: {ex.Message}", nameof(path), ex);
        }

        full = ResolveReparsePoints(full);
        return TrimTrailingDotsAndSpaces(full);
    }

    private static string StripAlternateDataStream(string value)
    {
        // Keep the drive colon; strip stream syntax after any later colon.
        var searchFrom = value.StartsWith(@"\\", StringComparison.Ordinal) ? 2 : 2;
        var colon = value.IndexOf(':', searchFrom);
        return colon > 1 ? value[..colon] : value;
    }

    private static string ResolveReparsePoints(string fullPath)
    {
        var root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrEmpty(root))
        {
            return fullPath;
        }

        var prefix = root;
        var segments = fullPath[root.Length..]
            .Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries);

        foreach (var segment in segments)
        {
            var candidate = Path.Combine(prefix, segment);
            var target = TryResolveSelfLink(candidate);
            prefix = target is not null ? Path.GetFullPath(target) : candidate;
        }

        return prefix;
    }

    /// <summary>
    /// Resolves one component of an already-resolved prefix. Asking per component matters: a link
    /// two levels up still has to be followed even when the component below it exists through that
    /// link and is therefore not itself a reparse point.
    /// </summary>
    private static string? TryResolveSelfLink(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                return File.ResolveLinkTarget(path, returnFinalTarget: true)?.FullName;
            }

            return Directory.Exists(path)
                ? Directory.ResolveLinkTarget(path, returnFinalTarget: true)?.FullName
                : null;
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    private static string TrimTrailingDotsAndSpaces(string fullPath)
    {
        var root = Path.GetPathRoot(fullPath) ?? string.Empty;
        var remainder = fullPath[root.Length..];
        if (remainder.Length == 0)
        {
            return fullPath;
        }

        var parts = remainder
            .Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.TrimEnd('.', ' '))
            .ToArray();

        return Path.Combine(new[] { root }.Concat(parts).ToArray());
    }

    private static bool IsUnder(string path, string root)
    {
        var normalisedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(path, normalisedRoot, StringComparison.OrdinalIgnoreCase)
               || path.StartsWith(normalisedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}

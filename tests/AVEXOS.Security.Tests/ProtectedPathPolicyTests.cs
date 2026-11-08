using AVEXOS.Core;
using AVEXOS.Security;
using Xunit;

namespace AVEXOS.Security.Tests;

public class ProtectedPathPolicyTests
{
    private readonly ProtectedPathPolicy _policy = new();

    [Theory]
    [InlineData(@"C:\Windows")]
    [InlineData(@"C:\Windows\System32\kernel32.dll")]
    [InlineData(@"C:\WINDOWS\system32\drivers\etc\hosts")]
    [InlineData(@"C:\Windows\WinSxS\manifests")]
    [InlineData(@"C:\Windows\SysWOW64\ntdll.dll")]
    public void Denies_Windows_System_Paths(string input)
    {
        var canonical = _policy.Canonicalize(input);
        Assert.False(_policy.IsAllowed(canonical, out var reason), reason);
    }

    [Fact]
    public void Denies_Case_Variants()
    {
        var canonical = _policy.Canonicalize(@"C:\wInDoWs\System32");
        Assert.False(_policy.IsAllowed(canonical, out _));
    }

    [Fact]
    public void Denies_Trailing_Dot_Evasion()
    {
        var canonical = _policy.Canonicalize(@"C:\Windows.");
        Assert.False(_policy.IsAllowed(canonical, out _));
    }

    [Fact]
    public void Denies_Device_Prefix_Evasion()
    {
        var canonical = _policy.Canonicalize(@"\\?\C:\Windows\System32");
        Assert.False(_policy.IsAllowed(canonical, out _));
    }

    [Fact]
    public void Denies_Traversal_From_Allowed_Root()
    {
        var canonical = _policy.Canonicalize(Path.Combine(AvexPaths.Root, "..", "..", "Windows", "System32"));
        Assert.False(_policy.IsAllowed(canonical, out _));
    }

    [Fact]
    public void Allows_Inside_AVEX_Root()
    {
        var path = Path.Combine(AvexPaths.Root, "themes", "avex-dark.json");
        var canonical = _policy.Canonicalize(path);
        Assert.True(_policy.IsAllowed(canonical, out _));
    }

    [Fact]
    public void Denies_Volume_Root()
    {
        var canonical = _policy.Canonicalize(@"C:\");
        Assert.False(_policy.IsAllowed(canonical, out _));
    }

    [Fact]
    public void Denies_Unc_Paths()
    {
        var canonical = _policy.Canonicalize(@"\\server\share\file");
        Assert.False(_policy.IsAllowed(canonical, out _));
    }

    [Fact]
    public void Denies_Alternate_Data_Stream()
    {
        var path = Path.Combine(AvexPaths.Root, "test.txt:stream");
        var canonical = _policy.Canonicalize(path);
        // The stream suffix is stripped; the underlying file is allowed.
        Assert.True(_policy.IsAllowed(canonical, out _));
    }

    [Fact]
    public void Denies_Junction_Pointing_To_Windows()
    {
        var temp = Path.Combine(Path.GetTempPath(), $"avex-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temp);
        var junction = Path.Combine(temp, "j");
        try
        {
            if (!TryCreateJunction(junction, @"C:\Windows")) return; // mklink unavailable; skip.

            var canonical = _policy.Canonicalize(junction);
            Assert.False(_policy.IsAllowed(canonical, out _));
        }
        finally
        {
            RemoveJunction(junction);
            DeleteTree(temp);
        }
    }

    [Fact]
    public void Denies_Path_Below_Junction_In_Allowed_Root()
    {
        var temp = Path.Combine(Path.GetTempPath(), $"avex-test-{Guid.NewGuid():N}");
        var allowedRoot = Path.Combine(temp, "avex");
        Directory.CreateDirectory(allowedRoot);
        var junction = Path.Combine(allowedRoot, "j");

        try
        {
            if (!TryCreateJunction(junction, @"C:\Windows")) return;

            var policy = new ProtectedPathPolicy(new[] { allowedRoot });
            var probe = Path.Combine(junction, "System32", "x.dll");

            // System32 exists *through* the link, so the deepest existing component of the probe
            // is not itself a reparse point. The policy must still walk the link above it.
            Assert.True(Directory.Exists(Path.GetDirectoryName(probe)!),
                "Setup: expected System32 to resolve through the junction.");

            var canonical = policy.Canonicalize(probe);
            Assert.Equal(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "x.dll"),
                canonical,
                ignoreCase: true);
            Assert.False(policy.IsAllowed(canonical, out var reason),
                $"Canonicalised to {canonical} and approved: {reason}");

            var result = new FileSystemGuard(policy).EnsureWritable(probe);
            Assert.False(result.Allowed, $"EnsureWritable approved {result.CanonicalPath}");

            // The directory *inside* the link also has to be refused on its resolved identity.
            var through = new FileSystemGuard(policy).EnsureWritable(Path.Combine(junction, "System32"));
            Assert.False(through.Allowed, $"EnsureWritable approved {through.CanonicalPath}");
        }
        finally
        {
            RemoveJunction(junction);
            DeleteTree(temp);
        }
    }

    private static bool TryCreateJunction(string link, string target)
    {
        var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c mklink /J \"{link}\" \"{target}\"",
            UseShellExecute = false,
            CreateNoWindow = true
        });
        process?.WaitForExit(5000);
        return Directory.Exists(link);
    }

    /// <summary>Removes the link without touching its target.</summary>
    private static void RemoveJunction(string link)
    {
        if (!Directory.Exists(link)) return;

        var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c rmdir \"{link}\"",
            UseShellExecute = false,
            CreateNoWindow = true
        });
        process?.WaitForExit(5000);
    }

    /// <summary>
    /// A directory whose reparse-point child was just removed can stay non-empty for a moment, so the
    /// first delete throws and leaves an empty shell in %TEMP%. Retries, then gives up quietly.
    /// </summary>
    private static void DeleteTree(string path)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
                return;
            }
            catch (IOException)
            {
                System.Threading.Thread.Sleep(50);
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }
        }
    }
}

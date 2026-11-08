using System.Text.Json;
using AVEXOS.Core;
using Xunit;

namespace AVEXOS.Backup.Tests;

public class SettingsBackupServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly InMemorySettingsStore _settings;
    private readonly FakeClock _clock;
    private readonly SettingsBackupService _service;

    public SettingsBackupServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"avex-settings-backup-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        AvexPaths.OverrideBackupDirForTests(_testDir);

        _settings = new InMemorySettingsStore();
        _clock = new FakeClock(new DateTimeOffset(2026, 11, 8, 14, 30, 0, TimeSpan.Zero));
        _service = new SettingsBackupService(_settings, _clock);
    }

    [Fact]
    public void CreateBackup_WritesFile_WithExpectedName()
    {
        _settings.Set("theme.id", "avex.dark");
        _settings.Set("theme.mode", "dark");

        var info = _service.CreateBackup();

        Assert.Equal("avex-settings-backup-20261108-143000.json", info.FileName);
        Assert.True(File.Exists(info.FilePath));
        Assert.Equal(2, info.SettingCount);
        Assert.True(info.FileSizeBytes > 0);
    }

    [Fact]
    public void CreateBackup_ProducesValidJson_WithAllSettings()
    {
        _settings.Set("theme.id", "avex.dark");
        _settings.Set("theme.mode", "dark");
        _settings.Set("wallpaper.slideshow.paths", @"C:\a|C:\b");

        var info = _service.CreateBackup();

        var json = File.ReadAllText(info.FilePath);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("1.0", root.GetProperty("version").GetString());
        Assert.Equal(3, root.GetProperty("settings").EnumerateObject().Count());
        Assert.Equal("avex.dark", root.GetProperty("settings").GetProperty("theme.id").GetString());
        Assert.NotNull(root.GetProperty("hash").GetString());
    }

    [Fact]
    public void ReadBackup_ReturnsSettings_WhenValid()
    {
        _settings.Set("theme.id", "avex.light");
        _settings.Set("theme.mode", "light");

        var info = _service.CreateBackup();
        var restored = _service.ReadBackup(info.FilePath);

        Assert.Equal("avex.light", restored["theme.id"]);
        Assert.Equal("light", restored["theme.mode"]);
    }

    [Fact]
    public void ReadBackup_ThrowsOnTamperedFile()
    {
        _settings.Set("theme.id", "avex.dark");
        var info = _service.CreateBackup();

        // Tamper with the file
        var json = File.ReadAllText(info.FilePath);
        var tampered = json.Replace("avex.dark", "avex.hacked");
        File.WriteAllText(info.FilePath, tampered);

        Assert.Throws<InvalidOperationException>(() => _service.ReadBackup(info.FilePath));
    }

    [Fact]
    public void ReadBackup_ThrowsOnMissingFile()
    {
        Assert.Throws<FileNotFoundException>(() =>
            _service.ReadBackup(Path.Combine(_testDir, "nonexistent.json")));
    }

    [Fact]
    public void RestoreBackup_WritesSettingsToStore()
    {
        _settings.Set("theme.id", "avex.dark");
        _settings.Set("theme.mode", "dark");
        var info = _service.CreateBackup();

        // Change settings
        _settings.Set("theme.id", "avex.light");
        _settings.Set("theme.mode", "light");

        // Restore
        var keys = _service.RestoreBackup(info.FilePath);

        Assert.Contains("theme.id", keys);
        Assert.Contains("theme.mode", keys);
        Assert.Equal("avex.dark", _settings.Get("theme.id"));
        Assert.Equal("dark", _settings.Get("theme.mode"));
    }

    [Fact]
    public void ListBackups_ReturnsAllBackups_NewestFirst()
    {
        _settings.Set("key", "val1");
        _clock.Advance(TimeSpan.FromSeconds(1));
        _service.CreateBackup();

        _settings.Set("key", "val2");
        _clock.Advance(TimeSpan.FromSeconds(1));
        _service.CreateBackup();

        var list = _service.ListBackups();

        Assert.Equal(2, list.Count);
        Assert.True(list[0].CreatedUtc > list[1].CreatedUtc);
    }

    [Fact]
    public void DeleteBackup_RemovesFile()
    {
        _settings.Set("key", "val");
        var info = _service.CreateBackup();
        Assert.True(File.Exists(info.FilePath));

        var deleted = _service.DeleteBackup(info.FilePath);

        Assert.True(deleted);
        Assert.False(File.Exists(info.FilePath));
    }

    [Fact]
    public void DeleteBackup_ReturnsFalse_WhenFileMissing()
    {
        var deleted = _service.DeleteBackup(Path.Combine(_testDir, "nope.json"));
        Assert.False(deleted);
    }

    [Fact]
    public void EnforceRetention_KeepsOnlyLast10()
    {
        for (int i = 0; i < 15; i++)
        {
            _settings.Set("key", $"val{i}");
            _clock.Advance(TimeSpan.FromSeconds(1));
            _service.CreateBackup();
        }

        var remaining = _service.ListBackups();
        Assert.Equal(10, remaining.Count);

        var files = Directory.GetFiles(_testDir, "avex-settings-backup-*.json");
        Assert.Equal(10, files.Length);
    }

    [Fact]
    public void CreateBackup_HandlesEmptySettings()
    {
        var info = _service.CreateBackup();

        Assert.Equal(0, info.SettingCount);
        var restored = _service.ReadBackup(info.FilePath);
        Assert.Empty(restored);
    }

    [Fact]
    public void CreateBackup_HandlesDuplicateTimestamps()
    {
        _settings.Set("key", "val1");
        var info1 = _service.CreateBackup();

        _settings.Set("key", "val2");
        var info2 = _service.CreateBackup(); // Same timestamp

        Assert.NotEqual(info1.FileName, info2.FileName);
        Assert.True(File.Exists(info1.FilePath));
        Assert.True(File.Exists(info2.FilePath));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, recursive: true);
        }
        catch { }
    }
}

// --- Test doubles ---

internal sealed class InMemorySettingsStore : ISettingsStore
{
    private readonly Dictionary<string, string> _data = new();

    public string? Get(string key) => _data.TryGetValue(key, out var v) ? v : null;

    public void Set(string key, string value) => _data[key] = value;

    public IReadOnlyDictionary<string, string> GetAll() => new Dictionary<string, string>(_data);

    public void SetMany(IReadOnlyDictionary<string, string> values)
    {
        foreach (var (k, v) in values) _data[k] = v;
    }
}

internal sealed class FakeClock : IClock
{
    private DateTimeOffset _now;

    public FakeClock(DateTimeOffset initial) => _now = initial;

    public DateTimeOffset UtcNow => _now;

    public void Advance(TimeSpan delta) => _now += delta;
}

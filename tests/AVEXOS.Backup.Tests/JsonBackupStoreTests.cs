using System.Text.Json;
using AVEXOS.Core;
using AVEXOS.Security;
using Xunit;

namespace AVEXOS.Backup.Tests;

public class JsonBackupStoreTests : IDisposable
{
    private readonly string _testDir;
    private readonly JsonBackupStore _store;

    public JsonBackupStoreTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"avex-backup-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);

        var policy = new ProtectedPathPolicy(new[] { _testDir });
        var guard = new FileSystemGuard(policy);
        var clock = new SystemClock();
        _store = new JsonBackupStore(guard, clock);

        AvexPaths.OverrideBackupDirForTests(_testDir);
    }

    [Fact]
    public void Load_Rejects_Tampered_Backup()
    {
        var snapshot = _store.Create("wallpaper", new TestPayload("original-value"), "test backup");

        var filePath = Path.Combine(_testDir, $"{snapshot.Id}.json");
        var json = File.ReadAllText(filePath);
        var doc = JsonDocument.Parse(json);
        var modified = json.Replace("original-value", "tampered-value");
        File.WriteAllText(filePath, modified);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            _store.Load<TestPayload>(snapshot.Id));
        Assert.Contains("integrity", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_Rejects_Unhashed_Backup()
    {
        var snapshot = _store.Create("wallpaper", new TestPayload("value"), "test backup");
        var filePath = Path.Combine(_testDir, $"{snapshot.Id}.json");

        var json = File.ReadAllText(filePath);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var modified = JsonSerializer.Serialize(new
        {
            id = root.GetProperty("id").GetString(),
            targetId = root.GetProperty("targetId").GetString(),
            createdUtc = root.GetProperty("createdUtc").GetDateTimeOffset(),
            description = root.GetProperty("description").GetString(),
            payload = JsonSerializer.Deserialize<TestPayload>(root.GetProperty("payload").GetRawText())
        });
        File.WriteAllText(filePath, modified);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            _store.Load<TestPayload>(snapshot.Id));
        Assert.Contains("unhashed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_Accepts_Valid_Hashed_Backup()
    {
        var snapshot = _store.Create("wallpaper", new TestPayload("correct-value"), "test backup");

        var loaded = _store.Load<TestPayload>(snapshot.Id);

        Assert.NotNull(loaded);
        Assert.Equal("correct-value", loaded!.Value);
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

public record TestPayload(string Value);

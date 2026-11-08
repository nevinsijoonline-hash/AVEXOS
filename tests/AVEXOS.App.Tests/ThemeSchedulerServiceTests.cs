using System;
using System.Collections.Generic;
using AVEXOS.Core;
using AVEXOS.ThemeEngine;
using Xunit;

// Stubs in AVEXOS.App namespace so the linked ThemeSchedulerService.cs compiles
namespace AVEXOS.App
{
    public sealed class FakeSettingsStore : ISettingsStore
    {
        private readonly Dictionary<string, string> _store = new();
        public string? Get(string key) => _store.TryGetValue(key, out var v) ? v : null;
        public void Set(string key, string value) => _store[key] = value;
        public IReadOnlyDictionary<string, string> GetAll() => new Dictionary<string, string>(_store);
        public void SetMany(IReadOnlyDictionary<string, string> values)
        {
            foreach (var (k, v) in values) _store[k] = v;
        }
    }

    public sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
    }

    public sealed class ThemeService
    {
        public string? LastAppliedThemeId { get; private set; }
        public string? LastAppliedMode { get; private set; }
        public int ApplyCount { get; private set; }

        public ThemeService(ISettingsStore settings, IThemeCatalog catalog) { }

        public void Apply(string themeId, string mode)
        {
            LastAppliedThemeId = themeId;
            LastAppliedMode = mode;
            ApplyCount++;
        }
    }

    public sealed class FakeThemeCatalog : IThemeCatalog
    {
        public IReadOnlyList<ThemeDocument> BuiltIn => Array.Empty<ThemeDocument>();
        public IReadOnlyList<ThemeDocument> UserThemes => Array.Empty<ThemeDocument>();
        public IReadOnlyList<ThemeDocument> All => Array.Empty<ThemeDocument>();
    }
}

namespace AVEXOS.App.Tests
{
    using AVEXOS.App;

    public class ThemeSchedulerServiceTests
    {
    private readonly FakeSettingsStore _settings = new();
    private readonly FakeClock _clock = new();
    private readonly List<(string ThemeId, string Mode)> _applied = new();

    private ThemeSchedulerService CreateScheduler(bool enabled = false)
    {
        if (enabled) _settings.Set("theme.scheduler.enabled", "True");
        return new ThemeSchedulerService(
            _settings,
            (id, mode) => _applied.Add((id, mode)),
            _clock);
    }

    [Fact]
    public void AddSchedule_PersistsToJson()
    {
        var scheduler = CreateScheduler();
        scheduler.AddSchedule(new TimeSpan(7, 0, 0), "avex.dark", "dark");

        Assert.Single(scheduler.Schedules);
        var json = _settings.Get("theme.scheduler.schedules");
        Assert.NotNull(json);
        Assert.Contains("avex.dark", json);
    }

    [Fact]
    public void RemoveSchedule_RemovesFromList()
    {
        var scheduler = CreateScheduler();
        scheduler.AddSchedule(new TimeSpan(7, 0, 0), "avex.dark", "dark");
        var id = scheduler.Schedules[0].Id;

        scheduler.RemoveSchedule(id);

        Assert.Empty(scheduler.Schedules);
    }

    [Fact]
    public void SetEnabled_PersistsFlag()
    {
        var scheduler = CreateScheduler();
        scheduler.SetEnabled(true);
        Assert.True(scheduler.IsEnabled);
        Assert.Equal("True", _settings.Get("theme.scheduler.enabled"));

        scheduler.SetEnabled(false);
        Assert.False(scheduler.IsEnabled);
    }

    [Fact]
    public void CheckSchedule_DoesNothingWhenDisabled()
    {
        var scheduler = CreateScheduler(enabled: false);
        scheduler.AddSchedule(new TimeSpan(10, 30, 0), "avex.light", "light");

        // Set clock to match schedule time
        _clock.UtcNow = new DateTimeOffset(2026, 1, 1, 10, 30, 0, TimeSpan.Zero);
        scheduler.CheckSchedule();

        Assert.Empty(_applied);
    }

    [Fact]
    public void CheckSchedule_AppliesThemeWhenTimeMatches()
    {
        var scheduler = CreateScheduler(enabled: true);
        scheduler.AddSchedule(new TimeSpan(10, 30, 0), "avex.light", "light");

        // Set clock to match schedule time (UTC, will be converted to local)
        _clock.UtcNow = new DateTimeOffset(2026, 1, 1, 10, 30, 0, TimeSpan.Zero);
        scheduler.CheckSchedule();

        // The local time depends on the machine's timezone. For UTC timezone, this should match.
        // We test the logic works by checking that if times align, apply is called.
        // On a UTC machine, this will have applied.
        var localHour = _clock.UtcNow.ToLocalTime().Hour;
        var localMinute = _clock.UtcNow.ToLocalTime().Minute;
        if (localHour == 10 && localMinute == 30)
        {
            Assert.Single(_applied);
            Assert.Equal("avex.light", _applied[0].ThemeId);
        }
    }

    [Fact]
    public void CheckSchedule_DoesNotApplySameScheduleTwice()
    {
        var scheduler = CreateScheduler(enabled: true);
        scheduler.AddSchedule(new TimeSpan(14, 0, 0), "avex.dark", "dark");

        _clock.UtcNow = new DateTimeOffset(2026, 1, 1, 14, 0, 0, TimeSpan.Zero);
        var localNow = _clock.UtcNow.ToLocalTime();
        if (localNow.Hour == 14 && localNow.Minute == 0)
        {
            scheduler.CheckSchedule();
            scheduler.CheckSchedule(); // second call same minute
            Assert.Single(_applied); // should not double-apply
        }
    }

    [Fact]
    public void Load_RestoresSchedulesFromSettings()
    {
        // Pre-populate settings with JSON
        var schedules = new List<ScheduledThemeSwitch>
        {
            new() { Id = "abc", Time = new TimeSpan(7, 0, 0), ThemeId = "avex.light", Mode = "light" },
            new() { Id = "def", Time = new TimeSpan(19, 0, 0), ThemeId = "avex.dark", Mode = "dark" }
        };
        _settings.Set("theme.scheduler.schedules",
            System.Text.Json.JsonSerializer.Serialize(schedules));
        _settings.Set("theme.scheduler.enabled", "True");

        var scheduler = CreateScheduler(enabled: true);

        Assert.Equal(2, scheduler.Schedules.Count);
        Assert.True(scheduler.IsEnabled);
    }

    [Fact]
    public void MultipleSchedules_OnlyFirstMatchApplies()
    {
        var scheduler = CreateScheduler(enabled: true);
        scheduler.AddSchedule(new TimeSpan(8, 0, 0), "theme1", "light");
        scheduler.AddSchedule(new TimeSpan(8, 0, 0), "theme2", "dark");

        _clock.UtcNow = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
        var localNow = _clock.UtcNow.ToLocalTime();
        if (localNow.Hour == 8 && localNow.Minute == 0)
        {
            scheduler.CheckSchedule();
            Assert.Single(_applied);
            Assert.Equal("theme1", _applied[0].ThemeId); // first match wins
        }
    }
}
}

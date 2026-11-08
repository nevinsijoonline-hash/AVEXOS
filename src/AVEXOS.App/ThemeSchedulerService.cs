using System.Text.Json;
using AVEXOS.Core;

namespace AVEXOS.App;

public sealed class ScheduledThemeSwitch
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public TimeSpan Time { get; set; }
    public string ThemeId { get; set; } = "";
    public string Mode { get; set; } = "dark";
}

public sealed class ThemeSchedulerService
{
    private const string SchedulesKey = "theme.scheduler.schedules";
    private const string EnabledKey = "theme.scheduler.enabled";

    private readonly ISettingsStore _settings;
    private readonly Action<string, string> _applyTheme;
    private readonly IClock _clock;
    private readonly List<ScheduledThemeSwitch> _schedules = new();
    private System.Threading.Timer? _timer;
    private bool _enabled;
    internal string? LastAppliedId;

    public IReadOnlyList<ScheduledThemeSwitch> Schedules => _schedules;
    public bool IsEnabled => _enabled;

    public ThemeSchedulerService(ISettingsStore settings, ThemeService themeService, IClock clock)
        : this(settings, (id, mode) => themeService.Apply(id, mode), clock)
    {
    }

    internal ThemeSchedulerService(ISettingsStore settings, Action<string, string> applyTheme, IClock clock)
    {
        _settings = settings;
        _applyTheme = applyTheme;
        _clock = clock;
        Load();
    }

    public void Start()
    {
        if (_timer is not null) return;
        CheckSchedule();
        _timer = new System.Threading.Timer(_ => CheckSchedule(), null,
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    public void SetEnabled(bool enabled)
    {
        _enabled = enabled;
        _settings.Set(EnabledKey, enabled.ToString());
        if (enabled) Start(); else Stop();
    }

    public void AddSchedule(TimeSpan time, string themeId, string mode)
    {
        var entry = new ScheduledThemeSwitch { Time = time, ThemeId = themeId, Mode = mode };
        _schedules.Add(entry);
        Save();
    }

    public void RemoveSchedule(string id)
    {
        _schedules.RemoveAll(s => s.Id == id);
        Save();
    }

    public void CheckSchedule()
    {
        if (!_enabled) return;

        var now = _clock.UtcNow.ToLocalTime();
        var currentTime = new TimeSpan(now.Hour, now.Minute, 0);

        foreach (var schedule in _schedules)
        {
            if (schedule.Time.Hours == currentTime.Hours &&
                schedule.Time.Minutes == currentTime.Minutes &&
                schedule.Id != LastAppliedId)
            {
                _applyTheme(schedule.ThemeId, schedule.Mode);
                LastAppliedId = schedule.Id;
                break;
            }
        }

        ResetIfPastLastApplied(currentTime);
    }

    private void ResetIfPastLastApplied(TimeSpan currentTime)
    {
        if (LastAppliedId is null) return;
        var last = _schedules.FirstOrDefault(s => s.Id == LastAppliedId);
        if (last is null) { LastAppliedId = null; return; }

        var lastMinutes = last.Time.Hours * 60 + last.Time.Minutes;
        var currentMinutes = currentTime.Hours * 60 + currentTime.Minutes;
        if (currentMinutes > lastMinutes + 1)
            LastAppliedId = null;
    }

    private void Load()
    {
        var enabledStr = _settings.Get(EnabledKey);
        _enabled = string.Equals(enabledStr, "True", StringComparison.OrdinalIgnoreCase);

        var json = _settings.Get(SchedulesKey);
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                var list = JsonSerializer.Deserialize<List<ScheduledThemeSwitch>>(json);
                if (list is not null) _schedules.AddRange(list);
            }
            catch { /* corrupt data, start fresh */ }
        }
    }

    private void Save()
    {
        var json = JsonSerializer.Serialize(_schedules);
        _settings.Set(SchedulesKey, json);
    }
}

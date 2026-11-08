using AVEXOS.Core;
using AVEXOS.WindowsIntegration;
using Xunit;

namespace AVEXOS.WindowsIntegration.Tests;

public class AutoBrightnessServiceTests
{
    private static AutoBrightnessService CreateService(
        ISettingsStore? settings = null,
        IClock? clock = null,
        Action<int>? brightnessApplier = null)
    {
        settings ??= new InMemorySettings();
        clock ??= new FakeClock { UtcNow = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero) };
        brightnessApplier ??= _ => { };
        return new AutoBrightnessService(settings, clock, brightnessApplier);
    }

    // ---- 1. Default config ----

    [Fact]
    public void GetConfig_Returns_Default_Config_When_Nothing_Saved()
    {
        var service = CreateService();
        var config = service.GetConfig();

        Assert.False(config.IsEnabled);
        Assert.Equal(80, config.DayBrightness);
        Assert.Equal(30, config.NightBrightness);
        Assert.Equal(new TimeSpan(7, 0, 0), config.DayStartTime);
        Assert.Equal(new TimeSpan(19, 0, 0), config.NightStartTime);
        Assert.Equal(30, config.TransitionMinutes);
    }

    // ---- 2. Save and retrieve config ----

    [Fact]
    public void SaveConfig_Persists_And_Reloads()
    {
        var settings = new InMemorySettings();
        var service = CreateService(settings: settings);

        var config = new AutoBrightnessConfig
        {
            IsEnabled = true,
            DayBrightness = 90,
            NightBrightness = 20,
            DayStartTime = new TimeSpan(6, 30, 0),
            NightStartTime = new TimeSpan(20, 0, 0),
            TransitionMinutes = 45
        };
        service.SaveConfig(config);

        // Create a new service reading from same store
        var service2 = CreateService(settings: settings);
        var loaded = service2.GetConfig();

        Assert.True(loaded.IsEnabled);
        Assert.Equal(90, loaded.DayBrightness);
        Assert.Equal(20, loaded.NightBrightness);
        Assert.Equal(new TimeSpan(6, 30, 0), loaded.DayStartTime);
        Assert.Equal(new TimeSpan(20, 0, 0), loaded.NightStartTime);
        Assert.Equal(45, loaded.TransitionMinutes);
    }

    // ---- 3. Enable / Disable ----

    [Fact]
    public void Enable_Sets_IsEnabled_True()
    {
        var service = CreateService();
        Assert.False(service.IsEnabled);

        service.Enable();
        Assert.True(service.IsEnabled);
    }

    [Fact]
    public void Disable_Sets_IsEnabled_False()
    {
        var service = CreateService();
        service.Enable();
        Assert.True(service.IsEnabled);

        service.Disable();
        Assert.False(service.IsEnabled);
    }

    // ---- 4. Brightness calculation - full day ----

    [Fact]
    public void CalculateBrightness_Returns_DayBrightness_During_Midday()
    {
        var service = CreateService();
        service.SaveConfig(new AutoBrightnessConfig
        {
            DayBrightness = 80,
            NightBrightness = 30,
            DayStartTime = new TimeSpan(7, 0, 0),
            NightStartTime = new TimeSpan(19, 0, 0),
            TransitionMinutes = 30
        });

        // 12:00 noon - solidly in day period
        var noon = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.FromHours(2));
        var brightness = service.CalculateBrightness(noon);

        Assert.Equal(80, brightness);
    }

    // ---- 5. Brightness calculation - full night ----

    [Fact]
    public void CalculateBrightness_Returns_NightBrightness_During_Midnight()
    {
        var service = CreateService();
        service.SaveConfig(new AutoBrightnessConfig
        {
            DayBrightness = 80,
            NightBrightness = 30,
            DayStartTime = new TimeSpan(7, 0, 0),
            NightStartTime = new TimeSpan(19, 0, 0),
            TransitionMinutes = 30
        });

        // 23:00 - solidly in night period
        var night = new DateTimeOffset(2026, 6, 15, 23, 0, 0, TimeSpan.FromHours(2));
        var brightness = service.CalculateBrightness(night);

        Assert.Equal(30, brightness);
    }

    // ---- 6. Brightness calculation - transition midpoint ----

    [Fact]
    public void CalculateBrightness_Interpolates_During_DayToNight_Transition()
    {
        var service = CreateService();
        service.SaveConfig(new AutoBrightnessConfig
        {
            DayBrightness = 80,
            NightBrightness = 20,
            DayStartTime = new TimeSpan(7, 0, 0),
            NightStartTime = new TimeSpan(19, 0, 0),
            TransitionMinutes = 60 // 19:00 to 20:00
        });

        // 19:30 - midpoint of transition
        var midTransition = new DateTimeOffset(2026, 6, 15, 19, 30, 0, TimeSpan.FromHours(2));
        var brightness = service.CalculateBrightness(midTransition);

        // Should be ~50 (lerp from 80 to 20 at 50%)
        Assert.InRange(brightness, 45, 55);
    }

    // ---- 7. Brightness calculation - start of transition ----

    [Fact]
    public void CalculateBrightness_At_Transition_Start_Returns_DayBrightness()
    {
        var service = CreateService();
        service.SaveConfig(new AutoBrightnessConfig
        {
            DayBrightness = 80,
            NightBrightness = 20,
            DayStartTime = new TimeSpan(7, 0, 0),
            NightStartTime = new TimeSpan(19, 0, 0),
            TransitionMinutes = 30
        });

        // Exactly at 19:00 - start of transition
        var start = new DateTimeOffset(2026, 6, 15, 19, 0, 0, TimeSpan.FromHours(2));
        var brightness = service.CalculateBrightness(start);

        // At the very start, should still be day brightness
        Assert.Equal(80, brightness);
    }

    // ---- 8. ApplyBrightness calls the applier ----

    [Fact]
    public void ApplyBrightness_Calls_Applier_With_Clamped_Value()
    {
        int appliedLevel = -1;
        var service = CreateService(brightnessApplier: level => appliedLevel = level);

        service.ApplyBrightness(50);
        Assert.Equal(50, appliedLevel);

        // Test clamping above 100
        service.ApplyBrightness(150);
        Assert.Equal(100, appliedLevel);

        // Test clamping below 0
        service.ApplyBrightness(-10);
        Assert.Equal(0, appliedLevel);
    }

    // ---- 9. GetCurrentBrightness tracks last applied ----

    [Fact]
    public void GetCurrentBrightness_Returns_Last_Applied_Level()
    {
        var service = CreateService();

        service.ApplyBrightness(65);
        Assert.Equal(65, service.GetCurrentBrightness());

        service.ApplyBrightness(30);
        Assert.Equal(30, service.GetCurrentBrightness());
    }

    // ---- 10. Validate clamps config values ----

    [Fact]
    public void Validate_Clamps_Brightness_To_0_100()
    {
        var config = new AutoBrightnessConfig
        {
            DayBrightness = 150,
            NightBrightness = -20,
            TransitionMinutes = 120
        };
        config.Validate();

        Assert.Equal(100, config.DayBrightness);
        Assert.Equal(0, config.NightBrightness);
        Assert.Equal(60, config.TransitionMinutes);
    }

    // ---- 11. Tick applies brightness when enabled ----

    [Fact]
    public void Tick_Applies_Brightness_When_Enabled()
    {
        int appliedLevel = -1;
        var clock = new FakeClock { UtcNow = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.FromHours(2)) };
        var service = CreateService(clock: clock, brightnessApplier: level => appliedLevel = level);
        service.Enable();

        service.Tick();

        Assert.Equal(80, appliedLevel); // default day brightness
    }

    // ---- 12. Tick does nothing when disabled ----

    [Fact]
    public void Tick_Does_Nothing_When_Disabled()
    {
        int appliedLevel = -1;
        var clock = new FakeClock { UtcNow = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.FromHours(2)) };
        var service = CreateService(clock: clock, brightnessApplier: level => appliedLevel = level);
        // Don't enable

        service.Tick();

        Assert.Equal(-1, appliedLevel); // never called
    }

    // ---- 13. Early morning before day start is night ----

    [Fact]
    public void CalculateBrightness_Returns_NightBrightness_Before_DayStart()
    {
        var service = CreateService();
        service.SaveConfig(new AutoBrightnessConfig
        {
            DayBrightness = 80,
            NightBrightness = 30,
            DayStartTime = new TimeSpan(7, 0, 0),
            NightStartTime = new TimeSpan(19, 0, 0),
            TransitionMinutes = 30
        });

        // 05:00 - before day starts
        var earlyMorning = new DateTimeOffset(2026, 6, 15, 5, 0, 0, TimeSpan.FromHours(2));
        var brightness = service.CalculateBrightness(earlyMorning);

        Assert.Equal(30, brightness);
    }

    // ---- 14. Dispose stops the timer ----

    [Fact]
    public void Dispose_Stops_Timer_And_Does_Not_Throw()
    {
        var service = CreateService();
        service.Enable();
        service.Start();

        // Should not throw
        service.Dispose();

        // Double dispose should also not throw
        service.Dispose();
    }

    // ---- 15. Transition at end returns night brightness ----

    [Fact]
    public void CalculateBrightness_At_Transition_End_Returns_NightBrightness()
    {
        var service = CreateService();
        service.SaveConfig(new AutoBrightnessConfig
        {
            DayBrightness = 80,
            NightBrightness = 20,
            DayStartTime = new TimeSpan(7, 0, 0),
            NightStartTime = new TimeSpan(19, 0, 0),
            TransitionMinutes = 30 // 19:00 to 19:30
        });

        // 19:30 - end of transition
        var end = new DateTimeOffset(2026, 6, 15, 19, 30, 0, TimeSpan.FromHours(2));
        var brightness = service.CalculateBrightness(end);

        Assert.Equal(20, brightness);
    }

    // ---- 16. Zero transition minutes = instant switch ----

    [Fact]
    public void CalculateBrightness_Zero_Transition_Switches_Instantly()
    {
        var service = CreateService();
        service.SaveConfig(new AutoBrightnessConfig
        {
            DayBrightness = 100,
            NightBrightness = 10,
            DayStartTime = new TimeSpan(7, 0, 0),
            NightStartTime = new TimeSpan(19, 0, 0),
            TransitionMinutes = 0
        });

        // 19:00 exactly - should be night brightness immediately
        var atNight = new DateTimeOffset(2026, 6, 15, 19, 0, 0, TimeSpan.FromHours(2));
        Assert.Equal(10, service.CalculateBrightness(atNight));

        // 18:59 - should be day brightness
        var beforeNight = new DateTimeOffset(2026, 6, 15, 18, 59, 0, TimeSpan.FromHours(2));
        Assert.Equal(100, service.CalculateBrightness(beforeNight));
    }
}

// ---- Test doubles ----


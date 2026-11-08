using System.Text.Json;

namespace AVEXOS.Core;

/// <summary>
/// Manages workspace profiles: CRUD operations, persistence to JSON files,
/// and applying wallpaper + theme when a profile is activated.
/// </summary>
public sealed class WorkspaceProfileService : IWorkspaceProfileService
{
    private readonly IWallpaperService _wallpaper;
    private readonly IThemeService _theme;

    public WorkspaceProfileService(IWallpaperService wallpaper, IThemeService theme)
    {
        _wallpaper = wallpaper;
        _theme = theme;
    }

    public IReadOnlyList<WorkspaceProfile> GetAll()
    {
        var dir = AvexPaths.ProfilesDir;
        if (!Directory.Exists(dir))
            return Array.Empty<WorkspaceProfile>();

        var profiles = new List<WorkspaceProfile>();
        foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var profile = JsonSerializer.Deserialize<WorkspaceProfile>(json, AvexJson.Options);
                if (profile is not null)
                    profiles.Add(profile);
            }
            catch
            {
                // Skip corrupt profile files
            }
        }
        return profiles;
    }

    public WorkspaceProfile? Get(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        var filePath = ProfilePath(id);
        if (!File.Exists(filePath))
            return null;

        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<WorkspaceProfile>(json, AvexJson.Options);
    }

    public void Save(WorkspaceProfile profile)
    {
        Validate(profile);

        // If this profile is default, clear default on all others
        if (profile.IsDefault)
            ClearOtherDefaults(profile.Id);

        var filePath = ProfilePath(profile.Id);
        var json = JsonSerializer.Serialize(profile, AvexJson.Options);
        Directory.CreateDirectory(AvexPaths.ProfilesDir);
        File.WriteAllText(filePath, json);
    }

    public void Delete(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        var filePath = ProfilePath(id);
        if (!File.Exists(filePath))
            return;

        File.Delete(filePath);
    }

    public void Apply(string id)
    {
        var profile = Get(id) ?? throw new KeyNotFoundException($"Profile '{id}' not found.");

        // Apply wallpaper to all monitors
        var appearance = new AppearanceState(profile.WallpaperPath, 10, false);
        _wallpaper.ApplyToAllMonitors(appearance);

        // Apply theme
        _theme.Apply(profile.ThemeId, profile.ThemeMode);
    }

    public void SetDefault(string id)
    {
        var profile = Get(id) ?? throw new KeyNotFoundException($"Profile '{id}' not found.");

        ClearOtherDefaults(id);

        var updated = profile with { IsDefault = true };
        Save(updated);
    }

    private static void Validate(WorkspaceProfile profile)
    {
        ArgumentException.ThrowIfNullOrEmpty(profile.Id);
        ArgumentException.ThrowIfNullOrEmpty(profile.Name);
        ArgumentException.ThrowIfNullOrEmpty(profile.WallpaperPath);
        ArgumentException.ThrowIfNullOrEmpty(profile.ThemeId);
        ArgumentException.ThrowIfNullOrEmpty(profile.ThemeMode);
    }

    private void ClearOtherDefaults(string excludeId)
    {
        foreach (var p in GetAll())
        {
            if (p.IsDefault && p.Id != excludeId)
            {
                var updated = p with { IsDefault = false };
                var json = JsonSerializer.Serialize(updated, AvexJson.Options);
                File.WriteAllText(ProfilePath(updated.Id), json);
            }
        }
    }

    private static string ProfilePath(string id) =>
        Path.Combine(AvexPaths.ProfilesDir, $"{id}.json");
}

using System.Text.Json.Serialization;

namespace AVEXOS.ThemeEngine;

public sealed class ThemeDocument
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string Mode { get; set; } = "";
    public string Category { get; set; } = "";
    public string Author { get; set; } = "";
    public string Description { get; set; } = "";
    public Dictionary<string, string> Color { get; set; } = new();
    public Dictionary<string, int> Radius { get; set; } = new();
    public Dictionary<string, int> Space { get; set; } = new();
    public Dictionary<string, int> Motion { get; set; } = new();
    public string FontFamily { get; set; } = "";
    public Dictionary<string, int> Type { get; set; } = new();
    public Dictionary<string, ThemeTarget> Targets { get; set; } = new();
}

public sealed class ThemeTarget
{
    public string Level { get; set; } = "";
    public string? Note { get; set; }
}

public sealed record ValidationResult(bool IsValid, IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings);

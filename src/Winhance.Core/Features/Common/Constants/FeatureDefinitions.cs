using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Constants;

public static class FeatureDefinitions
{
    public static readonly IReadOnlyList<FeatureDefinition> All = new List<FeatureDefinition>()
    {
        new(FeatureIds.WindowsTheme, "Windows Theme", "Customize", "WindowsThemeIconSymbol"),
        new(FeatureIds.Taskbar, "Taskbar", "Customize", "TaskbarIconSymbol"),
        new(FeatureIds.StartMenu, "Start Menu", "Customize", "StartMenuIconSymbol"),
        new(FeatureIds.Explorer, "Explorer", "Customize", "ExplorerIconSymbol"),
        new(FeatureIds.ContextMenu, "Context Menu", "Customize", "ContextMenuIconSymbol"),
        new(FeatureIds.TimeRegionLanguage, "Time, region and language", "Customize", "TimeRegionLanguageIconSymbol"),

        new(FeatureIds.Privacy, "Privacy & Security", "Optimize", "PrivacyIconSymbol"),
        new(FeatureIds.Power, "Power", "Optimize", "PowerIconSymbol"),
        new(FeatureIds.GamingPerformance, "Gaming & Performance", "Optimize", "GamingIconSymbol"),
        new(FeatureIds.Update, "Update", "Optimize", "UpdateIconSymbol"),
        new(FeatureIds.Notifications, "Notifications", "Optimize", "NotificationIconSymbol"),
        new(FeatureIds.Sound, "Sound", "Optimize", "SoundIconSymbol"),

        new(FeatureIds.WindowsApps, "Windows Apps", "SoftwareApps", "SoftwareAppsIconSymbol"),
        new(FeatureIds.ExternalApps, "External Apps", "SoftwareApps", "ExternalAppsIconSymbol"),

        // Last so the three summary groups keep the index-per-feature order their sums are pinned on.
        new(FeatureIds.Autounattend, "Autounattend", "Autounattend", "AutounattendIconSymbol")
    };

    public static readonly HashSet<string> OptimizeFeatures = All
        .Where(f => f.Category == "Optimize")
        .Select(f => f.Id)
        .ToHashSet();

    public static readonly HashSet<string> CustomizeFeatures = All
        .Where(f => f.Category == "Customize")
        .Select(f => f.Id)
        .ToHashSet();

    public static readonly HashSet<string> AutounattendFeatures = All
        .Where(f => f.Category == "Autounattend")
        .Select(f => f.Id)
        .ToHashSet();

    public static bool IsSettingsFeature(string featureId) => OptimizeFeatures.Contains(featureId) || CustomizeFeatures.Contains(featureId);

    public static FeatureDefinition? Get(string id) => All.FirstOrDefault(f => f.Id == id);

    public static string CategoryIconKey(string category) => category switch
    {
        "SoftwareApps" => "SoftwareAppsIconSymbol",
        "Optimize" => "OptimizeIconSymbol",
        "Customize" => "CustomizeIconSymbol",
        "Autounattend" => "AutounattendIconSymbol",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "No sidebar icon is mapped for this category."),
    };
}

using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Constants;

public static class FeatureDefinitions
{
    public static readonly IReadOnlyList<FeatureDefinition> All = new List<FeatureDefinition>()
    {
        new(FeatureIds.WindowsTheme, "Windows Theme", "Customize", "WindowsThemeIconPath"),
        new(FeatureIds.Taskbar, "Taskbar", "Customize", "TaskbarIconPath"),
        new(FeatureIds.StartMenu, "Start Menu", "Customize", "StartMenuIconPath"),
        new(FeatureIds.ExplorerCustomization, "Explorer", "Customize", "ExplorerIconPath"),
        new(FeatureIds.TimeRegionLanguage, "Time, region and language", "Customize", "TimeRegionLanguageIconSymbol"),

        new(FeatureIds.Privacy, "Privacy & Security", "Optimize", "PrivacyIconPath"),
        new(FeatureIds.Power, "Power", "Optimize", "PowerIconPath"),
        new(FeatureIds.GamingPerformance, "Gaming & Performance", "Optimize", "GamingIconPath"),
        new(FeatureIds.Update, "Windows Update", "Optimize", "UpdateIconSymbol"),
        new(FeatureIds.Notifications, "Notifications", "Optimize", "NotificationIconPath"),
        new(FeatureIds.Sound, "Sound", "Optimize", "SoundIconSymbol"),

        new(FeatureIds.WindowsApps, "Windows Apps", "SoftwareApps", "WindowsLogoIconPath"),
        new(FeatureIds.ExternalApps, "External Apps", "SoftwareApps", "ExternalAppsIconPath"),

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

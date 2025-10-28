using System.Collections.Generic;

public static class FeatureIds
{
    public const string ExternalApps = "ExternalApps";
    public const string WindowsApps = "WindowsApps";
    public const string WindowsCapabilities = "WindowsCapabilities";
    public const string WindowsOptionalFeatures = "WindowsOptionalFeatures";

    public const string GamingPerformance = "GamingPerformance";
    public const string Notification = "Notification";
    public const string Power = "Power";
    public const string Privacy = "Privacy";
    public const string Sound = "Sound";
    public const string Update = "Update";
    public const string Security = "Security";

    public const string WindowsTheme = "WindowsTheme";
    public const string StartMenu = "StartMenu";
    public const string Taskbar = "Taskbar";
    public const string ExplorerCustomization = "ExplorerCustomization";

    public static readonly HashSet<string> OptimizeFeatures = new()
    {
        Power,
        Privacy,
        GamingPerformance,
        Sound,
        Update,
        Notification
    };

    public static readonly HashSet<string> CustomizeFeatures = new()
    {
        WindowsTheme,
        Taskbar,
        StartMenu,
        ExplorerCustomization
    };

    public static readonly Dictionary<string, string> FeatureDisplayNames = new()
    {
        [Power] = "Power",
        [Privacy] = "Privacy & Security",
        [GamingPerformance] = "Gaming and Performance",
        [Sound] = "Sound",
        [Update] = "Windows Update",
        [Notification] = "Notifications",
        [WindowsTheme] = "Windows Theme",
        [Taskbar] = "Taskbar",
        [StartMenu] = "Start Menu",
        [ExplorerCustomization] = "Explorer"
    };

    public static string GetFeatureGroup(string featureId)
    {
        if (OptimizeFeatures.Contains(featureId)) return "Optimize";
        if (CustomizeFeatures.Contains(featureId)) return "Customize";
        return "Unknown";
    }

    public static string GetDisplayName(string featureId)
    {
        return FeatureDisplayNames.TryGetValue(featureId, out var name) ? name : featureId;
    }
}

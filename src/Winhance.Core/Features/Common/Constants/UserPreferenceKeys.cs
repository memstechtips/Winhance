namespace Winhance.Core.Features.Common.Constants;

public static class UserPreferenceKeys
{
    public const string EnableWindowsVersionFilter = "EnableWindowsVersionFilter";
    public const string DontShowFilterExplanation = "DontShowFilterExplanation";
    public const string WindowWidth = "WindowWidth";
    public const string WindowHeight = "WindowHeight";
    public const string WindowLeft = "WindowLeft";
    public const string WindowTop = "WindowTop";
    public const string WindowMaximized = "WindowMaximized";
    public const string InitialConfigBackupCompleted = "InitialConfigBackupCompleted";
    public const string InitialRestorePointOffered = "InitialRestorePointOffered";
    public const string ShowTechnicalDetails = "ShowTechnicalDetails";
    public const string ShowInfoBadges = "ShowInfoBadges";
    public const string ShowNewBadges = "ShowNewBadges";
    public const string SettingsStateFilter = "SettingsStateFilter";
    public const string HighestSeenAddedInVersion = "HighestSeenAddedInVersion";
    public const string UiZoomFactor = "UiZoomFactor";
    public const string SupportAskedAt = "SupportAskedAt";
    public const string InstallAppsForAllUsers = "InstallAppsForAllUsers";
    public const string AppInstallOptions = "AppInstallOptions";
    public const string CloseToTray = "CloseToTray";
    public const string MinimizeToTray = "MinimizeToTray";
    public const string TrayHintShown = "TrayHintShown";
    public const string AppUpdateCheckFrequency = "AppUpdateCheckFrequency";
    public const string AppUpdateAction = "AppUpdateAction";
    public const string LastAppUpdateCheck = "LastAppUpdateCheck";
    public const string LastNotifiedAppUpdates = "LastNotifiedAppUpdates";
    public const string SoftwareAppsViewMode = "SoftwareAppsViewMode";
    public const string SoftwareAppsSortMode = "SoftwareAppsSortMode";
    public const string SoftwareAppsFilter = "SoftwareAppsFilter";
    public const string ActiveProfile = "ActiveProfile";
    public const string ActiveProfileFailed = "ActiveProfileFailed";

    // Followed by the profile id; holds the Winhance version the reminder was turned off for.
    public const string ProfileReminderOffPrefix = "ProfileReminderOff_";
}

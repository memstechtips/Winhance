using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Services;

// What the tray icon says beyond "Winhance is running". Saved in the user preferences, so the full app and the TrayHost,
// which hand the icon to each other, show the same icon, tooltip and menu.
internal sealed record TrayStatus(int AppUpdates = 0, string? WinhanceVersion = null, bool Attention = false)
{
    internal const string PlainTooltip = "Winhance";

    public TrayBadge Badge => Resolve(Attention, WinhanceVersion, AppUpdates);

    // The most urgent state wins.
    internal static TrayBadge Resolve(bool attention, string? winhanceVersion, int appUpdates) =>
        attention ? TrayBadge.Attention
        : winhanceVersion is not null ? TrayBadge.WinhanceUpdate
        : appUpdates > 0 ? TrayBadge.AppUpdates
        : TrayBadge.None;

    // Null when there is no state to name.
    public string? Text(ILocalizationService localization) => Badge switch
    {
        TrayBadge.Attention => localization.GetString("Tray_State_Attention"),
        TrayBadge.WinhanceUpdate => localization.GetString("Tray_State_WinhanceUpdate", WinhanceVersion!),
        TrayBadge.AppUpdates => localization.GetString("Tray_State_AppUpdates", AppUpdates),
        _ => null,
    };

    public string Tooltip(ILocalizationService localization) => Text(localization) ?? PlainTooltip;

    // A saved Winhance update the running version already has is dropped.
    public static TrayStatus Load(IUserPreferencesService preferences, VersionInfo current)
    {
        var version = preferences.GetPreference(UserPreferenceKeys.TrayWinhanceUpdate, string.Empty);
        return new(
            preferences.GetPreference(UserPreferenceKeys.TrayAppUpdates, 0),
            version.Length > 0 && VersionInfo.FromTag(version).IsNewerThan(current) ? version : null,
            preferences.GetPreference(UserPreferenceKeys.TrayAttention, false));
    }

    public async Task SaveAsync(IUserPreferencesService preferences)
    {
        await preferences.SetPreferenceAsync(UserPreferenceKeys.TrayAppUpdates, AppUpdates);
        await preferences.SetPreferenceAsync(UserPreferenceKeys.TrayWinhanceUpdate, WinhanceVersion ?? string.Empty);
        await preferences.SetPreferenceAsync(UserPreferenceKeys.TrayAttention, Attention);
    }

    internal static bool IsNewNotice(string? version, string lastNotified) => version is not null && version != lastNotified;

    // Saves the result of a Winhance update check. True when this version has not been announced yet; it never is again.
    public static async Task<bool> RecordWinhanceUpdateAsync(IUserPreferencesService preferences, string? version, VersionInfo current)
    {
        await (Load(preferences, current) with { WinhanceVersion = version }).SaveAsync(preferences);
        if (!IsNewNotice(version, preferences.GetPreference(UserPreferenceKeys.LastNotifiedWinhanceUpdate, string.Empty)))
            return false;

        await preferences.SetPreferenceAsync(UserPreferenceKeys.LastNotifiedWinhanceUpdate, version!);
        return true;
    }

    // WinhanceTheme's names, as ThemeService saves them.
    internal static TrayMenuTheme MenuThemeFor(string themePreference) => themePreference switch
    {
        nameof(WinhanceTheme.DarkNative) => TrayMenuTheme.Dark,
        nameof(WinhanceTheme.LightNative) => TrayMenuTheme.Light,
        _ => TrayMenuTheme.System,
    };
}

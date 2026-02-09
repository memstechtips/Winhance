using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Constants;

/// <summary>
/// Centralized localization string keys to prevent typos and enable refactoring.
/// Use these constants when calling ILocalizationService.GetString(key).
/// </summary>
public static class StringKeys
{
    /// <summary>
    /// Application-level strings (title, branding, etc.)
    /// </summary>
    public static class App
    {
        public const string Title = "App_Title";
        public const string By = "App_By";
    }

    /// <summary>
    /// Feature/Module display names
    /// </summary>
    public static class Features
    {
        public const string Notifications_Name = "Feature_Notifications_Name";
        public const string Power_Name = "Feature_Power_Name";
        public const string Privacy_Name = "Feature_Privacy_Name";
        public const string GamingPerformance_Name = "Feature_GamingPerformance_Name";
        public const string Sound_Name = "Feature_Sound_Name";
        public const string Update_Name = "Feature_Update_Name";
        public const string Explorer_Name = "Feature_Explorer_Name";
        public const string StartMenu_Name = "Feature_StartMenu_Name";
        public const string Taskbar_Name = "Feature_Taskbar_Name";
        public const string WindowsTheme_Name = "Feature_WindowsTheme_Name";
    }

    /// <summary>
    /// Navigation menu items
    /// </summary>
    public static class Navigation
    {
        public const string SoftwareAndApps = "Nav_SoftwareAndApps";
        public const string Optimize = "Nav_Optimize";
        public const string Customize = "Nav_Customize";
        public const string AdvancedTools = "Nav_AdvancedTools";
        public const string More = "Nav_More";
    }

    /// <summary>
    /// Tooltip strings for UI elements
    /// </summary>
    public static class Tooltips
    {
        public const string SaveConfiguration = "Tooltip_SaveConfiguration";
        public const string ImportConfiguration = "Tooltip_ImportConfiguration";
        public const string ToggleTheme = "Tooltip_ToggleTheme";
        public const string Donate = "Tooltip_Donate";
        public const string CheckForUpdates = "Tooltip_CheckForUpdates";
        public const string ViewLogs = "Tooltip_ViewLogs";
        public const string ViewScripts = "Tooltip_ViewScripts";
    }

    /// <summary>
    /// Menu items (More menu, context menus, etc.)
    /// </summary>
    public static class Menu
    {
        public const string CheckForUpdates = "Menu_CheckForUpdates";
        public const string Logs = "Menu_Logs";
        public const string Scripts = "Menu_Scripts";
        public const string Close = "Menu_Close";
    }

    /// <summary>
    /// Dialog titles and messages
    /// </summary>
    public static class Dialogs
    {
        public const string Error = "Dialog_Error";
        public const string Warning = "Dialog_Warning";
        public const string Information = "Dialog_Information";
        public const string Confirmation = "Dialog_Confirmation";
        public const string ConfirmOperation = "Dialog_ConfirmOperation";
        public const string ItemsWillBeInstalled = "Dialog_ItemsWillBeInstalled";
        public const string ItemsWillBeRemoved = "Dialog_ItemsWillBeRemoved";
        public const string ItemsWillBeProcessed = "Dialog_ItemsWillBeProcessed";

        // Power-related dialogs
        public const string CannotDeleteActivePlan_Title = "Dialog_CannotDeleteActivePlan_Title";
        public const string CannotDeleteActivePlan_Message = "Dialog_CannotDeleteActivePlan_Message";
        public const string CannotDeletePlan_Title = "Dialog_CannotDeletePlan_Title";
        public const string CannotDeletePlan_Message = "Dialog_CannotDeletePlan_Message";
        public const string ConfirmDeletePowerPlan_Title = "Dialog_ConfirmDeletePowerPlan_Title";
        public const string ConfirmDeletePowerPlan_Header = "Dialog_ConfirmDeletePowerPlan_Header";
        public const string ConfirmDeletePowerPlan_Message = "Dialog_ConfirmDeletePowerPlan_Message";
        public const string DeletePowerPlanFailed_Title = "Dialog_DeletePowerPlanFailed_Title";
        public const string DeletePowerPlanFailed_Message = "Dialog_DeletePowerPlanFailed_Message";
        public const string PowerPlanError_Title = "Dialog_PowerPlanError_Title";
        public const string PowerPlanError_Message = "Dialog_PowerPlanError_Message";
    }

    /// <summary>
    /// Button labels
    /// </summary>
    public static class Buttons
    {
        public const string OK = "Button_OK";
        public const string Cancel = "Button_Cancel";
        public const string Continue = "Button_Continue";
        public const string Yes = "Button_Yes";
        public const string No = "Button_No";
        public const string Close = "Button_Close";
        public const string Import = "Button_Import";
        public const string Export = "Button_Export";
    }

    /// <summary>
    /// Language display names
    /// </summary>
    public static class Languages
    {
        public static readonly Dictionary<string, string> SupportedLanguages = new()
        {
            { "af", "Afrikaans" },
            { "ar", "العربية (Arabic)" },
            { "en", "English" },
            { "es", "Español (Spanish)" },
            { "fr", "Français (French)" },
            { "de", "Deutsch (German)" },
            { "pt-BR", "Português (Portuguese Brazil)" },
            { "pt", "Português (Portuguese)" },
            { "it", "Italiano (Italian)" },
            { "ru", "Русский (Russian)" },
            { "zh-Hans", "简体中文 (Chinese Simplified)" },
            { "zh-Hant", "繁體中文 (Chinese Traditional)" },
            { "cs", "Čeština (Czech)" },
            { "hi", "हिन्दी (Hindi)" },
            { "hu", "Magyar (Hungarian)" },
            { "ja", "日本語 (Japanese)" },
            { "lt", "Lietuviškai (Lithuanian)" },
            { "lv", "Latviešu (Latvian)" },
            { "nl", "Nederlands (Dutch)" },
            { "nl-BE", "Nederlands (België) (Dutch - Belgium)" },
            { "pl", "Polski (Polish)" },
            { "sv", "Svenska (Swedish)" },
            { "vi", "Tiếng Việt (Vietnamese)" },
            { "uk", "Українська (Ukrainian)" },
            { "el", "Ελληνικά (Greek)" }
        };
    }

    /// <summary>
    /// Setting group names
    /// </summary>
    public static class SettingGroups
    {
        public const string PrivacySecurity = "SettingGroup_PrivacySecurity";
        public const string Security = "SettingGroup_Security";
    }

    /// <summary>
    /// Settings page strings
    /// </summary>
    public static class Settings
    {
        public const string Title = "Settings_Title";
        public const string Language = "Settings_Menu_Language";
        public const string LanguageDescription = "Settings_Language_Description";
        public const string ThemeTitle = "Settings_Theme_Title";
        public const string ThemeDescription = "Tooltip_ToggleTheme";
        public const string BackupRestoreTitle = "Settings_BackupRestore_Title";
        public const string BackupRestoreDescription = "Settings_BackupRestore_Description";
    }

    /// <summary>
    /// Category labels
    /// </summary>
    public static class Categories
    {
        public const string General = "Category_General";
        public const string Configuration = "Category_Configuration";
    }

    /// <summary>
    /// Theme display names
    /// </summary>
    public static class Themes
    {
        public const string System = "Theme_System";
        public const string LightNative = "Theme_LightNative";
        public const string DarkNative = "Theme_DarkNative";
        public const string LegacyWhite = "Theme_LegacyWhite";
        public const string LegacyDark = "Theme_LegacyDark";
    }

    /// <summary>
    /// Helper method to get a setting name key by setting ID
    /// </summary>
    public static string GetSettingNameKey(string settingId) => $"Setting_{settingId}_Name";

    /// <summary>
    /// Helper method to get a setting description key by setting ID
    /// </summary>
    public static string GetSettingDescriptionKey(string settingId) => $"Setting_{settingId}_Description";

    /// <summary>
    /// Helper method to get a setting option key by setting ID and option index
    /// </summary>
    public static string GetSettingOptionKey(string settingId, int optionIndex) => $"Setting_{settingId}_Option_{optionIndex}";

    /// <summary>
    /// Helper method to get a setting custom state key by setting ID
    /// </summary>
    public static string GetSettingCustomStateKey(string settingId) => $"Setting_{settingId}_CustomState";

    /// <summary>
    /// Helper method to get a setting confirmation title key by setting ID
    /// </summary>
    public static string GetSettingConfirmTitleKey(string settingId) => $"Setting_{settingId}_ConfirmTitle";

    /// <summary>
    /// Helper method to get a setting confirmation message key by setting ID
    /// </summary>
    public static string GetSettingConfirmMessageKey(string settingId) => $"Setting_{settingId}_ConfirmMessage";

    /// <summary>
    /// Helper method to get a setting group key by group name
    /// </summary>
    public static string GetSettingGroupKey(string groupName) => $"SettingGroup_{groupName}";

    /// <summary>
    /// Provides resolved localized strings for x:Bind in XAML.
    /// Initialize with ILocalizationService at app startup.
    /// </summary>
    public static class Localized
    {
        private static ILocalizationService? _service;

        /// <summary>
        /// Initialize with the localization service instance.
        /// Must be called before any localized strings are accessed.
        /// </summary>
        public static void Initialize(ILocalizationService service) => _service = service;

        private static string Get(string key) => _service?.GetString(key) ?? key;

        // Application
        public static string App_Title => Get(App.Title);
        public static string App_By => Get(App.By);

        // Navigation
        public static string Nav_SoftwareAndApps => Get(Navigation.SoftwareAndApps);
        public static string Nav_Optimize => Get(Navigation.Optimize);
        public static string Nav_Customize => Get(Navigation.Customize);
        public static string Nav_AdvancedTools => Get(Navigation.AdvancedTools);
        public static string Nav_More => Get(Navigation.More);

        // Features
        public static string Feature_Notifications_Name => Get(Features.Notifications_Name);
        public static string Feature_Power_Name => Get(Features.Power_Name);
        public static string Feature_Privacy_Name => Get(Features.Privacy_Name);
        public static string Feature_GamingPerformance_Name => Get(Features.GamingPerformance_Name);
        public static string Feature_Sound_Name => Get(Features.Sound_Name);
        public static string Feature_Update_Name => Get(Features.Update_Name);
        public static string Feature_Explorer_Name => Get(Features.Explorer_Name);
        public static string Feature_StartMenu_Name => Get(Features.StartMenu_Name);
        public static string Feature_Taskbar_Name => Get(Features.Taskbar_Name);
        public static string Feature_WindowsTheme_Name => Get(Features.WindowsTheme_Name);

        // Tooltips
        public static string Tooltip_SaveConfiguration => Get(Tooltips.SaveConfiguration);
        public static string Tooltip_ImportConfiguration => Get(Tooltips.ImportConfiguration);
        public static string Tooltip_ToggleTheme => Get(Tooltips.ToggleTheme);
        public static string Tooltip_Donate => Get(Tooltips.Donate);
        public static string Tooltip_CheckForUpdates => Get(Tooltips.CheckForUpdates);
        public static string Tooltip_ViewLogs => Get(Tooltips.ViewLogs);
        public static string Tooltip_ViewScripts => Get(Tooltips.ViewScripts);

        // Menu
        public static string Menu_CheckForUpdates => Get(Menu.CheckForUpdates);
        public static string Menu_Logs => Get(Menu.Logs);
        public static string Menu_Scripts => Get(Menu.Scripts);
        public static string Menu_Close => Get(Menu.Close);

        // Dialogs
        public static string Dialog_Error => Get(Dialogs.Error);
        public static string Dialog_Warning => Get(Dialogs.Warning);
        public static string Dialog_Information => Get(Dialogs.Information);
        public static string Dialog_Confirmation => Get(Dialogs.Confirmation);
        public static string Dialog_ConfirmOperation => Get(Dialogs.ConfirmOperation);
        public static string Dialog_ItemsWillBeInstalled => Get(Dialogs.ItemsWillBeInstalled);
        public static string Dialog_ItemsWillBeRemoved => Get(Dialogs.ItemsWillBeRemoved);
        public static string Dialog_ItemsWillBeProcessed => Get(Dialogs.ItemsWillBeProcessed);

        // Buttons
        public static string Button_OK => Get(Buttons.OK);
        public static string Button_Cancel => Get(Buttons.Cancel);
        public static string Button_Continue => Get(Buttons.Continue);
        public static string Button_Yes => Get(Buttons.Yes);
        public static string Button_No => Get(Buttons.No);
        public static string Button_Close => Get(Buttons.Close);
        public static string Button_Import => Get("Button_Import");
        public static string Button_Export => Get("Button_Export");

        // Settings Page
        public static string Settings_Title => Get(Settings.Title);
        public static string Settings_Language => Get(Settings.Language);
        public static string Settings_Language_Description => Get(Settings.LanguageDescription);
        public static string Settings_Theme_Title => Get(Settings.ThemeTitle);
        public static string Settings_Theme_Description => Get(Settings.ThemeDescription);
        public static string Settings_BackupRestore_Title => Get(Settings.BackupRestoreTitle);
        public static string Settings_BackupRestore_Description => Get(Settings.BackupRestoreDescription);

        // Categories
        public static string Category_General => Get(Categories.General);
        public static string Category_Configuration => Get(Categories.Configuration);

        // Themes
        public static string Theme_System => Get(Themes.System);
        public static string Theme_LightNative => Get(Themes.LightNative);
        public static string Theme_DarkNative => Get(Themes.DarkNative);
        public static string Theme_LegacyWhite => Get(Themes.LegacyWhite);
        public static string Theme_LegacyDark => Get(Themes.LegacyDark);

        /// <summary>
        /// Dynamic string getter for keys not pre-defined as properties.
        /// Use this for dynamically generated keys like setting names.
        /// </summary>
        public static string GetString(string key) => Get(key);

        /// <summary>
        /// Dynamic string getter with format arguments.
        /// </summary>
        public static string GetString(string key, params object[] args)
        {
            return _service?.GetString(key, args) ?? key;
        }
    }
}

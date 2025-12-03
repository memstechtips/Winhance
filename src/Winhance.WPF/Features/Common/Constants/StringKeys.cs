namespace Winhance.WPF.Features.Common.Constants
{
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
        }

        /// <summary>
        /// Language display names
        /// </summary>
        public static class Languages
        {
            public const string English = "Language_English";
            public const string Spanish = "Language_Spanish";
            public const string French = "Language_French";
            public const string German = "Language_German";
            public const string Portuguese = "Language_Portuguese";
            public const string Italian = "Language_Italian";
            public const string Russian = "Language_Russian";
            public const string Chinese = "Language_Chinese";
        }

        /// <summary>
        /// Setting group names
        /// </summary>
        public static class SettingGroups
        {
            public const string PrivacySecurity = "SettingGroup_PrivacySecurity";
            public const string Security = "SettingGroup_Security";
            // Add more as needed
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
    }
}

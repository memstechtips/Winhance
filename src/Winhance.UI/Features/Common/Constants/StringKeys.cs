using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Constants;

/// <summary>
/// Centralized localization string keys to prevent typos and enable refactoring.
/// Use these constants when calling ILocalizationService.GetString(key).
/// </summary>
public static class StringKeys
{
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
            { "ko", "한국어 (Korean)" },
            { "lt", "Lietuviškai (Lithuanian)" },
            { "lv", "Latviešu (Latvian)" },
            { "nl", "Nederlands (Dutch)" },
            { "nl-BE", "Nederlands (België) (Dutch - Belgium)" },
            { "pl", "Polski (Polish)" },
            { "sv", "Svenska (Swedish)" },
            { "vi", "Tiếng Việt (Vietnamese)" },
            { "uk", "Українська (Ukrainian)" },
            { "el", "Ελληνικά (Greek)" },
            { "tr", "Türkçe (Turkish)" }
        };
    }

    /// <summary>
    /// Settings page strings
    /// </summary>
    public static class Settings
    {
        public const string Title = "Settings_Title";
        public const string Description = "Settings_Description";
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
    }

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

        // Dialogs
        public static string Dialog_Confirmation => Get("Dialog_Confirmation");

        // Buttons
        public static string Button_OK => Get(Buttons.OK);
        public static string Button_Cancel => Get(Buttons.Cancel);
        public static string Button_Continue => Get(Buttons.Continue);
        public static string Button_Yes => Get(Buttons.Yes);
        public static string Button_No => Get(Buttons.No);
    }
}

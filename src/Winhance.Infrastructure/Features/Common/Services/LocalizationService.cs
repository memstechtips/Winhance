using System.Globalization;
using System.Text.Json;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Services;

/// <summary>
/// Provides localization services by loading strings from JSON language files.
/// Uses the Localization folder in the application's base directory.
/// </summary>
public class LocalizationService : ILocalizationService
{
    private CultureInfo _currentCulture;
    private volatile Dictionary<string, string> _currentStrings;
    private volatile Dictionary<string, string> _fallbackStrings;
    private readonly string _localizationPath;
    private readonly IFileSystemService _fileSystemService;
    private string _currentLanguageCode;

    public event EventHandler? LanguageChanged;

    public LocalizationService(IFileSystemService fileSystemService)
    {
        _fileSystemService = fileSystemService;
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        _localizationPath = _fileSystemService.CombinePath(baseDir, "Localization");

        _currentCulture = CultureInfo.CurrentUICulture;
        _fallbackStrings = LoadLanguageFile("en");
        _currentLanguageCode = ResolveLanguageCode(_currentCulture);
        _currentStrings = LoadLanguageFile(_currentLanguageCode);
    }

    public string CurrentLanguage => _currentLanguageCode;

    public bool IsRightToLeft => _currentCulture.TextInfo.IsRightToLeft;

    public string GetString(string key)
    {
        if (_currentStrings.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value))
        {
            return value;
        }

        if (_fallbackStrings.TryGetValue(key, out var fallbackValue) && !string.IsNullOrEmpty(fallbackValue))
        {
            return fallbackValue;
        }

        return $"[{key}]";
    }

    public string GetString(string key, params object[] args)
    {
        var format = GetString(key);
        try
        {
            return string.Format(format, args);
        }
        catch
        {
            return format;
        }
    }

    public bool SetLanguage(string languageCode)
    {
        try
        {
            var culture = CultureInfo.GetCultureInfo(languageCode);
            _currentCulture = culture;
            _currentLanguageCode = languageCode;

            _currentStrings = LoadLanguageFile(languageCode);

            // Only update the UI culture for resource-loading purposes.
            // Deliberately do NOT change CultureInfo.CurrentCulture — keeping it at
            // InvariantCulture ensures that number formatting throughout the app
            // (including WinUI NumberBox controls) remains locale-independent.
            // Winhance's own localization uses a JSON-based system and does not
            // rely on the thread's CurrentCulture for number/date formatting.
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;

            LanguageChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private string ResolveLanguageCode(CultureInfo culture)
    {
        var availableLanguages = GetAvailableLanguageCodes().ToList();

        if (availableLanguages.Contains(culture.Name))
            return culture.Name;

        if (availableLanguages.Contains(culture.Parent.Name))
            return culture.Parent.Name;

        if (availableLanguages.Contains(culture.TwoLetterISOLanguageName))
            return culture.TwoLetterISOLanguageName;

        return "en";
    }

    private IEnumerable<string> GetAvailableLanguageCodes()
    {
        try
        {
            if (!_fileSystemService.DirectoryExists(_localizationPath))
            {
                return new[] { "en" };
            }

            var jsonFiles = _fileSystemService.GetFiles(_localizationPath, "*.json");
            var languages = jsonFiles
                .Select(_fileSystemService.GetFileNameWithoutExtension)
                .Where(lang => !string.IsNullOrEmpty(lang))
                .OrderBy(lang => lang)
                .ToList();

            if (!languages.Contains("en"))
            {
                languages.Insert(0, "en");
            }

            return languages!;
        }
        catch
        {
            return new[] { "en" };
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<LanguageOption> GetAvailableLanguages()
    {
        try
        {
            var codes = GetAvailableLanguageCodes().ToList();

            var options = new List<LanguageOption>();
            foreach (var code in codes)
            {
                var strings = LoadLanguageFile(code);
                string displayName;

                if (strings.TryGetValue("_Meta_LanguageDisplayName", out var metaName) &&
                    !string.IsNullOrWhiteSpace(metaName))
                {
                    displayName = metaName;
                }
                else
                {
                    try
                    {
                        displayName = CultureInfo.GetCultureInfo(code).NativeName;
                    }
                    catch
                    {
                        displayName = code;
                    }
                }

                options.Add(new LanguageOption(code, displayName));
            }

            // English first, then the rest alphabetically by DisplayName
            var enOption = options.FirstOrDefault(o => o.Code == "en");
            var rest = options
                .Where(o => o.Code != "en")
                .OrderBy(o => o.DisplayName, StringComparer.InvariantCulture)
                .ToList();

            var result = new List<LanguageOption>();
            if (enOption != null)
                result.Add(enOption);
            result.AddRange(rest);

            return result.AsReadOnly();
        }
        catch
        {
            return new List<LanguageOption> { new LanguageOption("en", "English") }.AsReadOnly();
        }
    }

    private Dictionary<string, string> LoadLanguageFile(string languageCode)
    {
        try
        {
            var filePath = _fileSystemService.CombinePath(_localizationPath, $"{languageCode}.json");

            if (!_fileSystemService.FileExists(filePath))
            {
                return new Dictionary<string, string>();
            }

            var json = _fileSystemService.ReadAllText(filePath);
            var dictionary = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

            return dictionary ?? new Dictionary<string, string>();
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }
}

using System.Globalization;
using System.Text.Json;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services;

/// <summary>
/// Provides localization services by loading strings from JSON language files.
/// Uses the Localization folder in the application's base directory.
/// </summary>
public class LocalizationService : ILocalizationService
{
    private CultureInfo _currentCulture;
    private Dictionary<string, string> _currentStrings;
    private Dictionary<string, string> _fallbackStrings;
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

            CultureInfo.CurrentUICulture = culture;
            CultureInfo.CurrentCulture = culture;

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
        var availableLanguages = GetAvailableLanguages().ToList();

        if (availableLanguages.Contains(culture.Name))
            return culture.Name;

        if (availableLanguages.Contains(culture.Parent.Name))
            return culture.Parent.Name;

        if (availableLanguages.Contains(culture.TwoLetterISOLanguageName))
            return culture.TwoLetterISOLanguageName;

        return "en";
    }

    private IEnumerable<string> GetAvailableLanguages()
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

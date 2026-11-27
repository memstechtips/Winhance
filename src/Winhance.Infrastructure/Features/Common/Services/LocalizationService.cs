using System.Globalization;
using System.Text.Json;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Services;

internal class LocalizationService : ILocalizationService
{
    private CultureInfo _currentCulture;
    private volatile Dictionary<string, string> _currentStrings;
    private volatile Dictionary<string, string> _fallbackStrings;
    private readonly string _localizationPath;
    private readonly IFileSystemService _fileSystemService;
    private string _currentLanguageCode;
    private IReadOnlyList<LanguageOption>? _availableLanguages;
    private string[]? _keptPrefixes;

    private const string DisplayNameKey = "_Meta_LanguageDisplayName";

    public event EventHandler? LanguageChanged;

    public LocalizationService(IFileSystemService fileSystemService)
    {
        _fileSystemService = fileSystemService;
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        _localizationPath = _fileSystemService.CombinePath(baseDir, "Localization");

        _currentCulture = CultureInfo.CurrentUICulture;
        _fallbackStrings = LoadLanguageFile("en");
        var languageCode = ResolveLanguageCode(_currentCulture);
        _currentStrings = languageCode == "en" ? _fallbackStrings : LoadLanguageFile(languageCode);
        _currentLanguageCode = languageCode;
    }

    public string CurrentLanguage => _currentLanguageCode;

    public bool IsRightToLeft => _currentCulture.TextInfo.IsRightToLeft;

    public bool TryGetString(string key, out string value)
    {
        if (_currentStrings.TryGetValue(key, out var current) && !string.IsNullOrEmpty(current))
        {
            value = current;
            return true;
        }

        if (_fallbackStrings.TryGetValue(key, out var english) && !string.IsNullOrEmpty(english))
        {
            value = english;
            return true;
        }

        value = string.Empty;
        return false;
    }

    // Defined in terms of TryGetString so the two can never disagree about what counts as a miss.
    public string GetString(string key) => TryGetString(key, out var value) ? value : $"[{key}]";

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

    public void KeepOnly(params string[] keyPrefixes)
    {
        _keptPrefixes = keyPrefixes;
        var english = Kept(_fallbackStrings);
        _currentStrings = _currentStrings == _fallbackStrings ? english : Kept(_currentStrings);
        _fallbackStrings = english;
    }

    private Dictionary<string, string> Kept(Dictionary<string, string> strings) =>
        _keptPrefixes is not { } prefixes ? strings
        : strings.Where(pair => prefixes.Any(prefix => pair.Key.StartsWith(prefix, StringComparison.Ordinal))).ToDictionary();

    public bool SetLanguage(string languageCode)
    {
        try
        {
            var culture = CultureInfo.GetCultureInfo(languageCode);
            _currentCulture = culture;
            _currentStrings = StringsFor(languageCode);
            _currentLanguageCode = languageCode;

            // Only update the UI culture for resource-loading purposes.
            // Deliberately do NOT change CultureInfo.CurrentCulture — keeping it at
            // InvariantCulture ensures that number formatting throughout the app
            // (including WinUI NumberBox controls) remains locale-independent.
            // Winhance's own localization uses a JSON-based system and does not
            // rely on the thread's CurrentCulture for number/date formatting.
            CultureInfo.CurrentUICulture = culture;
            // Thread-pool threads, where the region lists read display names, take their UI culture from this static.
            CultureInfo.DefaultThreadCurrentUICulture = culture;
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

    // The files do not change while the app runs, and Settings asks on every visit.
    public IReadOnlyList<LanguageOption> GetAvailableLanguages() => _availableLanguages ??= ReadAvailableLanguages();

    private IReadOnlyList<LanguageOption> ReadAvailableLanguages()
    {
        try
        {
            var codes = GetAvailableLanguageCodes().ToList();

            var options = new List<LanguageOption>();
            foreach (var code in codes)
            {
                options.Add(new LanguageOption(code, DisplayNameOf(code)));
            }

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

    // The file's own name keeps the English beside the native one ("Deutsch (German)"), which NativeName ("Deutsch")
    // drops, so it is read; a culture Windows knows stands in for a file without one.
    private string DisplayNameOf(string code)
    {
        if (ReadDisplayName(code) is { } name && !string.IsNullOrWhiteSpace(name))
            return name;

        try
        {
            return CultureInfo.GetCultureInfo(code).NativeName;
        }
        catch (CultureNotFoundException)
        {
            return code;
        }
    }

    // The name is near the top of each file, so this reads up to it and no further: a language's strings are parsed
    // only once it is the one shown.
    private string? ReadDisplayName(string code)
    {
        var path = _fileSystemService.CombinePath(_localizationPath, $"{code}.json");
        if (!_fileSystemService.FileExists(path))
            return null;

        try
        {
            using var stream = _fileSystemService.OpenRead(path);
            return ReadDisplayName(stream);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private static string? ReadDisplayName(Stream stream)
    {
        var buffer = new byte[4096];
        int filled = stream.ReadAtLeast(buffer, 3, throwOnEndOfStream: false);
        int start = buffer.AsSpan(0, filled).StartsWith("\uFEFF"u8) ? 3 : 0;
        var state = default(JsonReaderState);
        bool atName = false;
        int read = filled;
        while (true)
        {
            var reader = new Utf8JsonReader(buffer.AsSpan(start, filled - start), isFinalBlock: read == 0, state);
            while (reader.Read())
            {
                if (atName)
                    return reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
                atName = reader.TokenType == JsonTokenType.PropertyName && reader.CurrentDepth == 1 && reader.ValueTextEquals(DisplayNameKey);
            }
            if (read == 0)
                return null;

            state = reader.CurrentState;
            start += (int)reader.BytesConsumed;
            buffer.AsSpan(start, filled - start).CopyTo(buffer);
            filled -= start;
            start = 0;
            if (filled == buffer.Length)
                Array.Resize(ref buffer, buffer.Length * 2);
            read = stream.Read(buffer, filled, buffer.Length - filled);
            filled += read;
        }
    }

    // A language file is about 250 KB of JSON and startup asks for English three times; the loaded
    // dictionaries are never changed, so they are shared rather than parsed again.
    private Dictionary<string, string> StringsFor(string languageCode) =>
        languageCode == "en" ? _fallbackStrings
        : languageCode == _currentLanguageCode ? _currentStrings
        : LoadLanguageFile(languageCode);

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

            return Kept(dictionary ?? new Dictionary<string, string>());
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }
}

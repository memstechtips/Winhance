using System.Globalization;
using System.Text.Json;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.TestSupport;

namespace Winhance.Infrastructure.Tests.Docs;

// The docs must show the strings the app shows. The builder's inline English fallbacks are not guaranteed to
// match en.json, and a Moq service answers "missing" for everything, so neither is an acceptable source.
internal sealed class EnJsonLocalization : ILocalizationService
{
    private readonly IReadOnlyDictionary<string, string> _strings;

    private EnJsonLocalization(IReadOnlyDictionary<string, string> strings) => _strings = strings;

    public static EnJsonLocalization Load()
    {
        var path = Path.Combine(RepoPaths.LocalizationDir(), "en.json");
        // ReadAllText strips the BOM en.json carries; JsonSerializer over the raw bytes would choke on it.
        var strings = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))
            ?? throw new InvalidOperationException($"{path} deserialized to null");
        return new EnJsonLocalization(strings);
    }

    public string CurrentLanguage => "en";

    public bool IsRightToLeft => false;

    public event EventHandler? LanguageChanged { add { } remove { } }

    public string GetString(string key) => _strings.TryGetValue(key, out var value) ? value : $"[{key}]";

    public string GetString(string key, params object[] args) =>
        string.Format(CultureInfo.InvariantCulture, GetString(key), args);

    public bool TryGetString(string key, out string value)
    {
        if (_strings.TryGetValue(key, out var found) && found.Length > 0)
        {
            value = found;
            return true;
        }

        value = string.Empty;
        return false;
    }

    public bool SetLanguage(string languageCode) => languageCode == "en";

    public IReadOnlyList<LanguageOption> GetAvailableLanguages() => [new("en", "English")];
}

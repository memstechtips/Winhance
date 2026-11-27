using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

public interface ILocalizationService
{
    string GetString(string key);

    string GetString(string key, params object[] args);

    // Prefer this to sniffing GetString's "[key]" miss-marker: a real translation can legitimately be bracketed.
    bool TryGetString(string key, out string value);

    string CurrentLanguage { get; }

    bool IsRightToLeft { get; }

    bool SetLanguage(string languageCode);

    // Drops every string whose key starts with none of the prefixes, now and in any language loaded later. For the tray
    // host, which runs for days and shows a handful of strings from a table of thousands.
    void KeepOnly(params string[] keyPrefixes);

    event EventHandler? LanguageChanged;

    IReadOnlyList<LanguageOption> GetAvailableLanguages();
}

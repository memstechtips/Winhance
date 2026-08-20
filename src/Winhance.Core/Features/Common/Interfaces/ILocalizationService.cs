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

    event EventHandler? LanguageChanged;

    IReadOnlyList<LanguageOption> GetAvailableLanguages();
}

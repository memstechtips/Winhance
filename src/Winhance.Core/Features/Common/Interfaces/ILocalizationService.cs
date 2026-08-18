using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

public interface ILocalizationService
{
    string GetString(string key);

    string GetString(string key, params object[] args);

    /// <summary>Looks up <paramref name="key"/> and reports whether it was found.
    /// Prefer this to sniffing <see cref="GetString(string)"/>'s "[key]" miss-marker: a real
    /// translation can legitimately be bracketed, and the marker cannot tell the two apart.</summary>
    bool TryGetString(string key, out string value);

    string CurrentLanguage { get; }

    bool IsRightToLeft { get; }

    bool SetLanguage(string languageCode);

    event EventHandler? LanguageChanged;

    IReadOnlyList<LanguageOption> GetAvailableLanguages();
}

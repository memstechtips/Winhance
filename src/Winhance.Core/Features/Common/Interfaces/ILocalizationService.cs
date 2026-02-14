using System;
using System.Collections.Generic;

namespace Winhance.Core.Features.Common.Interfaces
{
    public interface ILocalizationService
    {
        string GetString(string key);

        string GetString(string key, params object[] args);

        string CurrentLanguage { get; }

        bool IsRightToLeft { get; }

        bool SetLanguage(string languageCode);

        IEnumerable<string> GetAvailableLanguages();

        event EventHandler? LanguageChanged;
    }
}

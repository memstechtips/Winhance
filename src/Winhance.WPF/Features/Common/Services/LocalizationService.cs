using System;
using System.Collections.Generic;
using System.Globalization;
using System.Resources;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.WPF.Features.Common.Services
{
    public class LocalizationService : ILocalizationService
    {
        private CultureInfo _currentCulture;
        private readonly ResourceManager _resourceManager;

        public event EventHandler? LanguageChanged;

        public LocalizationService()
        {
            _resourceManager = new ResourceManager(
                "Winhance.WPF.Resources.Localization.Strings",
                typeof(LocalizationService).Assembly);

            _currentCulture = CultureInfo.CurrentUICulture;
        }

        public string CurrentLanguage => _currentCulture.TwoLetterISOLanguageName;

        public string GetString(string key)
        {
            try
            {
                var value = _resourceManager.GetString(key, _currentCulture);
                return value ?? $"[{key}]";
            }
            catch
            {
                return $"[{key}]";
            }
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

        public IEnumerable<string> GetAvailableLanguages()
        {
            return new[] { "en", "es", "fr", "de", "pt", "it", "ru", "zh" };
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.WPF.Features.Common.Services
{
    public class LocalizationService : ILocalizationService
    {
        private CultureInfo _currentCulture;
        private Dictionary<string, string> _currentStrings;
        private Dictionary<string, string> _fallbackStrings;
        private readonly string _localizationPath;

        public event EventHandler? LanguageChanged;

        public LocalizationService()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _localizationPath = Path.Combine(baseDir, "Localization");

            _currentCulture = CultureInfo.CurrentUICulture;

            _fallbackStrings = LoadLanguageFile("en");
            _currentStrings = LoadLanguageFile(_currentCulture.TwoLetterISOLanguageName);
        }

        public string CurrentLanguage => _currentCulture.TwoLetterISOLanguageName;

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

        public IEnumerable<string> GetAvailableLanguages()
        {
            try
            {
                if (!Directory.Exists(_localizationPath))
                {
                    return new[] { "en" };
                }

                var jsonFiles = Directory.GetFiles(_localizationPath, "*.json");
                var languages = jsonFiles
                    .Select(Path.GetFileNameWithoutExtension)
                    .Where(lang => !string.IsNullOrEmpty(lang))
                    .OrderBy(lang => lang)
                    .ToList();

                if (!languages.Contains("en"))
                {
                    languages.Insert(0, "en");
                }

                return languages;
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
                var filePath = Path.Combine(_localizationPath, $"{languageCode}.json");

                if (!File.Exists(filePath))
                {
                    return new Dictionary<string, string>();
                }

                var json = File.ReadAllText(filePath);
                var dictionary = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

                return dictionary ?? new Dictionary<string, string>();
            }
            catch
            {
                return new Dictionary<string, string>();
            }
        }
    }
}

using System;
using System.ComponentModel;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.WPF.Features.Common.Services
{
    public sealed class LocalizationManager : INotifyPropertyChanged
    {
        private static LocalizationManager? _instance;
        private static readonly object _lock = new object();
        private ILocalizationService? _localizationService;

        public static LocalizationManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new LocalizationManager();
                        }
                    }
                }
                return _instance;
            }
        }

        private LocalizationManager()
        {
        }

        public void Initialize(ILocalizationService localizationService)
        {
            if (_localizationService != null)
            {
                _localizationService.LanguageChanged -= OnLanguageChanged;
            }

            _localizationService = localizationService;
            _localizationService.LanguageChanged += OnLanguageChanged;
        }

        public string this[string key]
        {
            get
            {
                if (_localizationService == null)
                    return $"[{key}]";

                return _localizationService.GetString(key);
            }
        }

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}

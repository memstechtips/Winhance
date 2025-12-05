using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Constants;

namespace Winhance.WPF.Features.Common.ViewModels
{
    public partial class WinhanceSettingsViewModel : ObservableObject
    {
        private readonly ILocalizationService _localizationService;
        private readonly IWindowManagementService _windowManagementService;
        private readonly IConfigurationService _configurationService;
        private readonly IUserPreferencesService _preferencesService;
        private readonly IDialogService _dialogService;

        [ObservableProperty]
        private ObservableCollection<ComboBoxOption> _languages;

        [ObservableProperty]
        private string _selectedLanguage;

        [ObservableProperty]
        private ObservableCollection<ComboBoxOption> _themes;

        [ObservableProperty]
        private string _selectedTheme;

        public ICommand ImportConfigCommand { get; }
        public ICommand ExportConfigCommand { get; }
        public ICommand ToggleThemeCommand { get; }

        public WinhanceSettingsViewModel(
            ILocalizationService localizationService,
            IWindowManagementService windowManagementService,
            IConfigurationService configurationService,
            IUserPreferencesService preferencesService,
            IDialogService dialogService)
        {
            _localizationService = localizationService;
            _windowManagementService = windowManagementService;
            _configurationService = configurationService;
            _preferencesService = preferencesService;
            _dialogService = dialogService;

            Languages = new ObservableCollection<ComboBoxOption>();
            foreach (var lang in StringKeys.Languages.SupportedLanguages)
            {
                Languages.Add(new ComboBoxOption { DisplayText = lang.Value, Value = lang.Key });
            }

            // Initialize themes
            UpdateThemeOptions();

            // Subscribe to language changes
            _localizationService.LanguageChanged += OnLanguageChanged;

            // Load current language
            var currentLang = _localizationService.CurrentLanguage;
            _selectedLanguage = currentLang ?? "en";

            // Initialize theme selection
            _selectedTheme = _windowManagementService.IsDarkTheme ? "Dark" : "Light";

            ImportConfigCommand = new AsyncRelayCommand(async () => await _configurationService.ImportConfigurationAsync());
            ExportConfigCommand = new AsyncRelayCommand(async () => await _configurationService.ExportConfigurationAsync());
            ToggleThemeCommand = new RelayCommand(_windowManagementService.ToggleTheme);
        }

        private void OnLanguageChanged(object sender, EventArgs e)
        {
            UpdateThemeOptions();
        }

        private void UpdateThemeOptions()
        {
            if (Themes == null)
            {
                Themes = new ObservableCollection<ComboBoxOption>
                {
                    new ComboBoxOption { DisplayText = _localizationService.GetString("Theme_Dark"), Value = "Dark" },
                    new ComboBoxOption { DisplayText = _localizationService.GetString("Theme_Light"), Value = "Light" }
                };
            }
            else
            {
                foreach (var theme in Themes)
                {
                    if (theme.Value?.ToString() == "Dark")
                        theme.DisplayText = _localizationService.GetString("Theme_Dark");
                    else if (theme.Value?.ToString() == "Light")
                        theme.DisplayText = _localizationService.GetString("Theme_Light");
                }
            }
        }

        partial void OnSelectedThemeChanged(string value)
        {
            if (string.IsNullOrEmpty(value)) return;

            bool isDark = value == "Dark";
            if (_windowManagementService.IsDarkTheme != isDark)
            {
                _windowManagementService.ToggleTheme();
            }
        }

        partial void OnSelectedLanguageChanged(string value)
        {
            // Avoid redundant updates
            if (string.IsNullOrEmpty(value) || value == _localizationService.CurrentLanguage) return;

            if (_localizationService.SetLanguage(value))
            {
                _preferencesService.SetPreferenceAsync("Language", value);
            }
        }
    }
}

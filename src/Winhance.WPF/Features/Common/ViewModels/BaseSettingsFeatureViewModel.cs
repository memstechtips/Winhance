using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Interfaces;

namespace Winhance.WPF.Features.Common.ViewModels
{
    /// <summary>
    /// Base class for settings-based feature ViewModels (Customize/Optimize features).
    /// </summary>
    public abstract partial class BaseSettingsFeatureViewModel : ObservableObject, IFeatureViewModel
    {
        protected readonly IDomainServiceRouter _domainServiceRouter;
        protected readonly ISettingsLoadingService _settingsLoadingService;
        protected readonly ILogService _logService;

        [ObservableProperty]
        private ObservableCollection<SettingItemViewModel> _settings = new();

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private bool _isExpanded = true;

        [ObservableProperty]
        private string _searchText = string.Empty;

        // IFeatureViewModel implementation
        public bool HasVisibleSettings => Settings.Any(s => s.IsVisible);
        public bool IsVisibleInSearch => HasVisibleSettings;
        public event EventHandler<FeatureVisibilityChangedEventArgs>? VisibilityChanged;
        public int SettingsCount => Settings?.Count ?? 0;

        // Commands
        public ICommand LoadSettingsCommand { get; }
        public ICommand ToggleExpandCommand { get; }

        // Abstract properties that child classes must implement
        public abstract string ModuleId { get; }
        public abstract string DisplayName { get; }
        public abstract string Description { get; }
        public abstract string Category { get; }
        public abstract int SortOrder { get; }

        protected BaseSettingsFeatureViewModel(
          IDomainServiceRouter domainServiceRouter,
          ISettingsLoadingService settingsLoadingService,
          ILogService logService)
        {
            _domainServiceRouter = domainServiceRouter ?? throw new ArgumentNullException(nameof(domainServiceRouter));
            _settingsLoadingService = settingsLoadingService ?? throw new ArgumentNullException(nameof(settingsLoadingService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            LoadSettingsCommand = new AsyncRelayCommand(LoadSettingsAsync);
            ToggleExpandCommand = new RelayCommand(() => IsExpanded = !IsExpanded);
        }

        /// <summary>
        /// Applies search filter to all settings in this feature.
        /// </summary>
        public void ApplySearchFilter(string searchText)
        {
            SearchText = searchText ?? string.Empty;
        }

        partial void OnSearchTextChanged(string value)
        {
            // Check if search matches the feature name itself
            bool featureMatches = string.IsNullOrWhiteSpace(value) ||
                                 DisplayName.ToLowerInvariant().Contains(value.ToLowerInvariant());

            if (featureMatches)
            {
                // Show ALL settings in this feature
                foreach (var setting in Settings)
                {
                    setting.IsVisible = true;
                }
            }
            else
            {
                // Filter individual settings
                foreach (var setting in Settings)
                {
                    setting.UpdateVisibility(value);
                }
            }

            // Notify about visibility changes
            OnPropertyChanged(nameof(HasVisibleSettings));
            OnPropertyChanged(nameof(IsVisibleInSearch));
            VisibilityChanged?.Invoke(this, new FeatureVisibilityChangedEventArgs(ModuleId, IsVisibleInSearch, value));
        }

        public virtual async Task LoadSettingsAsync()
        {
            try
            {
                IsLoading = true;

                Settings = new ObservableCollection<SettingItemViewModel>(
                    (await _settingsLoadingService.LoadConfiguredSettingsAsync(
                        _domainServiceRouter.GetDomainService(ModuleId),
                        ModuleId,
                        $"Loading {DisplayName} settings..."
                    )).Cast<SettingItemViewModel>()
                );

                _logService.Log(LogLevel.Info,
                    $"{GetType().Name}: Successfully loaded {Settings.Count} settings");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error,
                    $"Error loading {DisplayName} settings: {ex.Message}");
                throw;
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
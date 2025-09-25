using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Services;
using Winhance.WPF.Features.Common.Interfaces;

namespace Winhance.WPF.Features.Common.ViewModels
{
    public abstract partial class BaseSettingsFeatureViewModel : BaseFeatureViewModel, ISettingsFeatureViewModel
    {
        protected readonly IDomainServiceRouter domainServiceRouter;
        protected readonly ISettingsLoadingService settingsLoadingService;
        protected readonly ILogService logService;
        private bool _isDisposed;
        private bool _settingsLoaded = false;
        private readonly object _loadingLock = new object();
        
        [ObservableProperty]
        private ObservableCollection<SettingItemViewModel> _settings = new();

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private bool _isExpanded = true;

        [ObservableProperty]
        private string _searchText = string.Empty;

        public bool HasVisibleSettings => Settings.Any(s => s.IsVisible);
        public bool IsVisibleInSearch => HasVisibleSettings;
        public event EventHandler<FeatureVisibilityChangedEventArgs>? VisibilityChanged;
        public int SettingsCount => Settings?.Count ?? 0;

        public ICommand LoadSettingsCommand { get; }
        public ICommand ToggleExpandCommand { get; }

        protected BaseSettingsFeatureViewModel(
            IDomainServiceRouter domainServiceRouter,
            ISettingsLoadingService settingsLoadingService,
            ILogService logService)
            : base()
        {
            this.domainServiceRouter = domainServiceRouter ?? throw new ArgumentNullException(nameof(domainServiceRouter));
            this.settingsLoadingService = settingsLoadingService ?? throw new ArgumentNullException(nameof(settingsLoadingService));
            this.logService = logService ?? throw new ArgumentNullException(nameof(logService));
            LoadSettingsCommand = new AsyncRelayCommand(LoadSettingsAsync);
            ToggleExpandCommand = new RelayCommand(() => IsExpanded = !IsExpanded);
        }

        public virtual async Task<bool> HandleDomainContextSettingAsync(SettingDefinition setting, object? value, bool additionalContext = false)
        {
            return false;
        }

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
            lock (_loadingLock)
            {
                if (_settingsLoaded) return;
                _settingsLoaded = true;
            }

            try
            {
                IsLoading = true;

                if (Settings?.Any() == true)
                {
                    foreach (var setting in Settings.OfType<IDisposable>())
                    {
                        setting?.Dispose();
                    }
                    Settings.Clear();
                }

                var loadedSettings = (await settingsLoadingService.LoadConfiguredSettingsAsync(
                    domainServiceRouter.GetDomainService(ModuleId),
                    ModuleId,
                    $"Loading {DisplayName} settings...",
                    this
                )).Cast<SettingItemViewModel>();
                
                Settings = new ObservableCollection<SettingItemViewModel>(loadedSettings);
                
                UpdateParentChildRelationships();

                logService.Log(LogLevel.Info,
                    $"{GetType().Name}: Successfully loaded {Settings.Count} settings");
            }
            catch (Exception ex)
            {
                lock (_loadingLock)
                {
                    _settingsLoaded = false;
                }
                logService.Log(LogLevel.Error,
                    $"Error loading {DisplayName} settings: {ex.Message}");
                throw;
            }
            finally
            {
                IsLoading = false;
            }
        }

        public virtual void OnNavigatedFrom()
        {
            SearchText = string.Empty;
            VisibilityChanged = null;
        }

        public virtual void OnNavigatedTo(object? parameter = null)
        {
            if (!Settings.Any())
            {
                _ = LoadSettingsAsync();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed && disposing)
            {
                if (Settings != null)
                {
                    foreach (var setting in Settings.OfType<IDisposable>())
                    {
                        setting?.Dispose();
                    }
                    Settings.Clear();
                }

                _settingsLoaded = false;
                VisibilityChanged = null;
                _isDisposed = true;
            }
        }

        private void UpdateParentChildRelationships()
        {
            foreach (var setting in Settings)
            {
                if (!string.IsNullOrEmpty(setting.SettingDefinition?.ParentSettingId))
                {
                    var parent = Settings.FirstOrDefault(s => s.SettingId == setting.SettingDefinition.ParentSettingId);
                    if (parent != null)
                    {
                        setting.ParentIsEnabled = parent.IsSelected;
                    }
                }
            }
        }

        ~BaseSettingsFeatureViewModel()
        {
            Dispose(false);
        }
    }
}
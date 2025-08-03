using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.WPF.Features.Common.Helpers;
using Winhance.WPF.Features.Common.Models;

namespace Winhance.WPF.Features.Common.ViewModels
{
    /// <summary>
    /// Modern base class for settings view models using clean architecture principles.
    /// Uses IApplicationSettingsService for business logic and SettingUIItem for UI state.
    /// </summary>
    public abstract partial class BaseSettingsViewModel : ObservableObject
    {
        #region Protected Services

        protected readonly IApplicationSettingsService _settingsService;
        protected readonly ITaskProgressService _progressService;
        protected readonly ILogService _logService;

        #endregion

        #region Observable Properties

        /// <summary>
        /// Collection of UI setting items for display.
        /// </summary>
        public ObservableCollection<SettingUIItem> Settings { get; } = new();

        /// <summary>
        /// Collection of grouped settings for organized display.
        /// </summary>
        public ObservableCollection<SettingGroup> SettingGroups { get; } = new();

        /// <summary>
        /// Whether settings are currently being loaded.
        /// </summary>
        [ObservableProperty]
        private bool _isLoading;

        /// <summary>
        /// Whether all settings are selected (for bulk operations).
        /// </summary>
        [ObservableProperty]
        private bool _isAllSelected;

        /// <summary>
        /// Whether there are visible settings to display.
        /// </summary>
        [ObservableProperty]
        private bool _hasVisibleSettings = true;

        /// <summary>
        /// The category name for this settings view model.
        /// </summary>
        [ObservableProperty]
        private string _categoryName = string.Empty;

        /// <summary>
        /// Current search/filter text.
        /// </summary>
        [ObservableProperty]
        private string _searchText = string.Empty;

        #endregion

        #region Commands

        /// <summary>
        /// Command to apply a specific setting.
        /// </summary>
        public ICommand ApplySettingCommand { get; }

        /// <summary>
        /// Command to apply all selected settings.
        /// </summary>
        public ICommand ApplyAllSelectedCommand { get; }

        /// <summary>
        /// Command to refresh all settings status.
        /// </summary>
        public ICommand RefreshAllCommand { get; }

        /// <summary>
        /// Command to select/deselect all settings.
        /// </summary>
        public ICommand SelectAllCommand { get; }

        #endregion

        #region Constructor

        protected BaseSettingsViewModel(
            IApplicationSettingsService settingsService,
            ITaskProgressService progressService,
            ILogService logService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _progressService = progressService ?? throw new ArgumentNullException(nameof(progressService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));

            // Initialize commands
            ApplySettingCommand = new AsyncRelayCommand<SettingUIItem>(ApplySettingAsync);
            ApplyAllSelectedCommand = new AsyncRelayCommand(ApplyAllSelectedAsync);
            RefreshAllCommand = new AsyncRelayCommand(RefreshAllSettingsAsync);
            SelectAllCommand = new RelayCommand<bool>(SelectAllSettings);

            // Subscribe to service events
            _settingsService.SettingStatusChanged += OnSettingStatusChanged;

            // Subscribe to property changes for search
            PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SearchText))
                {
                    FilterSettings();
                }
            };
        }

        #endregion

        #region Abstract Methods

        /// <summary>
        /// Derived classes must implement this to provide their specific application settings.
        /// </summary>
        /// <returns>A task that returns the collection of application settings for this view model.</returns>
        protected abstract Task<IEnumerable<ApplicationSetting>> GetApplicationSettingsAsync();

        #endregion

        #region Public Methods

        /// <summary>
        /// Loads settings and initializes the UI state.
        /// </summary>
        public virtual async Task LoadSettingsAsync()
        {
            try
            {
                IsLoading = true;
                _logService.Log(LogLevel.Info, $"Loading settings for {CategoryName}");

                // Clear existing collections
                Settings.Clear();
                SettingGroups.Clear();

                // Get application settings from derived class
                var applicationSettings = await GetApplicationSettingsAsync();

                // Register settings with the service
                _settingsService.RegisterSettings(applicationSettings);

                // Convert to UI models
                var uiItems = SettingUIMapper.ToUIItems(applicationSettings).ToList();

                // Add to collections
                foreach (var uiItem in uiItems)
                {
                    Settings.Add(uiItem);
                }

                // Create grouped view
                var groups = SettingUIMapper.ToGroupedUIItems(applicationSettings);
                foreach (var group in groups)
                {
                    SettingGroups.Add(group);
                }

                // Refresh status for all settings
                await RefreshAllSettingsAsync();

                _logService.Log(LogLevel.Info, $"Loaded {Settings.Count} settings for {CategoryName}");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error loading settings for {CategoryName}: {ex.Message}");
                throw;
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Refreshes the status of all settings from the system.
        /// </summary>
        public virtual async Task RefreshAllSettingsAsync()
        {
            try
            {
                _logService.Log(LogLevel.Debug, "Refreshing all settings status");

                var settingIds = Settings.Select(s => s.Id).ToList();
                
                // Get current state from service
                var statesTask = _settingsService.GetMultipleSettingsStateAsync(settingIds);
                var valuesTask = _settingsService.GetMultipleSettingsValuesAsync(settingIds);

                await Task.WhenAll(statesTask, valuesTask);

                var states = statesTask.Result;
                var values = valuesTask.Result;

                // Update UI items
                foreach (var uiItem in Settings)
                {
                    if (states.TryGetValue(uiItem.Id, out var isEnabled) &&
                        values.TryGetValue(uiItem.Id, out var currentValue))
                    {
                        var status = isEnabled ? RegistrySettingStatus.Applied : RegistrySettingStatus.NotApplied;
                        SettingUIMapper.UpdateFromSystemState(uiItem, isEnabled, currentValue, status);
                    }
                }

                UpdateHasVisibleSettings();
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error refreshing settings status: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region Command Handlers

        /// <summary>
        /// Applies a specific setting using the service.
        /// </summary>
        private async Task ApplySettingAsync(SettingUIItem? uiItem)
        {
            if (uiItem == null) return;

            try
            {
                uiItem.IsApplying = true;
                _logService.Log(LogLevel.Info, $"Applying setting: {uiItem.Name}");

                // Use the service to apply the setting
                await _settingsService.ApplySettingAsync(uiItem.Id, uiItem.IsSelected, uiItem.SelectedValue);

                // Refresh this setting's status
                await RefreshSettingStatusAsync(uiItem.Id);

                _logService.Log(LogLevel.Info, $"Successfully applied setting: {uiItem.Name}");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error applying setting {uiItem.Name}: {ex.Message}");
                // TODO: Show user-friendly error message
                throw;
            }
            finally
            {
                uiItem.IsApplying = false;
            }
        }

        /// <summary>
        /// Applies all selected settings.
        /// </summary>
        private async Task ApplyAllSelectedAsync()
        {
            try
            {
                var selectedSettings = Settings.Where(s => s.IsSelected).ToList();
                if (!selectedSettings.Any()) return;

                _progressService.StartTask($"Applying {selectedSettings.Count} settings...");

                var settingsToApply = selectedSettings.ToDictionary(
                    s => s.Id, 
                    s => (s.IsSelected, s.SelectedValue)
                );

                await _settingsService.ApplyMultipleSettingsAsync(settingsToApply);
                await RefreshAllSettingsAsync();

                _progressService.CompleteTask();
                _logService.Log(LogLevel.Info, $"Applied {selectedSettings.Count} settings successfully");
            }
            catch (Exception ex)
            {
                _progressService.CompleteTask();
                _logService.Log(LogLevel.Error, $"Error applying multiple settings: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Selects or deselects all settings.
        /// </summary>
        private void SelectAllSettings(bool selectAll)
        {
            foreach (var setting in Settings.Where(s => s.IsEnabled))
            {
                setting.IsSelected = selectAll;
            }

            IsAllSelected = selectAll;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Handles setting status change events from the service.
        /// </summary>
        private void OnSettingStatusChanged(object? sender, SettingStatusChangedEventArgs e)
        {
            var uiItem = Settings.FirstOrDefault(s => s.Id == e.SettingId);
            if (uiItem != null)
            {
                var status = e.IsEnabled ? RegistrySettingStatus.Applied : RegistrySettingStatus.NotApplied;
                SettingUIMapper.UpdateFromSystemState(uiItem, e.IsEnabled, e.CurrentValue, status);
            }
        }

        /// <summary>
        /// Refreshes the status of a specific setting.
        /// </summary>
        private async Task RefreshSettingStatusAsync(string settingId)
        {
            try
            {
                await _settingsService.RefreshSettingStatusAsync(settingId);
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error refreshing setting {settingId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Filters settings based on search text.
        /// </summary>
        private void FilterSettings()
        {
            foreach (var setting in Settings)
            {
                setting.IsVisible = string.IsNullOrWhiteSpace(SearchText) || 
                                   setting.MatchesSearch(SearchText);
            }

            // Update group visibility
            foreach (var group in SettingGroups)
            {
                group.UpdateVisibility();
            }

            UpdateHasVisibleSettings();
        }

        /// <summary>
        /// Updates the HasVisibleSettings property based on current visibility.
        /// </summary>
        private void UpdateHasVisibleSettings()
        {
            HasVisibleSettings = Settings.Any(s => s.IsVisible);
        }

        #endregion

        #region IDisposable

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _settingsService.SettingStatusChanged -= OnSettingStatusChanged;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}

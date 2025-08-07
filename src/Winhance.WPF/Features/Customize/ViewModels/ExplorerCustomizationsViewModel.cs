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
using Winhance.Core.Features.Customize.Interfaces;
using Winhance.WPF.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Models;

namespace Winhance.WPF.Features.Customize.ViewModels
{
    /// <summary>
    /// ViewModel for Explorer customizations using clean architecture principles.
    /// </summary>
    public partial class ExplorerCustomizationsViewModel : ObservableObject, IFeatureViewModel
    {
        private readonly IExplorerCustomizationService _explorerService;
        private readonly IDialogService _dialogService;
        private readonly ISettingsUICoordinator _uiCoordinator;
        private readonly ILogService _logService;
        private readonly ITaskProgressService _progressService;

        /// <summary>
        /// Gets the command to execute an action.
        /// </summary>
        public IAsyncRelayCommand<string> ExecuteActionCommand { get; }

        // LoadSettingsCommand is now defined as ICommand for IFeatureViewModel interface

        // Delegating properties to UI coordinator
        public ObservableCollection<SettingUIItem> Settings => _uiCoordinator.Settings;
        public ObservableCollection<SettingGroup> SettingGroups => _uiCoordinator.SettingGroups;
        public bool IsLoading => _uiCoordinator.IsLoading;
        public string CategoryName => _uiCoordinator.CategoryName;
        public string SearchText
        {
            get => _uiCoordinator.SearchText;
            set => _uiCoordinator.SearchText = value;
        }
        public bool HasVisibleSettings => _uiCoordinator.HasVisibleSettings;

        // IFeatureViewModel implementation
        public string ModuleId => "ExplorerCustomization";
        public string DisplayName => "Explorer";
        public int SettingsCount => Settings?.Count ?? 0;
        public string Category => "Customize";
        public string Description => "Customize Windows Explorer settings";
        public int SortOrder => 1;
        public ICommand LoadSettingsCommand { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExplorerCustomizationsViewModel"/> class.
        /// </summary>
        /// <param name="explorerService">The explorer domain service.</param>
        /// <param name="uiCoordinator">The settings UI coordinator.</param>
        /// <param name="logService">The log service.</param>
        /// <param name="dialogService">The dialog service.</param>
        /// <param name="progressService">The task progress service.</param>
        public ExplorerCustomizationsViewModel(
            IExplorerCustomizationService explorerService,
            ISettingsUICoordinator uiCoordinator,
            ILogService logService,
            IDialogService dialogService,
            ITaskProgressService progressService)
        {
            _explorerService = explorerService ?? throw new ArgumentNullException(nameof(explorerService));
            _uiCoordinator = uiCoordinator ?? throw new ArgumentNullException(nameof(uiCoordinator));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _progressService = progressService ?? throw new ArgumentNullException(nameof(progressService));
            
            _uiCoordinator.CategoryName = "Explorer";
            
            // Initialize commands
            ExecuteActionCommand = new AsyncRelayCommand<string>(ExecuteActionAsync);
            LoadSettingsCommand = new AsyncRelayCommand(LoadSettingsAsync);
        }

        /// <summary>
        /// Executes a named action.
        /// </summary>
        /// <param name="actionId">The ID of the action to execute.</param>
        private async Task ExecuteActionAsync(string actionId)
        {
            if (string.IsNullOrEmpty(actionId))
                return;

            try
            {
                _progressService.StartTask($"Executing action: {actionId}...");
                
                // Use the explorer service to execute the action
                await _explorerService.ExecuteExplorerActionAsync(actionId);
                
                // Refresh settings after action
                await LoadSettingsAsync();
                
                _progressService.CompleteTask();
            }
            catch (Exception ex)
            {
                _progressService.CompleteTask();
                _logService.Log(LogLevel.Error, $"Error executing action {actionId}: {ex.Message}");
                await _dialogService.ShowErrorAsync($"Failed to execute action: {ex.Message}", "Action Error");
            }
        }

        /// <summary>
        /// Loads settings and initializes the UI state.
        /// </summary>
        public async Task LoadSettingsAsync()
        {
            try
            {
                _progressService.StartTask("Loading explorer settings...");
                
                // Use UI coordinator to load settings
                await _uiCoordinator.LoadSettingsAsync(() => _explorerService.GetSettingsAsync());
                
                // Apply any Explorer-specific filtering or organization
                OrganizeExplorerSettings();
                
                _progressService.CompleteTask();
            }
            catch (Exception ex)
            {
                _progressService.CompleteTask();
                _logService.Log(LogLevel.Error, $"Error loading explorer settings: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Organizes Explorer settings with specific grouping and display logic.
        /// </summary>
        private void OrganizeExplorerSettings()
        {
            // Apply Explorer-specific organization if needed
            // The UI coordinator already handles basic grouping
            // This method can be used for Explorer-specific customizations
        }

        /// <summary>
        /// Refreshes the settings for this feature asynchronously.
        /// </summary>
        public async Task RefreshSettingsAsync()
        {
            await LoadSettingsAsync();
        }

        /// <summary>
        /// Clears all settings and resets the feature state.
        /// </summary>
        public void ClearSettings()
        {
            _uiCoordinator.ClearSettings();
        }
    }
}

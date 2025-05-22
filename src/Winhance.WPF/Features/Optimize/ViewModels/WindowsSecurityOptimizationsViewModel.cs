using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Models;
using Winhance.WPF.Features.Common.Models;
using Winhance.WPF.Features.Common.ViewModels;
// Use UacLevel from Core.Models.Enums for the UI
using UacLevel = Winhance.Core.Models.Enums.UacLevel;

namespace Winhance.WPF.Features.Optimize.ViewModels
{
    /// <summary>
    /// ViewModel for Windows Security optimizations.
    /// </summary>
    public partial class WindowsSecurityOptimizationsViewModel : BaseViewModel
    {
        private readonly IRegistryService _registryService;
        private readonly ILogService _logService;
        private readonly ISystemServices _systemServices;

        /// <summary>
        /// Gets or sets a value indicating whether the view model is selected.
        /// </summary>
        [ObservableProperty]
        private bool _isSelected;

        /// <summary>
        /// Gets or sets the selected UAC level.
        /// </summary>
        [ObservableProperty]
        private UacLevel _selectedUacLevel;

        /// <summary>
        /// Gets or sets a value indicating whether the view model has visible settings.
        /// </summary>
        [ObservableProperty]
        private bool _hasVisibleSettings = true;

        /// <summary>
        /// Gets or sets a value indicating whether the UAC level is being applied.
        /// </summary>
        [ObservableProperty]
        private bool _isApplyingUacLevel;

        /// <summary>
        /// Gets the collection of available UAC levels with their display names.
        /// </summary>
        public ObservableCollection<KeyValuePair<UacLevel, string>> UacLevelOptions { get; } = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="WindowsSecurityOptimizationsViewModel"/> class.
        /// </summary>
        /// <param name="progressService">The task progress service.</param>
        /// <param name="registryService">The registry service.</param>
        /// <param name="logService">The log service.</param>
        /// <param name="systemServices">The system services.</param>
        public WindowsSecurityOptimizationsViewModel(
            ITaskProgressService progressService,
            IRegistryService registryService,
            ILogService logService,
            ISystemServices systemServices
        )
            : base(progressService, logService, new Features.Common.Services.MessengerService())
        {
            _registryService = registryService ?? throw new ArgumentNullException(nameof(registryService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _systemServices = systemServices ?? throw new ArgumentNullException(nameof(systemServices));

            // Initialize UAC level options
            foreach (var kvp in UacOptimizations.UacLevelNames)
            {
                UacLevelOptions.Add(new KeyValuePair<UacLevel, string>(kvp.Key, kvp.Value));
            }

            // Subscribe to property changes to handle UAC level changes
            this.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SelectedUacLevel))
                {
                    HandleUACLevelChange();
                }
            };
        }

        /// <summary>
        /// Gets the collection of settings.
        /// </summary>
        public ObservableCollection<ApplicationSettingItem> Settings { get; } =
            new ObservableCollection<ApplicationSettingItem>();

        /// <summary>
        /// Handles changes to the UAC notification level.
        /// </summary>
        private void HandleUACLevelChange()
        {
            try
            {
                IsApplyingUacLevel = true;
                
                // Set the UAC level using the system service - no conversion needed
                _systemServices.SetUacLevel(SelectedUacLevel);
                
                string levelName = UacOptimizations.UacLevelNames.TryGetValue(SelectedUacLevel, out string name) ? name : SelectedUacLevel.ToString();
                LogInfo($"UAC Notification Level set to {levelName}");
            }
            catch (Exception ex)
            {
                LogError($"Error setting UAC notification level: {ex.Message}");
            }
            finally
            {
                IsApplyingUacLevel = false;
            }
        }
        
        // Conversion methods removed as we're now using UacLevel directly

        /// <summary>
        /// Gets the display name of the UAC level.
        /// </summary>
        /// <param name="level">The UAC level.</param>
        /// <returns>The display name of the UAC level.</returns>
        private string GetUacLevelDisplayName(UacLevel level)
        {
            return UacOptimizations.UacLevelNames.TryGetValue(level, out string name) ? name : level.ToString();
        }

        /// <summary>
        /// Loads the settings.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task LoadSettingsAsync()
        {
            try
            {
                IsLoading = true;
                LogInfo("Loading UAC notification level setting");

                // Clear existing settings
                Settings.Clear();

                // Add a searchable item for the UAC dropdown
                var uacDropdownItem = new ApplicationSettingItem(_registryService, null, _logService)
                {
                    Id = "UACDropdown",
                    Name = "User Account Control Notification Level",
                    Description = "Controls when Windows notifies you about changes to your computer",
                    GroupName = "Windows Security Settings",
                    IsVisible = true,
                    ControlType = ControlType.Dropdown,
                };
                Settings.Add(uacDropdownItem);
                LogInfo("Added searchable item for UAC dropdown");

                // Load UAC notification level
                try
                {
                    // Get the UAC level from the system service (now returns Core UacLevel directly)
                    SelectedUacLevel = _systemServices.GetUacLevel();
                    
                    string levelName = GetUacLevelDisplayName(SelectedUacLevel);
                    LogInfo($"Loaded UAC Notification Level: {levelName}");
                }
                catch (Exception ex)
                {
                    LogError($"Error loading UAC notification level: {ex.Message}");
                    SelectedUacLevel = UacLevel.NotifyChangesOnly; // Default to standard notification level if there's an error
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                LogError($"Error loading Windows security settings: {ex.Message}");
                throw;
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Checks the status of all settings.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task CheckSettingStatusesAsync()
        {
            try
            {
                IsLoading = true;
                LogInfo("Checking UAC notification level status");

                // Refresh UAC notification level
                await LoadSettingsAsync();

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                LogError($"Error checking UAC notification level status: {ex.Message}");
                throw;
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Applies the selected settings.
        /// </summary>
        /// <param name="progress">Progress reporter.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task ApplySettingsAsync(
            IProgress<Winhance.Core.Features.Common.Models.TaskProgressDetail> progress
        )
        {
            try
            {
                IsLoading = true;
                IsApplyingUacLevel = true;
                
                progress.Report(
                    new Winhance.Core.Features.Common.Models.TaskProgressDetail
                    {
                        StatusText = "Applying UAC notification level setting...",
                        Progress = 0,
                    }
                );

                // Apply UAC notification level using the system service directly
                // No conversion needed as we're now using UacLevel directly
                _systemServices.SetUacLevel(SelectedUacLevel);
                
                string levelName = GetUacLevelDisplayName(SelectedUacLevel);
                LogInfo($"UAC Notification Level set to {levelName}");
                
                progress.Report(
                    new Winhance.Core.Features.Common.Models.TaskProgressDetail
                    {
                        StatusText = $"{levelName} UAC notification level applied",
                        Progress = 1.0,
                    }
                );
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                LogError($"Error applying UAC notification level: {ex.Message}");
                throw;
            }
            finally
            {
                IsLoading = false;
                IsApplyingUacLevel = false;
            }
        }

        /// <summary>
        /// Restores the default settings.
        /// </summary>
        /// <param name="progress">Progress reporter.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task RestoreDefaultsAsync(
            IProgress<Winhance.Core.Features.Common.Models.TaskProgressDetail> progress
        )
        {
            try
            {
                IsLoading = true;
                progress.Report(
                    new Winhance.Core.Features.Common.Models.TaskProgressDetail
                    {
                        StatusText = "Restoring UAC notification level to default...",
                        Progress = 0,
                    }
                );

                // Set UAC notification level to default (Notify when apps try to make changes)
                SelectedUacLevel = UacLevel.NotifyChangesOnly;
                progress.Report(
                    new Winhance.Core.Features.Common.Models.TaskProgressDetail
                    {
                        StatusText = "Applying UAC notification level...",
                        Progress = 0.5,
                    }
                );

                // Apply the changes
                await ApplySettingsAsync(progress);

                progress.Report(
                    new Winhance.Core.Features.Common.Models.TaskProgressDetail
                    {
                        StatusText = "UAC notification level restored to default",
                        Progress = 1.0,
                    }
                );
            }
            catch (Exception ex)
            {
                LogError($"Error restoring UAC notification level: {ex.Message}");
                throw;
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}

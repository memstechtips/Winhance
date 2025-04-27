using System;
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
using Winhance.WPF.Features.Optimize.Models;

namespace Winhance.WPF.Features.Optimize.ViewModels
{
    /// <summary>
    /// ViewModel for Windows Security optimizations.
    /// </summary>
    public partial class WindowsSecurityOptimizationsViewModel : BaseViewModel
    {
        private readonly IRegistryService _registryService;
        private readonly ILogService _logService;

        /// <summary>
        /// Gets or sets a value indicating whether the view model is selected.
        /// </summary>
        [ObservableProperty]
        private bool _isSelected;

        /// <summary>
        /// Gets or sets the UAC notification level (0=Low, 1=Moderate, 2=High).
        /// </summary>
        [ObservableProperty]
        private int _uacLevel;

        /// <summary>
        /// Gets or sets a value indicating whether the view model has visible settings.
        /// </summary>
        [ObservableProperty]
        private bool _hasVisibleSettings = true;

        /// <summary>
        /// Initializes a new instance of the <see cref="WindowsSecurityOptimizationsViewModel"/> class.
        /// </summary>
        /// <param name="progressService">The task progress service.</param>
        /// <param name="registryService">The registry service.</param>
        /// <param name="logService">The log service.</param>
        public WindowsSecurityOptimizationsViewModel(
            ITaskProgressService progressService,
            IRegistryService registryService,
            ILogService logService)
            : base(progressService, logService, new Features.Common.Services.MessengerService())
        {
            _registryService = registryService ?? throw new ArgumentNullException(nameof(registryService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));

            // Subscribe to property changes to handle UAC level changes
            this.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(UacLevel))
                {
                    HandleUACLevelChange();
                }
            };
        }

        /// <summary>
        /// Gets the collection of settings.
        /// </summary>
        public ObservableCollection<ISettingItem> Settings { get; } = new ObservableCollection<ISettingItem>();

        /// <summary>
        /// Handles changes to the UAC notification level.
        /// </summary>
        private void HandleUACLevelChange()
        {
            try
            {
                // Get the registry value corresponding to the selected UAC level
                if (UacOptimizations.LevelToRegistryValue.TryGetValue(UacLevel, out int registryValue))
                {
                    // Apply UAC notification level setting
                    string registryPath = $"HKLM\\{UacOptimizations.RegistryPath}";
                    _registryService.SetValue(
                        registryPath, 
                        UacOptimizations.RegistryName, 
                        registryValue, 
                        UacOptimizations.ValueKind);
                    
                    LogInfo($"UAC Notification Level set to {GetUacLevelName(UacLevel)} (registry value: {registryValue})");
                }
                else
                {
                    LogError($"Invalid UAC level: {UacLevel}");
                }
            }
            catch (Exception ex)
            {
                LogError($"Error setting UAC notification level: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the name of the UAC level.
        /// </summary>
        /// <param name="level">The UAC level (0=Low, 1=Moderate, 2=High).</param>
        /// <returns>The name of the UAC level.</returns>
        private string GetUacLevelName(int level)
        {
            return level switch
            {
                0 => "Low",
                1 => "Moderate",
                2 => "High",
                _ => "Unknown"
            };
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

                // Add a searchable item for the UAC slider
                var uacSliderItem = new OptimizationSettingItem(_registryService, null, _logService)
                {
                    Id = "UACSlider",
                    Name = "User Account Control Notification Level",
                    Description = "Controls when Windows notifies you about changes to your computer",
                    GroupName = "Windows Security Settings",
                    IsVisible = true,
                    ControlType = ControlType.ThreeStateSlider
                };
                Settings.Add(uacSliderItem);
                LogInfo("Added searchable item for UAC slider");

                // Load UAC notification level
                try
                {
                    string registryPath = $"HKLM\\{UacOptimizations.RegistryPath}";
                    var consentValue = _registryService.GetValue(registryPath, UacOptimizations.RegistryName);
                    
                    if (consentValue != null)
                    {
                        int registryValue = Convert.ToInt32(consentValue);
                        
                        // Convert registry value to slider level
                        if (UacOptimizations.RegistryValueToLevel.TryGetValue(registryValue, out int level))
                        {
                            UacLevel = level;
                            LogInfo($"Loaded UAC Notification Level: {GetUacLevelName(UacLevel)} (registry value: {registryValue})");
                        }
                        else
                        {
                            // Default to Moderate if registry value is not recognized
                            UacLevel = 1;
                            LogInfo($"Unrecognized UAC registry value: {registryValue}, defaulting to Moderate");
                        }
                    }
                    else
                    {
                        // Default to Moderate if registry value is not found
                        UacLevel = 1;
                        LogInfo("UAC registry value not found, defaulting to Moderate");
                    }
                }
                catch (Exception ex)
                {
                    LogError($"Error loading UAC notification level: {ex.Message}");
                    UacLevel = 1; // Default to Moderate if there's an error
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
        /// Applies all selected settings.
        /// </summary>
        /// <param name="progress">The progress reporter.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task ApplySettingsAsync(IProgress<TaskProgressDetail> progress)
        {
            try
            {
                IsLoading = true;
                progress.Report(new TaskProgressDetail { StatusText = "Applying UAC notification level setting...", IsIndeterminate = false, Progress = 0 });

                // Apply UAC notification level
                HandleUACLevelChange();
                progress.Report(new TaskProgressDetail { StatusText = "UAC notification level applied", IsIndeterminate = false, Progress = 1.0 });
            }
            catch (Exception ex)
            {
                LogError($"Error applying UAC notification level: {ex.Message}");
                throw;
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Restores all selected settings to their default values.
        /// </summary>
        /// <param name="progress">The progress reporter.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task RestoreDefaultsAsync(IProgress<TaskProgressDetail> progress)
        {
            try
            {
                IsLoading = true;
                progress.Report(new TaskProgressDetail { StatusText = "Restoring UAC notification level to default...", IsIndeterminate = false, Progress = 0 });

                // Set UAC notification level to Moderate (1)
                UacLevel = 1;
                progress.Report(new TaskProgressDetail { StatusText = "Applying UAC notification level...", IsIndeterminate = false, Progress = 0.5 });

                // Apply the changes
                await ApplySettingsAsync(progress);

                progress.Report(new TaskProgressDetail { StatusText = "UAC notification level restored to default", IsIndeterminate = false, Progress = 1.0 });
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

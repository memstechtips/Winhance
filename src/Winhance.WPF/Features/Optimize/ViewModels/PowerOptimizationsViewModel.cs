using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.Core.Features.Optimize.Models;
using Winhance.WPF.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.ViewModels;
namespace Winhance.WPF.Features.Optimize.ViewModels
{
    /// <summary>
    /// ViewModel for Power optimizations using clean architecture principles.
    /// Directly manages settings using domain services without UI coordinator.
    /// </summary>
    public partial class PowerOptimizationsViewModel : BaseSettingsFeatureViewModel
    {
        private readonly IPowerService _powerService;
        private readonly IDialogService _dialogService;
        private readonly ISettingApplicationService _settingApplicationService;
        private readonly IEventBus _eventBus;
        private readonly ITaskProgressService _progressService;
        private readonly ISettingsConfirmationService _confirmationService;

        /// <summary>
        /// Gets or sets a value indicating whether the system has a battery.
        /// </summary>
        [ObservableProperty]
        private bool _hasBattery;

        /// <summary>
        /// Gets or sets a value indicating whether the system has a lid.
        /// </summary>
        [ObservableProperty]
        private bool _hasLid;

        /// <summary>
        /// Gets or sets the power plan value.
        /// </summary>
        [ObservableProperty]
        private int _powerPlanValue;

        /// <summary>
        /// Gets or sets a value indicating whether a power plan is being applied.
        /// </summary>
        [ObservableProperty]
        private bool _isApplyingPowerPlan;

        /// <summary>
        /// Gets or sets the advanced power setting groups.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasAdvancedPowerSettings))]
        private ObservableCollection<AdvancedPowerSettingGroup> _advancedPowerSettingGroups = new();

        /// <summary>
        /// Gets a value indicating whether there are any advanced power settings available.
        /// </summary>
        public bool HasAdvancedPowerSettings => AdvancedPowerSettingGroups.Count > 0;

        /// <summary>
        /// Gets or sets a value indicating whether advanced power settings are being loaded.
        /// </summary>
        [ObservableProperty]
        private bool _isLoadingAdvancedSettings;

        /// <summary>
        /// Gets or sets the selected power plan.
        /// </summary>
        [ObservableProperty]
        private PowerPlan _selectedPowerPlan;

        /// <summary>
        /// Called when SelectedPowerPlan property changes.
        /// </summary>
        partial void OnSelectedPowerPlanChanged(PowerPlan value)
        {
            // Auto-apply power plan when selection changes (not during initial load)
            if (value != null && !_loadingState)
            {
                _ = Task.Run(async () => await ApplyPowerPlanAsync());
            }
        }

        /// <summary>
        /// Gets or sets the available power plans.
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<PowerPlan> _powerPlans = new();

        /// <summary>
        /// Gets or sets the selected display timeout index.
        /// </summary>
        [ObservableProperty]
        private int _selectedDisplayTimeoutIndex = 15; // Default to "Never"

        /// <summary>
        /// Gets or sets the selected sleep timeout index.
        /// </summary>
        [ObservableProperty]
        private int _selectedSleepTimeoutIndex = 15; // Default to "Never"

        /// <summary>
        /// Indicates if the ViewModel is currently loading (to prevent auto-apply during initial load).
        /// </summary>
        private bool _loadingState = true;

        /// <summary>
        /// Called when SelectedDisplayTimeoutIndex property changes.
        /// </summary>
        partial void OnSelectedDisplayTimeoutIndexChanged(int value)
        {
            if (!_loadingState)
            {
                _ = Task.Run(async () => await ApplyDisplayTimeoutAsync());
            }
        }

        /// <summary>
        /// Called when SelectedSleepTimeoutIndex property changes.
        /// </summary>
        partial void OnSelectedSleepTimeoutIndexChanged(int value)
        {
            if (!_loadingState)
            {
                _ = Task.Run(async () => await ApplySleepTimeoutAsync());
            }
        }

        /// <summary>
        /// Gets or sets the timeout options for display and sleep settings.
        /// Loaded from domain service following clean architecture principles.
        /// </summary>
        [ObservableProperty]
        private List<string> _timeoutOptions = new List<string>();

        /// <summary>
        /// Command to apply power plan changes.
        /// </summary>
        public ICommand ApplyPowerPlanCommand { get; }

        /// <summary>
        /// Command to apply display timeout changes.
        /// </summary>
        public ICommand ApplyDisplayTimeoutCommand { get; }

        /// <summary>
        /// Command to apply sleep timeout changes.
        /// </summary>
        public ICommand ApplySleepTimeoutCommand { get; }

        public override string ModuleId => "Power";
        public override string DisplayName => "Power";
        public override string Category => "Optimize";
        public override string Description => "Optimize Windows power settings";
        public override int SortOrder => 4;

        /// <summary>
        /// Initializes a new instance of the <see cref="PowerOptimizationsViewModel"/> class.
        /// </summary>
        /// <param name="powerService">The power domain service.</param>
        /// <param name="settingApplicationService">The setting application service.</param>
        /// <param name="progressService">The task progress service.</param>
        /// <param name="logService">The log service.</param>
        /// <param name="dialogService">The dialog service.</param>
        public PowerOptimizationsViewModel(
            IDomainServiceRouter domainServiceRouter,
            ISettingsLoadingService settingsLoadingService,
            ILogService logService,
            IPowerService powerService,
            ISettingApplicationService settingApplicationService,
            IEventBus eventBus,
            ITaskProgressService progressService,
            IDialogService dialogService,
            ISettingsConfirmationService confirmationService
        ) : base(domainServiceRouter, settingsLoadingService, logService)
        {
            _powerService = powerService ?? throw new ArgumentNullException(nameof(powerService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _settingApplicationService = settingApplicationService ?? throw new ArgumentNullException(nameof(settingApplicationService));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _progressService = progressService ?? throw new ArgumentNullException(nameof(progressService));
            _confirmationService = confirmationService ?? throw new ArgumentNullException(nameof(confirmationService));

            // Initialize commands
            ApplyPowerPlanCommand = new AsyncRelayCommand(ApplyPowerPlanAsync);
            ApplyDisplayTimeoutCommand = new AsyncRelayCommand(ApplyDisplayTimeoutAsync);
            ApplySleepTimeoutCommand = new AsyncRelayCommand(ApplySleepTimeoutAsync);
        }

        /// <summary>
        /// Loads settings and initializes the UI state.
        /// </summary>
        public override async Task LoadSettingsAsync()
        {
            try
            {
                _loadingState = true; // Prevent auto-apply during loading
                _progressService.StartTask("Loading power optimization settings...");
                IsLoadingAdvancedSettings = true; // Start loading advanced settings

                // Load available power plans for the power plan selector
                var powerPlans = await _powerService.GetAvailablePowerPlansAsync();
                PowerPlans.Clear();
                foreach (var plan in powerPlans.Cast<PowerPlan>())
                {
                    PowerPlans.Add(plan);
                }

                // Set the active power plan as selected
                var activePlan = PowerPlans.FirstOrDefault(p => p.IsActive);
                if (activePlan != null)
                {
                    SelectedPowerPlan = activePlan;
                    _logService.Log(LogLevel.Info, $"Set active power plan: {activePlan.Name}");
                }
                else
                {
                    _logService.Log(LogLevel.Warning, "No active power plan found in power plans collection");
                }

                // Check system capabilities
                var capabilities = await _powerService.CheckPowerSystemCapabilitiesAsync();
                HasBattery = capabilities.GetValueOrDefault("HasBattery", false);
                HasLid = capabilities.GetValueOrDefault("HasLid", false);

                // Load timeout options from domain service (clean architecture compliance)
                TimeoutOptions = _powerService.GetTimeoutOptions().ToList();

                // Load current system power settings (display timeout, sleep timeout)
                await LoadCurrentSystemSettingsAsync();

                // Load settings for the new architecture
                await LoadPowerSettingsAsync();

                // Subscribe to property changes on dynamic settings for auto-apply
                SubscribeToSettingsChanges();

                // Advanced settings are now loaded
                IsLoadingAdvancedSettings = false;

                // CRITICAL FIX: Delay enabling auto-apply until after UI binding completes
                // This prevents automatic setting application during UI initialization
                _ = Task.Run(async () =>
                {
                    // Wait for UI thread to complete all pending binding operations
                    await Task.Delay(500); // Allow UI binding to complete

                    // Now it's safe to enable auto-apply for user interactions
                    _loadingState = false;
                    _logService.Log(LogLevel.Info, "[PowerVM] Auto-apply enabled after UI initialization completed");
                });

                _progressService.CompleteTask();
                _logService.Log(LogLevel.Info, "Power optimization settings loaded successfully");
            }
            catch (Exception ex)
            {
                _loadingState = false; // Enable auto-apply even on error
                IsLoadingAdvancedSettings = false; // Stop loading on error
                _progressService.CompleteTask();
                _logService.Log(LogLevel.Error, $"Error loading power optimization settings: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Checks the system capabilities for power management.
        /// </summary>
        private async Task CheckSystemCapabilitiesAsync()
        {
            try
            {
                // For now, assume desktop systems don't have battery/lid
                // This can be enhanced later with proper system detection
                HasBattery = false;
                HasLid = false;

                _logService.Log(LogLevel.Info, $"System capabilities: Battery={HasBattery}, Lid={HasLid}");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error checking system capabilities: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the application settings that this ViewModel manages.
        /// </summary>
        /// <returns>Collection of application settings for power optimizations.</returns>
        private async Task<IEnumerable<SettingDefinition>> GetSettingDefinitionsAsync()
        {
            return await _powerService.GetSettingsAsync();
        }

        /// <summary>
        /// Refreshes the settings for this feature asynchronously.
        /// </summary>

        /// <summary>
        /// Applies the selected power plan to the system.
        /// </summary>
        private async Task ApplyPowerPlanAsync()
        {
            if (SelectedPowerPlan == null) return;

            try
            {
                IsApplyingPowerPlan = true;
                _logService.Log(LogLevel.Info, $"Applying power plan: {SelectedPowerPlan.Name}");

                // Use the clean GUID (should not contain [Active] suffix)
                string cleanGuid = SelectedPowerPlan.Guid;
                bool success = await _powerService.SetActivePowerPlanAsync(cleanGuid);

                if (success)
                {
                    _logService.Log(LogLevel.Info, $"Successfully applied power plan: {SelectedPowerPlan.Name}");
                    // Refresh the power plans to update [Active] tags
                    await RefreshPowerPlansAsync();
                }
                else
                {
                    _logService.Log(LogLevel.Error, $"Failed to apply power plan: {SelectedPowerPlan.Name}");
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error applying power plan: {ex.Message}");
            }
            finally
            {
                IsApplyingPowerPlan = false;
            }
        }

        /// <summary>
        /// Applies the selected display timeout to the system.
        /// </summary>
        private async Task ApplyDisplayTimeoutAsync()
        {
            try
            {
                _logService.Log(LogLevel.Info, $"Applying display timeout: {TimeoutOptions[SelectedDisplayTimeoutIndex]}");

                int timeoutMinutes = ConvertTimeoutIndexToMinutes(SelectedDisplayTimeoutIndex);
                int timeoutSeconds = timeoutMinutes * 60; // Convert minutes to seconds
                await ApplyPowerSettingAsync("3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e", "7516b95f-f776-4464-8c53-06167f40cc99", "3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e", timeoutSeconds);

                _logService.Log(LogLevel.Info, $"Successfully applied display timeout: {timeoutMinutes} minutes ({timeoutSeconds} seconds)");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error applying display timeout: {ex.Message}");
            }
        }

        /// <summary>
        /// Applies the selected sleep timeout to the system.
        /// </summary>
        private async Task ApplySleepTimeoutAsync()
        {
            try
            {
                _logService.Log(LogLevel.Info, $"Applying sleep timeout: {TimeoutOptions[SelectedSleepTimeoutIndex]}");

                int timeoutMinutes = ConvertTimeoutIndexToMinutes(SelectedSleepTimeoutIndex);
                int timeoutSeconds = timeoutMinutes * 60; // Convert minutes to seconds
                await ApplyPowerSettingAsync("238c9fa8-0aad-41ed-83f4-97be242c8f20", "238c9fa8-0aad-41ed-83f4-97be242c8f20", "29f6c1db-86da-48c5-9fdb-f2b67b1f44da", timeoutSeconds);

                _logService.Log(LogLevel.Info, $"Successfully applied sleep timeout: {timeoutMinutes} minutes ({timeoutSeconds} seconds)");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error applying sleep timeout: {ex.Message}");
            }
        }

        /// <summary>
        /// Refreshes the power plans list and updates the selected plan.
        /// </summary>
        private async Task RefreshPowerPlansAsync()
        {
            try
            {
                var powerPlans = await _powerService.GetAvailablePowerPlansAsync();
                PowerPlans.Clear();
                foreach (var plan in powerPlans.Cast<PowerPlan>())
                {
                    PowerPlans.Add(plan);
                }

                // Update selected plan to the active one
                var activePlan = PowerPlans.FirstOrDefault(p => p.IsActive);
                if (activePlan != null)
                {
                    SelectedPowerPlan = activePlan;
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error refreshing power plans: {ex.Message}");
            }
        }

        /// <summary>
        /// Converts timeout option index to minutes.
        /// </summary>
        private int ConvertTimeoutIndexToMinutes(int index)
        {
            return index switch
            {
                0 => 1,      // 1 minute
                1 => 2,      // 2 minutes
                2 => 3,      // 3 minutes
                3 => 5,      // 5 minutes
                4 => 10,     // 10 minutes
                5 => 15,     // 15 minutes
                6 => 20,     // 20 minutes
                7 => 25,     // 25 minutes
                8 => 30,     // 30 minutes
                9 => 45,     // 45 minutes
                10 => 60,    // 1 hour
                11 => 120,   // 2 hours
                12 => 180,   // 3 hours
                13 => 240,   // 4 hours
                14 => 300,   // 5 hours
                15 => 0,     // Never
                _ => 0       // Default to Never
            };
        }

        /// <summary>
        /// Converts minutes to timeout option index.
        /// </summary>
        private int ConvertMinutesToTimeoutIndex(int minutes)
        {
            return minutes switch
            {
                1 => 0,      // 1 minute
                2 => 1,      // 2 minutes
                3 => 2,      // 3 minutes
                5 => 3,      // 5 minutes
                10 => 4,     // 10 minutes
                15 => 5,     // 15 minutes
                20 => 6,     // 20 minutes
                25 => 7,     // 25 minutes
                30 => 8,     // 30 minutes
                45 => 9,     // 45 minutes
                60 => 10,    // 1 hour
                120 => 11,   // 2 hours
                180 => 12,   // 3 hours
                240 => 13,   // 4 hours
                300 => 14,   // 5 hours
                0 => 15,     // Never
                _ => 15      // Default to Never
            };
        }

        /// <summary>
        /// Applies a power setting to the system.
        /// </summary>
        private async Task ApplyPowerSettingAsync(string powerPlanGuid, string subgroupGuid, string settingGuid, int value)
        {
            var activePlan = await _powerService.GetActivePowerPlanAsync();
            if (activePlan != null)
            {
                // Use the clean GUID (should not contain [Active] suffix)
                string cleanGuid = activePlan.Guid;
                await _powerService.ApplyAdvancedPowerSettingAsync(cleanGuid, subgroupGuid, settingGuid, value, value);
            }
        }

        /// <summary>
        /// Subscribes to property changes on dynamic power settings for auto-apply functionality.
        /// </summary>
        private void SubscribeToSettingsChanges()
        {
            if (Settings == null)
            {
                _logService.Log(LogLevel.Warning, "[PowerVM] No settings available for subscription");
                return;
            }

            _logService.Log(LogLevel.Info, $"[PowerVM] Setting up callbacks for {Settings.Count} power settings");

            // Set up handlers for each setting using property change notifications
            foreach (var setting in Settings)
            {
                _logService.Log(LogLevel.Debug, $"[PowerVM] Setting up callbacks for setting: {setting.SettingId} - {setting.Name}");

                // Subscribe to IsSelected property changes for toggle controls
                setting.PropertyChanged += async (sender, e) =>
                {
                    if (e.PropertyName == nameof(setting.IsSelected))
                    {
                        _logService.Log(LogLevel.Info, $"[PowerVM] IsSelected changed for {setting.SettingId}: IsLoading={_loadingState}, IsSelected={setting.IsSelected}");

                        // Only apply changes after initial loading is complete
                        if (_loadingState)
                        {
                            _logService.Log(LogLevel.Info, $"[PowerVM] Skipping application for {setting.SettingId} - still loading");
                            return;
                        }

                        // Delegate to domain service - no business logic in ViewModel
                        await ApplyDynamicPowerSettingAsync(setting.SettingId, setting.IsSelected, null);
                    }
                    else if (e.PropertyName == nameof(setting.SelectedValue))
                    {
                        _logService.Log(LogLevel.Info, $"[PowerVM] SelectedValue changed for {setting.SettingId}: IsLoading={_loadingState}, NewValue={setting.SelectedValue}, ValueType={setting.SelectedValue?.GetType().Name ?? "null"}");

                        // Only apply changes after initial loading is complete
                        if (_loadingState)
                        {
                            _logService.Log(LogLevel.Info, $"[PowerVM] Skipping value application for {setting.SettingId} - still loading");
                            return;
                        }

                        // Delegate to domain service - no business logic in ViewModel
                        await ApplyDynamicPowerSettingAsync(setting.SettingId, setting.IsSelected, setting.SelectedValue);
                    }
                };
            }

            _logService.Log(LogLevel.Info, $"[PowerVM] Successfully subscribed to property changes for {Settings.Count} dynamic power settings");
        }

        /// <summary>
        /// Applies a dynamic power setting change by delegating to the domain service.
        /// ViewModel only handles UI coordination - all business logic in domain service.
        /// </summary>
        private async Task ApplyDynamicPowerSettingAsync(string settingId, bool isSelected, object? selectedValue)
        {
            try
            {
                _logService.Log(LogLevel.Info, $"[PowerVM] Applying dynamic power setting change - ID: {settingId}, Selected: {isSelected}, Value: {selectedValue}, ValueType: {selectedValue?.GetType().Name ?? "null"}");

                // Delegate all business logic to domain service (SOLID compliance)
                await _powerService.ApplyDynamicPowerSettingAsync(settingId, isSelected, selectedValue);

                _logService.Log(LogLevel.Info, $"[PowerVM] Successfully applied dynamic power setting change for ID: {settingId}");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"[PowerVM] Error applying dynamic power setting change for ID: {settingId} - {ex.Message}");
                _logService.Log(LogLevel.Error, $"[PowerVM] Stack trace: {ex.StackTrace}");

                // ViewModel only handles UI concerns - user notification
                await _dialogService.ShowErrorAsync(
                    "Power Setting Error",
                    $"Failed to apply power setting: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads power settings using the new architecture.
        /// </summary>
        private async Task LoadPowerSettingsAsync()
        {
            try
            {
                // Get settings from domain service
                var SettingDefinitions = await _powerService.GetSettingsAsync();

                Settings.Clear();
                foreach (var setting in SettingDefinitions)
                {
                    // Create SettingItemViewModel for each setting
                    var settingViewModel = new SettingItemViewModel(_settingApplicationService, _eventBus, _logService, _confirmationService, _domainServiceRouter)
                    {
                        SettingId = setting.Id,
                        Name = setting.Name,
                        Description = setting.Description,
                        GroupName = setting.GroupName,
                        InputType = setting.InputType,
                        Icon = setting.Icon,
                        RequiresConfirmation = setting.RequiresConfirmation,
                        ConfirmationTitle = setting.ConfirmationTitle,
                        ConfirmationMessage = setting.ConfirmationMessage,
                        ActionCommandName = setting.ActionCommand
                    };

                    // Get current state from system
                    var currentState = await _settingApplicationService.GetSettingStateAsync(setting.Id);
                    settingViewModel.IsSelected = currentState.IsEnabled;
                    settingViewModel.SelectedValue = currentState.CurrentValue;

                    // Handle ComboBox options if applicable
                    if (setting.InputType == SettingInputType.Selection && setting.CustomProperties.ContainsKey("Options"))
                    {
                        var options = setting.CustomProperties["Options"] as System.Collections.IEnumerable;
                        if (options != null)
                        {
                            foreach (var option in options)
                            {
                                settingViewModel.ComboBoxOptions.Add(new ComboBoxOption
                                {
                                    DisplayText = option.ToString() ?? string.Empty,
                                    Value = option
                                });
                            }
                        }
                    }

                    Settings.Add(settingViewModel);
                }

                _logService.Log(
                    LogLevel.Info,
                    $"PowerOptimizationsViewModel: Loaded {Settings.Count} settings"
                );
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error loading power settings: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Loads current system power settings including display timeout and sleep timeout.
        /// </summary>
        private async Task LoadCurrentSystemSettingsAsync()
        {
            try
            {
                var activePlan = await _powerService.GetActivePowerPlanAsync();
                if (activePlan == null) return;

                // Use the clean GUID (should not contain [Active] suffix)
                string cleanGuid = activePlan.Guid;

                // Load display timeout setting (GUID format: power plan, subgroup, setting)
                var (displayAcValue, displayDcValue) = await _powerService.GetSettingValueAsync(
                    cleanGuid,
                    "7516b95f-f776-4464-8c53-06167f40cc99",
                    "3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e");
                int displayMinutes = displayAcValue / 60; // Convert seconds to minutes
                SelectedDisplayTimeoutIndex = ConvertMinutesToTimeoutIndex(displayMinutes);

                // Load sleep timeout setting (GUID format: power plan, subgroup, setting)
                var (sleepAcValue, sleepDcValue) = await _powerService.GetSettingValueAsync(
                    cleanGuid,
                    "238c9fa8-0aad-41ed-83f4-97be242c8f20",
                    "29f6c1db-86da-48c5-9fdb-f2b67b1f44da");
                int sleepMinutes = sleepAcValue / 60; // Convert seconds to minutes
                SelectedSleepTimeoutIndex = ConvertMinutesToTimeoutIndex(sleepMinutes);

                _logService.Log(LogLevel.Info, $"Loaded current system settings - Display: {displayMinutes}min, Sleep: {sleepMinutes}min");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error loading current system settings: {ex.Message}");
            }
        }
    }
}

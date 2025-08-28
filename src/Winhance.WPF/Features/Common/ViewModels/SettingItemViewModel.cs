using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.Features;
using Winhance.Core.Features.Common.Events.UI;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.WPF.Features.Common.ViewModels
{
    /// <summary>
    /// Universal ViewModel for individual settings across all features.
    /// Works with both Customize and Optimize features, handling all control types.
    /// </summary>
    public partial class SettingItemViewModel : ObservableObject, IDisposable
    {
        private readonly ISettingApplicationService _settingApplicationService;
        private readonly IEventBus _eventBus;
        private readonly ILogService _logService;
        private ISubscriptionToken? _tooltipUpdatedSubscription;
        private ISubscriptionToken? _tooltipsBulkLoadedSubscription;
        private ISubscriptionToken? _featureComposedSubscription;
        private bool _isInitializing = true;

        [ObservableProperty]
        private string _settingId = string.Empty;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private string _groupName = string.Empty;

        [ObservableProperty]
        private bool _isSelected;

        /// <summary>
        /// Called when IsSelected property changes. Automatically applies the setting.
        /// Only triggers during user interaction, not during initialization.
        /// </summary>
        partial void OnIsSelectedChanged(bool value)
        {
            if (IsApplying || _isInitializing)
                return;

            _ = Task.Run(async () => await HandleToggleAsync());
        }

        [ObservableProperty]
        private bool _isApplying;

        [ObservableProperty]
        private string _status = string.Empty;

        [ObservableProperty]
        private SettingInputType _inputType;

        [ObservableProperty]
        private object? _selectedValue;

        /// <summary>
        /// Called when SelectedValue property changes. Automatically applies the setting.
        /// Only triggers during user interaction, not during initialization.
        /// </summary>
        partial void OnSelectedValueChanged(object? value)
        {
            if (IsApplying || _isInitializing)
                return;

            _ = Task.Run(async () => await HandleValueChangedAsync(value));
        }

        [ObservableProperty]
        private ObservableCollection<Winhance.Core.Features.Common.Interfaces.ComboBoxOption> _comboBoxOptions =
            new();

        [ObservableProperty]
        private bool _isVisible = true;

        [ObservableProperty]
        private bool _isEnabled = true;

        [ObservableProperty]
        private string? _icon;

        [ObservableProperty]
        private bool _requiresConfirmation;

        [ObservableProperty]
        private string? _confirmationTitle;

        [ObservableProperty]
        private string? _confirmationMessage;

        [ObservableProperty]
        private string? _actionCommandName;

        [ObservableProperty]
        private SettingTooltipData? _tooltipData;

        public IAsyncRelayCommand ToggleCommand { get; }
        public IAsyncRelayCommand<object> ValueChangedCommand { get; }
        public IAsyncRelayCommand ActionCommand { get; }

        /// <summary>
        /// Sets up ComboBox options using the centralized service.
        /// Eliminates duplication across all ViewModels.
        /// </summary>
        public void SetupComboBox(
            SettingDefinition setting,
            object? currentValue,
            IComboBoxSetupService comboBoxSetupService,
            ILogService logService
        )
        {
            if (setting.InputType != SettingInputType.Selection)
                return;

            var comboBoxSetupResult = comboBoxSetupService.SetupComboBoxOptions(
                setting,
                currentValue
            );
            if (comboBoxSetupResult.Success)
            {
                // Copy options from service result to ViewModel
                foreach (var option in comboBoxSetupResult.Options)
                {
                    ComboBoxOptions.Add(
                        new Winhance.Core.Features.Common.Interfaces.ComboBoxOption
                        {
                            DisplayText = option.DisplayText,
                            Value = option.Value,
                            Description = option.Description,
                        }
                    );
                }
                SelectedValue = comboBoxSetupResult.SelectedValue;
                logService.Log(
                    LogLevel.Info,
                    $"SettingItemViewModel: ComboBox setup completed for '{SettingId}' with {comboBoxSetupResult.Options.Count} options"
                );
            }
            else
            {
                logService.Log(
                    LogLevel.Warning,
                    $"SettingItemViewModel: ComboBox setup failed for '{SettingId}': {comboBoxSetupResult.ErrorMessage}"
                );
                SelectedValue = currentValue;
            }
        }

        public SettingItemViewModel(
            ISettingApplicationService settingService,
            IEventBus eventBus,
            ILogService logService
        )
        {
            _settingApplicationService =
                settingService ?? throw new ArgumentNullException(nameof(settingService));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));

            ToggleCommand = new AsyncRelayCommand(HandleToggleAsync);
            ValueChangedCommand = new AsyncRelayCommand<object>(HandleValueChangedAsync);
            ActionCommand = new AsyncRelayCommand(HandleActionAsync);

            // Subscribe to events
            _tooltipUpdatedSubscription = _eventBus.Subscribe<TooltipUpdatedEvent>(
                HandleTooltipUpdated
            );
            _tooltipsBulkLoadedSubscription = _eventBus.Subscribe<TooltipsBulkLoadedEvent>(
                HandleTooltipsBulkLoaded
            );
            _featureComposedSubscription = _eventBus.Subscribe<FeatureComposedEvent>(
                HandleFeatureComposed
            );
        }

        /// <summary>
        /// Handles binary toggle changes (BinaryToggle control type).
        /// </summary>
        private async Task HandleToggleAsync()
        {
            if (IsApplying)
                return;

            IsApplying = true;
            Status = "Applying...";

            try
            {
                // Use existing ISettingApplicationService - proper layer separation
                await _settingApplicationService.ApplySettingAsync(SettingId, IsSelected);

                Status = "Applied";
                _logService.Log(
                    LogLevel.Info,
                    $"Successfully applied setting {SettingId} with value {IsSelected}"
                );
            }
            catch (Exception ex)
            {
                Status = "Error";
                IsSelected = !IsSelected; // Revert UI state on error
                _logService.Log(
                    LogLevel.Error,
                    $"Exception applying setting {SettingId}: {ex.Message}"
                );
            }
            finally
            {
                IsApplying = false;

                // Clear status after a delay
                _ = Task.Delay(3000)
                    .ContinueWith(
                        _ => Status = string.Empty,
                        TaskScheduler.FromCurrentSynchronizationContext()
                    );
            }
        }

        /// <summary>
        /// Handles value changes for ComboBox, NumericUpDown, and Slider control types.
        /// </summary>
        private async Task HandleValueChangedAsync(object? value)
        {
            if (IsApplying)
                return;

            var previousValue = SelectedValue;
            IsApplying = true;
            Status = "Applying...";

            try
            {
                // Use existing ISettingApplicationService with value parameter
                await _settingApplicationService.ApplySettingAsync(
                    SettingId,
                    IsSelected,
                    SelectedValue
                );

                Status = "Applied";
                _logService.Log(
                    LogLevel.Info,
                    $"Successfully applied setting {SettingId} with value {value}"
                );
            }
            catch (Exception ex)
            {
                Status = "Error";
                SelectedValue = previousValue; // Revert to previous value on error
                _logService.Log(
                    LogLevel.Error,
                    $"Exception applying setting {SettingId}: {ex.Message}"
                );
            }
            finally
            {
                IsApplying = false;

                // Clear status after a delay
                _ = Task.Delay(3000)
                    .ContinueWith(
                        _ => Status = string.Empty,
                        TaskScheduler.FromCurrentSynchronizationContext()
                    );
            }
        }

        /// <summary>
        /// Handles action button clicks (ActionButton control type).
        /// </summary>
        private async Task HandleActionAsync()
        {
            if (IsApplying || string.IsNullOrEmpty(ActionCommandName))
                return;

            _logService.Log(
                LogLevel.Debug,
                $"SettingItemViewModel: Action requested for {SettingId}, command: {ActionCommandName}"
            );

            IsApplying = true;
            Status = "Executing...";

            try
            {
                // Use existing ISettingApplicationService to execute action commands
                await _settingApplicationService.ExecuteActionCommandAsync(
                    SettingId,
                    ActionCommandName
                );

                Status = "Completed";
                _logService.Log(
                    LogLevel.Info,
                    $"Successfully executed action {ActionCommandName} for setting {SettingId}"
                );
            }
            catch (Exception ex)
            {
                Status = "Error";
                _logService.Log(
                    LogLevel.Error,
                    $"Exception executing action {ActionCommandName} for setting {SettingId}: {ex.Message}"
                );
            }
            finally
            {
                IsApplying = false;

                // Clear status after a delay
                _ = Task.Delay(3000)
                    .ContinueWith(
                        _ => Status = string.Empty,
                        TaskScheduler.FromCurrentSynchronizationContext()
                    );
            }
        }

        /// <summary>
        /// Refreshes the current state of this setting from the system.
        /// </summary>
        public async Task RefreshStateAsync()
        {
            try
            {
                var result = await _settingApplicationService.GetSettingStateAsync(SettingId);

                if (result.Success)
                {
                    IsSelected = result.IsEnabled;
                    SelectedValue = result.CurrentValue;
                }
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Failed to refresh state for setting {SettingId}: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Handles TooltipUpdatedEvent for individual setting tooltip updates.
        /// </summary>
        private void HandleTooltipUpdated(TooltipUpdatedEvent evt)
        {
            if (evt.SettingId == SettingId)
            {
                TooltipData = evt.TooltipData;
                _logService.Log(
                    LogLevel.Debug,
                    $"SettingItemViewModel: Updated tooltip data for setting '{SettingId}'"
                );
            }
        }

        /// <summary>
        /// Handles TooltipsBulkLoadedEvent for bulk tooltip initialization.
        /// </summary>
        private void HandleTooltipsBulkLoaded(TooltipsBulkLoadedEvent evt)
        {
            if (evt.TooltipDataCollection.TryGetValue(SettingId, out var tooltipData))
            {
                TooltipData = tooltipData;
                _logService.Log(
                    LogLevel.Debug,
                    $"SettingItemViewModel: Initialized tooltip data for setting '{SettingId}'"
                );
            }
        }

        /// <summary>
        /// Handles FeatureComposedEvent to complete initialization when feature loading is done.
        /// </summary>
        private void HandleFeatureComposed(FeatureComposedEvent evt)
        {
            // Check if this setting belongs to the composed feature
            if (evt.Settings.Any(s => s.Id == SettingId))
            {
                _isInitializing = false;
                _logService.Log(
                    LogLevel.Debug,
                    $"SettingItemViewModel: Completed initialization for setting '{SettingId}' via FeatureComposedEvent"
                );
            }
        }

        /// <summary>
        /// Updates visibility based on search text.
        /// </summary>
        public void UpdateVisibility(string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                IsVisible = true;
                return;
            }

            var searchLower = searchText.ToLowerInvariant();
            IsVisible =
                Name.ToLowerInvariant().Contains(searchLower)
                || Description.ToLowerInvariant().Contains(searchLower)
                || GroupName.ToLowerInvariant().Contains(searchLower);
        }

        /// <summary>
        /// Disposes of event subscriptions to prevent memory leaks.
        /// </summary>
        public void Dispose()
        {
            _tooltipUpdatedSubscription?.Dispose();
            _tooltipsBulkLoadedSubscription?.Dispose();
            _featureComposedSubscription?.Dispose();
            _logService.Log(
                LogLevel.Debug,
                $"SettingItemViewModel: Disposed event subscriptions for setting '{SettingId}'"
            );
        }
    }
}

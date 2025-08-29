using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.Features;
using Winhance.Core.Features.Common.Events.Settings;
using Winhance.Core.Features.Common.Events.UI;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.WPF.Features.Common.Interfaces;

namespace Winhance.WPF.Features.Common.ViewModels
{
    /// <summary>
    /// Universal ViewModel for individual settings across all features.
    /// Works with both Customize and Optimize features, handling all control types.
    /// </summary>
    public partial class SettingItemViewModel : ObservableObject, ISearchable, IDisposable
    {
        private readonly ISettingApplicationService _settingApplicationService;
        private readonly IEventBus _eventBus;
        private readonly ILogService _logService;
        private readonly ISettingsConfirmationService _confirmationService;
        private readonly IDomainServiceRouter _domainServiceRouter;
        private ISubscriptionToken? _tooltipUpdatedSubscription;
        private ISubscriptionToken? _tooltipsBulkLoadedSubscription;
        private ISubscriptionToken? _featureComposedSubscription;
        private ISubscriptionToken? _settingAppliedSubscription;
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
            ILogService logService,
            ISettingsConfirmationService confirmationService,
            IDomainServiceRouter domainServiceRouter
        )
        {
            _settingApplicationService =
                settingService ?? throw new ArgumentNullException(nameof(settingService));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _confirmationService = confirmationService ?? throw new ArgumentNullException(nameof(confirmationService));
            _domainServiceRouter = domainServiceRouter ?? throw new ArgumentNullException(nameof(domainServiceRouter));

            ToggleCommand = new AsyncRelayCommand(HandleToggleAsync);
            ValueChangedCommand = new AsyncRelayCommand<object>(HandleValueChangedAsync);
            ActionCommand = new AsyncRelayCommand(HandleActionAsync);

            _tooltipUpdatedSubscription = _eventBus.Subscribe<TooltipUpdatedEvent>(
                HandleTooltipUpdated
            );
            _tooltipsBulkLoadedSubscription = _eventBus.Subscribe<TooltipsBulkLoadedEvent>(
                HandleTooltipsBulkLoaded
            );
            _featureComposedSubscription = _eventBus.Subscribe<FeatureComposedEvent>(
                HandleFeatureComposed
            );
            _settingAppliedSubscription = _eventBus.Subscribe<SettingAppliedEvent>(
                HandleSettingApplied
            );
        }

        private async Task HandleToggleAsync()
        {
            if (IsApplying)
                return;

            IsApplying = true;
            Status = "Applying...";

            try
            {
                var (canProceed, applyWallpaper) = await HandleConfirmationIfNeeded(IsSelected);
                if (!canProceed)
                {
                    IsSelected = !IsSelected;
                    Status = string.Empty;
                    return;
                }

                if (SettingId == "theme-mode-windows")
                {
                    await _settingApplicationService.ApplySettingAsync(SettingId, IsSelected, SelectedValue, applyWallpaper);
                }
                else
                {
                    await _settingApplicationService.ApplySettingAsync(SettingId, IsSelected);
                }

                Status = "Applied";
            }
            catch (Exception ex)
            {
                Status = "Error";
                IsSelected = !IsSelected;
                _logService.Log(LogLevel.Error, $"Exception applying setting {SettingId}: {ex.Message}");
            }
            finally
            {
                IsApplying = false;
                _ = Task.Delay(3000).ContinueWith(_ => Status = string.Empty, TaskScheduler.FromCurrentSynchronizationContext());
            }
        }

        private async Task HandleValueChangedAsync(object? value)
        {
            if (IsApplying)
                return;

            var previousValue = SelectedValue;
            IsApplying = true;
            Status = "Applying...";

            try
            {
                var (canProceed, applyWallpaper) = await HandleConfirmationIfNeeded(SelectedValue);
                if (!canProceed)
                {
                    SelectedValue = previousValue;
                    Status = string.Empty;
                    return;
                }

                if (SettingId == "theme-mode-windows")
                {
                    await _settingApplicationService.ApplySettingAsync(SettingId, IsSelected, SelectedValue, applyWallpaper);
                }
                else
                {
                    await _settingApplicationService.ApplySettingAsync(SettingId, IsSelected, SelectedValue);
                }

                Status = "Applied";
            }
            catch (Exception ex)
            {
                Status = "Error";
                SelectedValue = previousValue;
                _logService.Log(LogLevel.Error, $"Exception applying setting {SettingId}: {ex.Message}");
            }
            finally
            {
                IsApplying = false;
                _ = Task.Delay(3000).ContinueWith(_ => Status = string.Empty, TaskScheduler.FromCurrentSynchronizationContext());
            }
        }

        private async Task HandleActionAsync()
        {
            if (IsApplying || string.IsNullOrEmpty(ActionCommandName))
                return;

            IsApplying = true;
            Status = "Executing...";

            try
            {
                var (canProceed, applyRecommended) = await HandleConfirmationIfNeeded(null);
                if (!canProceed)
                {
                    Status = string.Empty;
                    return;
                }

                var context = new ActionExecutionContext
                {
                    SettingId = SettingId,
                    CommandString = ActionCommandName,
                    ApplyRecommendedSettings = applyRecommended
                };

                await _settingApplicationService.ExecuteActionCommandAsync(context);
                Status = "Completed";
            }
            catch (Exception ex)
            {
                Status = "Error";
                _logService.Log(LogLevel.Error, $"Exception executing action {ActionCommandName} for setting {SettingId}: {ex.Message}");
            }
            finally
            {
                IsApplying = false;
                _ = Task.Delay(3000).ContinueWith(_ => Status = string.Empty, TaskScheduler.FromCurrentSynchronizationContext());
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
                    // Temporarily supress property change events to avoid calls to domain services (only update UI)
                    _isInitializing = true;

                    IsSelected = result.IsEnabled;
                    SelectedValue = result.CurrentValue;

                    // Re-enable property change events
                    _isInitializing = false;
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
            if (evt.Settings.Any(s => s.Id == SettingId))
            {
                _isInitializing = false;
            }
        }

        public bool MatchesSearch(string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText)) return true;

            var searchLower = searchText.ToLowerInvariant();
            return Name.ToLowerInvariant().Contains(searchLower) ||
            Description.ToLowerInvariant().Contains(searchLower) ||
            GroupName.ToLowerInvariant().Contains(searchLower);
        }

        public string[] GetSearchableProperties()
        {
            return new[] { nameof(Name), nameof(Description), nameof(GroupName) };
        }

        /// <summary>
        /// Updates visibility based on search text. Called by BaseSettingsFeatureViewModel.
        /// </summary>
        public void UpdateVisibility(string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                IsVisible = true;
                return;
            }

            IsVisible = MatchesSearch(searchText);
        }

        private async Task<(bool canProceed, bool checkboxResult)> HandleConfirmationIfNeeded(object? value)
        {
            var setting = await GetSettingDefinition();
            if (setting?.RequiresConfirmation != true)
                return (true, false);

            var (confirmed, checkboxChecked) = await _confirmationService.HandleConfirmationAsync(SettingId, value, setting);
            return (confirmed, checkboxChecked);
        }

        private async Task<SettingDefinition?> GetSettingDefinition()
        {
            try
            {
                var domainService = _domainServiceRouter.GetDomainService(SettingId);
                var settings = await domainService.GetRawSettingsAsync();
                return settings.FirstOrDefault(s => s.Id == SettingId);
            }
            catch
            {
                return null;
            }
        }

        private async void HandleSettingApplied(SettingAppliedEvent evt)
        {
            if (evt.SettingId == SettingId)
            {
                await RefreshStateAsync();
            }
        }

        public void Dispose()
        {
            _tooltipUpdatedSubscription?.Dispose();
            _tooltipsBulkLoadedSubscription?.Dispose();
            _featureComposedSubscription?.Dispose();
            _settingAppliedSubscription?.Dispose();
        }
    }
}

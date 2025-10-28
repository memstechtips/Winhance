using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.Features;
using Winhance.Core.Features.Common.Events.Settings;
using Winhance.Core.Features.Common.Events.UI;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Utils;
using Winhance.WPF.Features.Common.Interfaces;
using System.Windows;

namespace Winhance.WPF.Features.Common.ViewModels
{
    public partial class SettingItemViewModel : ObservableObject, IDisposable
    {
        private readonly ISettingApplicationService _settingApplicationService;
        private readonly IEventBus _eventBus;
        private readonly ILogService _logService;
        private readonly ISettingsConfirmationService _confirmationService;
        private readonly IDomainServiceRouter _domainServiceRouter;
        private readonly IInitializationService _initializationService;
        private readonly IComboBoxSetupService _comboBoxSetupService;
        private readonly ISystemSettingsDiscoveryService _discoveryService;
        private readonly IUserPreferencesService _userPreferencesService;
        private readonly IDialogService _dialogService;
        private ISubscriptionToken? _tooltipUpdatedSubscription;
        private ISubscriptionToken? _tooltipsBulkLoadedSubscription;
        private ISubscriptionToken? _featureComposedSubscription;
        private ISubscriptionToken? _settingAppliedSubscription;
        public bool _isInitializing = true;
        private CancellationTokenSource? _debounceTokenSource;
        private bool _isApplyingNumericValue;
        private bool _isRefreshingComboBox = false;
        private object? _lastConfirmedSelectedValue;
        private object? _lastConfirmedACValue;
        private object? _lastConfirmedDCValue;
        private bool _hasChangedThisSession = false;

        public ISettingsFeatureViewModel? ParentFeatureViewModel { get; set; }
        public SettingDefinition? SettingDefinition { get; set; }

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
        private string? _warningText;

        [ObservableProperty]
        private InputType _inputType;

        [ObservableProperty]
        private object? _selectedValue;

        partial void OnSelectedValueChanged(object? value)
        {
            if (IsApplying || _isInitializing)
            {
                return;
            }

            _ = Task.Run(async () => await HandleValueChangedAsync(value));
        }

        [ObservableProperty]
        private ObservableCollection<Winhance.Core.Features.Common.Interfaces.ComboBoxOption> _comboBoxOptions =
            new();

        [ObservableProperty]
        private Winhance.Core.Features.Common.Interfaces.ComboBoxOption? _selectedComboBoxItem;

        [ObservableProperty]
        private int _numericValue;

        partial void OnNumericValueChanged(int value)
        {
            if (IsApplying || _isInitializing)
                return;

            if (_initializationService.IsGloballyInitializing)
            {
                _logService.Log(LogLevel.Error, $"NumericValue change blocked during global initialization: {SettingId}");
                return;
            }

            _debounceTokenSource?.Cancel();
            _debounceTokenSource = new CancellationTokenSource();

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(500, _debounceTokenSource.Token);
                    await HandleNumericValueChangedAsync(value);
                }
                catch (OperationCanceledException)
                {
                }
            });
        }

        [ObservableProperty]
        private int _minValue;

        [ObservableProperty]
        private int _maxValue = 100;

        [ObservableProperty]
        private string _units = string.Empty;

        [ObservableProperty]
        private bool _isVisible = true;

        [ObservableProperty]
        private bool _isEnabled = true;

        partial void OnIsEnabledChanged(bool value)
        {
            OnPropertyChanged(nameof(EffectiveIsEnabled));
        }

        [ObservableProperty]
        private bool _parentIsEnabled = true;

        partial void OnParentIsEnabledChanged(bool value)
        {
            OnPropertyChanged(nameof(EffectiveIsEnabled));
        }

        public bool EffectiveIsEnabled => IsEnabled && ParentIsEnabled;

        [ObservableProperty]
        private string? _icon;

        [ObservableProperty]
        private string? _iconPack = "Material";

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

        [ObservableProperty]
        private object? _aCValue;

        partial void OnACValueChanged(object? value)
        {
            if (IsApplying || _isInitializing)
                return;

            _ = Task.Run(async () => await HandleACValueChangedAsync(value));
        }

        [ObservableProperty]
        private object? _dCValue;

        partial void OnDCValueChanged(object? value)
        {
            if (IsApplying || _isInitializing)
                return;

            _ = Task.Run(async () => await HandleDCValueChangedAsync(value));
        }

        [ObservableProperty]
        private int _aCNumericValue;

        partial void OnACNumericValueChanged(int value)
        {
            if (IsApplying || _isInitializing)
                return;

            if (_initializationService.IsGloballyInitializing)
            {
                _logService.Log(LogLevel.Error, $"ACNumericValue change blocked during global initialization: {SettingId}");
                return;
            }

            _debounceTokenSource?.Cancel();
            _debounceTokenSource = new CancellationTokenSource();

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(500, _debounceTokenSource.Token);
                    await HandleACNumericValueChangedAsync(value);
                }
                catch (OperationCanceledException)
                {
                }
            });
        }

        [ObservableProperty]
        private int _dCNumericValue;

        partial void OnDCNumericValueChanged(int value)
        {
            if (IsApplying || _isInitializing)
                return;

            if (_initializationService.IsGloballyInitializing)
            {
                _logService.Log(LogLevel.Error, $"DCNumericValue change blocked during global initialization: {SettingId}");
                return;
            }

            _debounceTokenSource?.Cancel();
            _debounceTokenSource = new CancellationTokenSource();

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(500, _debounceTokenSource.Token);
                    await HandleDCNumericValueChangedAsync(value);
                }
                catch (OperationCanceledException)
                {
                }
            });
        }

        public bool IsSubSetting => !string.IsNullOrEmpty(SettingDefinition?.ParentSettingId);

        public bool SupportsSeparateACDC => SettingDefinition?.PowerCfgSettings?.Any(p =>
            p.PowerModeSupport == PowerModeSupport.Separate) == true;

        public bool RequiresAdvancedUnlock => SettingDefinition?.RequiresAdvancedUnlock == true;

        [ObservableProperty]
        private bool _isLocked;

        public IAsyncRelayCommand ToggleCommand { get; }
        public IAsyncRelayCommand<object> ValueChangedCommand { get; }
        public IAsyncRelayCommand ActionCommand { get; }
        public IAsyncRelayCommand UnlockCommand { get; }

        public void SetupNumericUpDown(SettingDefinition setting, object? currentValue, Dictionary<string, object?>? rawValues = null)
        {
            if (setting.InputType != InputType.NumericRange)
                return;

            _isInitializing = true;

            if (setting.CustomProperties != null)
            {
                MaxValue = setting.CustomProperties.TryGetValue("MaxValue", out var max) ? (int)max : int.MaxValue;
                MinValue = setting.CustomProperties.TryGetValue("MinValue", out var min) ? (int)min : 0;
                Units = setting.CustomProperties.TryGetValue("Units", out var units) ? (string)units : "";
            }

            if (SupportsSeparateACDC && rawValues != null)
            {
                if (rawValues.TryGetValue("ACValue", out var acVal) && acVal is int acIntValue)
                {
                    ACNumericValue = ConvertSystemValueToDisplayValue(setting, acIntValue);
                }

                if (rawValues.TryGetValue("DCValue", out var dcVal) && dcVal is int dcIntValue)
                {
                    DCNumericValue = ConvertSystemValueToDisplayValue(setting, dcIntValue);
                }
            }
            else if (currentValue is int intValue)
            {
                var displayValue = ConvertSystemValueToDisplayValue(setting, intValue);

                if (MaxValue != int.MaxValue && displayValue > MaxValue)
                {
                    _logService.Log(LogLevel.Warning, $"Converted value {displayValue} exceeds MaxValue {MaxValue} for {setting.Id} - leaving empty");
                }
                else
                {
                    NumericValue = displayValue;
                }
            }

            _isInitializing = false;
        }


        public async Task SetupComboBoxAsync(
            SettingDefinition setting,
            object? currentValue,
            IComboBoxSetupService comboBoxSetupService,
            ILogService logService,
            Dictionary<string, object?>? rawValues = null
        )
        {
            _logService.Log(LogLevel.Info, $"[SettingItemViewModel] SetupComboBox called for '{SettingId}' with currentValue: {currentValue}");

            if (setting.InputType != InputType.Selection)
            {
                _logService.Log(LogLevel.Info, $"[SettingItemViewModel] Setting '{SettingId}' is not Selection type, skipping combobox setup");
                return;
            }

            _logService.Log(LogLevel.Info, $"[SettingItemViewModel] Calling ComboBoxSetupService for '{SettingId}' - currentValue will be resolved to index");
            var comboBoxSetupResult = await comboBoxSetupService.SetupComboBoxOptionsAsync(setting, currentValue);

            if (comboBoxSetupResult.Success)
            {
                _logService.Log(LogLevel.Info, $"[SettingItemViewModel] ComboBox setup successful for '{SettingId}', adding {comboBoxSetupResult.Options.Count} options");

                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var option in comboBoxSetupResult.Options)
                    {
                        ComboBoxOptions.Add(new Winhance.Core.Features.Common.Interfaces.ComboBoxOption
                        {
                            DisplayText = option.DisplayText,
                            Value = option.Value,
                            Description = option.Description,
                        });
                    }
                });

                _logService.Log(LogLevel.Info, $"[SettingItemViewModel] Setting SelectedValue to resolved index: {comboBoxSetupResult.SelectedValue} for '{SettingId}'");
                SelectedValue = comboBoxSetupResult.SelectedValue;
                _lastConfirmedSelectedValue = comboBoxSetupResult.SelectedValue;
                UpdateWarningText(comboBoxSetupResult.SelectedValue);

                if (SupportsSeparateACDC && rawValues != null)
                {
                    if (rawValues.TryGetValue("ACValue", out var acVal))
                    {
                        var acIndex = comboBoxSetupService.ResolveIndexFromRawValues(setting, new Dictionary<string, object?> { ["PowerCfgValue"] = acVal });
                        ACValue = acIndex;
                        _lastConfirmedACValue = acIndex;
                    }

                    if (rawValues.TryGetValue("DCValue", out var dcVal))
                    {
                        var dcIndex = comboBoxSetupService.ResolveIndexFromRawValues(setting, new Dictionary<string, object?> { ["PowerCfgValue"] = dcVal });
                        DCValue = dcIndex;
                        _lastConfirmedDCValue = dcIndex;
                    }
                }
            }
            else
            {
                _logService.Log(LogLevel.Warning, $"[SettingItemViewModel] ComboBox setup failed for '{SettingId}': {comboBoxSetupResult.ErrorMessage}");
                SelectedValue = 0;
            }
        }




        public SettingItemViewModel(
            ISettingApplicationService settingService,
            IEventBus eventBus,
            ILogService logService,
            ISettingsConfirmationService confirmationService,
            IDomainServiceRouter domainServiceRouter,
            IInitializationService initializationService,
            IComboBoxSetupService comboBoxSetupService,
            ISystemSettingsDiscoveryService discoveryService,
            IUserPreferencesService userPreferencesService,
            IDialogService dialogService
        )
        {
            _settingApplicationService =
                settingService ?? throw new ArgumentNullException(nameof(settingService));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _confirmationService = confirmationService ?? throw new ArgumentNullException(nameof(confirmationService));
            _domainServiceRouter = domainServiceRouter ?? throw new ArgumentNullException(nameof(domainServiceRouter));
            _initializationService = initializationService ?? throw new ArgumentNullException(nameof(initializationService));
            _comboBoxSetupService = comboBoxSetupService ?? throw new ArgumentNullException(nameof(comboBoxSetupService));
            _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
            _userPreferencesService = userPreferencesService ?? throw new ArgumentNullException(nameof(userPreferencesService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

            ToggleCommand = new AsyncRelayCommand(HandleToggleAsync);
            ValueChangedCommand = new AsyncRelayCommand<object>(HandleValueChangedAsync);
            ActionCommand = new AsyncRelayCommand(HandleActionAsync);
            UnlockCommand = new AsyncRelayCommand(HandleUnlockAsync);

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
                var (canProceed, checkboxResult) = await HandleConfirmationIfNeeded(IsSelected);
                if (!canProceed)
                {
                    IsSelected = !IsSelected;
                    Status = string.Empty;
                    return;
                }

                await _settingApplicationService.ApplySettingAsync(SettingId, IsSelected, SelectedValue, checkboxResult);

                _hasChangedThisSession = true;
                ShowRestartWarningIfNeeded();
                Status = "Applied";
                UpdateChildSettings();
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
                _ = Task.Run(async () => 
                {
                    await Task.Delay(3000);
                    Application.Current.Dispatcher.Invoke(() => Status = string.Empty);
                });
            }
        }

        private async Task HandleNumericValueChangedAsync(int displayValue)
        {
            if (IsApplying)
                return;

            _isApplyingNumericValue = true;
            var previousValue = NumericValue;
            IsApplying = true;
            Status = "Applying...";

            try
            {
                _logService.Log(LogLevel.Info, $"Applying numeric setting {SettingId}: display value={displayValue} (Units: {Units})");
                
                // Convert display value back to system value before applying
                var systemValue = ConvertDisplayValueToSystemValue(displayValue);
                
                await _settingApplicationService.ApplySettingAsync(SettingId, IsSelected, systemValue);

                await Task.Delay(100);
                Status = "Applied";
                UpdateChildSettings();
            }
            catch (Exception ex)
            {
                Status = "Error";
                NumericValue = previousValue;
                _logService.Log(LogLevel.Error, $"Exception applying numeric setting {SettingId}: {ex.Message}");
            }
            finally
            {
                IsApplying = false;
                _isApplyingNumericValue = false;
                _ = Task.Run(async () => 
                {
                    await Task.Delay(3000);
                    Application.Current.Dispatcher.Invoke(() => Status = string.Empty);
                });
            }
        }

        private async Task HandleValueChangedAsync(object? value)
        {
            _logService.Log(LogLevel.Info, $"[SettingItemViewModel] HandleValueChangedAsync called for '{SettingId}' with value: {value}");

            if (IsApplying || _isRefreshingComboBox)
                return;

            var previousValue = _lastConfirmedSelectedValue;
            var setting = await GetSettingDefinition();

            try
            {
                var (canProceed, checkboxResult) = await HandleConfirmationIfNeeded(SelectedValue);
                if (!canProceed)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        _isInitializing = true;
                        SelectedValue = previousValue;
                        _isInitializing = false;
                    });
                    return;
                }

                UpdateWarningText(value);
                IsApplying = true;
                Status = "Applying...";

                bool enableFlag = InputType == InputType.Selection ? true : IsSelected;

                await _settingApplicationService.ApplySettingAsync(SettingId, enableFlag, SelectedValue, checkboxResult);
                _hasChangedThisSession = true;
                ShowRestartWarningIfNeeded();
                Status = "Applied";
                _lastConfirmedSelectedValue = value;
                UpdateChildSettings();
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"[SettingItemViewModel] Exception applying setting '{SettingId}': {ex.Message}");
                Status = "Error";
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _isInitializing = true;
                    SelectedValue = previousValue;
                    _isInitializing = false;
                });
            }
            finally
            {
                IsApplying = false;
                _ = Task.Run(async () =>
                {
                    await Task.Delay(3000);
                    Application.Current.Dispatcher.Invoke(() => Status = string.Empty);
                });
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

                await _settingApplicationService.ApplySettingAsync(SettingId, false, null, false, ActionCommandName, applyRecommended);
                Status = "Completed";
                UpdateChildSettings();
            }
            catch (Exception ex)
            {
                Status = "Error";
                _logService.Log(LogLevel.Error, $"Exception executing action {ActionCommandName} for setting {SettingId}: {ex.Message}");
            }
            finally
            {
                IsApplying = false;
                _ = Task.Run(async () => 
                {
                    await Task.Delay(3000);
                    Application.Current.Dispatcher.Invoke(() => Status = string.Empty);
                });
            }
        }

        public async Task RefreshStateAsync()
        {
            if (_isApplyingNumericValue)
                return;

            try
            {
                _logService.Log(LogLevel.Info, $"[SettingItemViewModel] RefreshStateAsync called for '{SettingId}', current SelectedValue: {SelectedValue}");

                var setting = await GetSettingDefinition();

                var results = await _discoveryService.GetSettingStatesAsync(new[] { setting });
                var result = results.TryGetValue(SettingId, out var state) ? state : new SettingStateResult();
                if (result.Success)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        _isInitializing = true;

                        var previousIsSelected = IsSelected;
                        IsSelected = result.IsEnabled;

                        if (InputType == InputType.Selection)
                        {
                            var resolvedIndex = _comboBoxSetupService.ResolveIndexFromRawValues(setting, result.RawValues ?? new Dictionary<string, object?>());
                            _logService.Log(LogLevel.Info, $"[SettingItemViewModel] RefreshStateAsync for '{SettingId}' - resolved index: {resolvedIndex}, current SelectedValue: {SelectedValue}, changing SelectedValue to: {resolvedIndex}");

                            var previousSelectedValue = SelectedValue;
                            SelectedValue = resolvedIndex;
                            _lastConfirmedSelectedValue = resolvedIndex;

                            if (previousSelectedValue != SelectedValue)
                            {
                                UpdateChildSettings();
                            }
                        }
                        else
                        {
                            SelectedValue = result.CurrentValue;
                        }

                        if (InputType == InputType.NumericRange && result.CurrentValue is int numericCurrentValue)
                        {
                            var displayValue = ConvertSystemValueToDisplayValue(setting, numericCurrentValue);
                            NumericValue = displayValue;
                        }

                        _isInitializing = false;

                        if (InputType == InputType.Toggle && previousIsSelected != IsSelected)
                        {
                            UpdateChildSettings();
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Failed to refresh state for setting {SettingId}: {ex.Message}");
            }
        }

        private void HandleTooltipUpdated(TooltipUpdatedEvent evt)
        {
            if (evt.SettingId == SettingId)
            {
                TooltipData = evt.TooltipData;
            }
        }

        private void HandleTooltipsBulkLoaded(TooltipsBulkLoadedEvent evt)
        {
            if (evt.TooltipDataCollection.TryGetValue(SettingId, out var tooltipData))
            {
                TooltipData = tooltipData;
            }
        }

        private void HandleFeatureComposed(FeatureComposedEvent evt)
        {
            if (evt.Settings.Any(s => s.Id == SettingId))
            {
                _isInitializing = false;
            }
        }

        public bool MatchesSearch(string searchText)
        {
            return SearchHelper.MatchesSearchTerm(searchText, Name, Description, GroupName);
        }

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
                var settings = await domainService.GetSettingsAsync();
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




        private int ConvertSystemValueToDisplayValue(SettingDefinition setting, int systemValue)
        {
            if (setting.PowerCfgSettings?.Count > 0)
            {
                var powerCfgSetting = setting.PowerCfgSettings.First();
                var systemUnits = powerCfgSetting.Units ?? "";
                var displayUnits = Units ?? "";
                
                // Convert seconds (from PowerCfg) to minutes (for display)
                if (systemUnits.Equals("Seconds", StringComparison.OrdinalIgnoreCase) && 
                    displayUnits.Equals("Minutes", StringComparison.OrdinalIgnoreCase))
                {
                    return systemValue / 60;
                }
            }
            
            return systemValue;
        }

        private int ConvertDisplayValueToSystemValue(int displayValue)
        {
            // For power-harddisk-timeout, convert minutes back to seconds
            if (SettingId == "power-harddisk-timeout")
            {
                return displayValue * 60;
            }
            
            return displayValue;
        }

        private void UpdateChildSettings()
        {
            if (ParentFeatureViewModel?.Settings == null) return;

            var children = ParentFeatureViewModel.Settings
                .Where(s => s.SettingDefinition?.ParentSettingId == SettingId);

            bool parentEnabled = InputType switch
            {
                InputType.Toggle => IsSelected,
                InputType.Selection => SelectedValue is int index && index != 0,
                _ => IsSelected
            };

            foreach (var child in children)
            {
                child.ParentIsEnabled = parentEnabled;
            }
        }

        private void UpdateWarningText(object? value)
        {
            if (SettingDefinition == null || value is not int selectedIndex)
            {
                WarningText = null;
                return;
            }

            if (SettingDefinition.CustomProperties?.TryGetValue(CustomPropertyKeys.OptionWarnings, out var warnings) == true &&
                warnings is Dictionary<int, string> warningDict &&
                warningDict.TryGetValue(selectedIndex, out var warning))
            {
                WarningText = warning;
            }
            else
            {
                WarningText = null;
            }
        }

        private void ShowRestartWarningIfNeeded()
        {
            if (!_hasChangedThisSession)
                return;

            if (SettingDefinition?.CustomProperties?.TryGetValue(
                CustomPropertyKeys.RequiresRestartMessage, out var message) == true &&
                message is string messageText)
            {
                WarningText = messageText;
            }
        }

        private async Task HandleACValueChangedAsync(object? value)
        {
            if (IsApplying) return;

            var previousValue = _lastConfirmedACValue;
            var setting = await GetSettingDefinition();
            IsApplying = true;
            Status = "Applying...";

            try
            {
                var (canProceed, checkboxResult) = await HandleConfirmationIfNeeded(value);
                if (!canProceed)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        _isInitializing = true;
                        ACValue = previousValue;
                        _isInitializing = false;
                    });
                    Status = string.Empty;
                    IsApplying = false;
                    return;
                }

                var combinedValue = new Dictionary<string, object?>
                {
                    ["ACValue"] = value,
                    ["DCValue"] = DCValue
                };

                await _settingApplicationService.ApplySettingAsync(SettingId, true, combinedValue, checkboxResult);
                Status = "Applied";
                _lastConfirmedACValue = value;
                UpdateChildSettings();
            }
            catch (Exception ex)
            {
                Status = "Error";
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _isInitializing = true;
                    ACValue = previousValue;
                    _isInitializing = false;
                });
                _logService.Log(LogLevel.Error, $"Exception applying AC value for setting {SettingId}: {ex.Message}");
            }
            finally
            {
                IsApplying = false;
                _ = Task.Run(async () =>
                {
                    await Task.Delay(3000);
                    Application.Current.Dispatcher.Invoke(() => Status = string.Empty);
                });
            }
        }

        private async Task HandleDCValueChangedAsync(object? value)
        {
            if (IsApplying) return;

            var previousValue = _lastConfirmedDCValue;
            var setting = await GetSettingDefinition();
            IsApplying = true;
            Status = "Applying...";

            try
            {
                var (canProceed, checkboxResult) = await HandleConfirmationIfNeeded(value);
                if (!canProceed)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        _isInitializing = true;
                        DCValue = previousValue;
                        _isInitializing = false;
                    });
                    Status = string.Empty;
                    IsApplying = false;
                    return;
                }

                var combinedValue = new Dictionary<string, object?>
                {
                    ["ACValue"] = ACValue,
                    ["DCValue"] = value
                };

                await _settingApplicationService.ApplySettingAsync(SettingId, true, combinedValue, checkboxResult);
                Status = "Applied";
                _lastConfirmedDCValue = value;
                UpdateChildSettings();
            }
            catch (Exception ex)
            {
                Status = "Error";
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _isInitializing = true;
                    DCValue = previousValue;
                    _isInitializing = false;
                });
                _logService.Log(LogLevel.Error, $"Exception applying DC value for setting {SettingId}: {ex.Message}");
            }
            finally
            {
                IsApplying = false;
                _ = Task.Run(async () =>
                {
                    await Task.Delay(3000);
                    Application.Current.Dispatcher.Invoke(() => Status = string.Empty);
                });
            }
        }

        private async Task HandleACNumericValueChangedAsync(int displayValue)
        {
            if (IsApplying) return;

            _isApplyingNumericValue = true;
            var previousValue = ACNumericValue;
            IsApplying = true;
            Status = "Applying...";

            try
            {
                var systemValue = ConvertDisplayValueToSystemValue(displayValue);

                var combinedValue = new Dictionary<string, object?>
                {
                    ["ACValue"] = systemValue,
                    ["DCValue"] = ConvertDisplayValueToSystemValue(DCNumericValue)
                };

                await _settingApplicationService.ApplySettingAsync(SettingId, IsSelected, combinedValue);

                await Task.Delay(100);
                Status = "Applied";
                UpdateChildSettings();
            }
            catch (Exception ex)
            {
                Status = "Error";
                ACNumericValue = previousValue;
                _logService.Log(LogLevel.Error, $"Exception applying AC numeric setting {SettingId}: {ex.Message}");
            }
            finally
            {
                IsApplying = false;
                _isApplyingNumericValue = false;
                _ = Task.Run(async () =>
                {
                    await Task.Delay(3000);
                    Application.Current.Dispatcher.Invoke(() => Status = string.Empty);
                });
            }
        }

        private async Task HandleDCNumericValueChangedAsync(int displayValue)
        {
            if (IsApplying) return;

            _isApplyingNumericValue = true;
            var previousValue = DCNumericValue;
            IsApplying = true;
            Status = "Applying...";

            try
            {
                var systemValue = ConvertDisplayValueToSystemValue(displayValue);

                var combinedValue = new Dictionary<string, object?>
                {
                    ["ACValue"] = ConvertDisplayValueToSystemValue(ACNumericValue),
                    ["DCValue"] = systemValue
                };

                await _settingApplicationService.ApplySettingAsync(SettingId, IsSelected, combinedValue);

                await Task.Delay(100);
                Status = "Applied";
                UpdateChildSettings();
            }
            catch (Exception ex)
            {
                Status = "Error";
                DCNumericValue = previousValue;
                _logService.Log(LogLevel.Error, $"Exception applying DC numeric setting {SettingId}: {ex.Message}");
            }
            finally
            {
                IsApplying = false;
                _isApplyingNumericValue = false;
                _ = Task.Run(async () =>
                {
                    await Task.Delay(3000);
                    Application.Current.Dispatcher.Invoke(() => Status = string.Empty);
                });
            }
        }

        private async Task HandleUnlockAsync()
        {
            if (!IsLocked) return;

            var message = "⚠️ Advanced Power Setting Warning\n\n" +
                          "This setting is not normally exposed in Windows Power Options and requires registry modifications to access.\n\n" +
                          "Modifying it incorrectly may cause:\n" +
                          "• System instability or unexpected behavior\n" +
                          "• Performance degradation\n" +
                          "• Thermal management problems\n" +
                          "• Settings may not work on all CPU types (modern HWP vs legacy)\n\n" +
                          "Only change this if you understand processor power management.\n\n" +
                          "Are you sure you want to modify this setting?";

            var (confirmed, dontShowAgain) = await _dialogService.ShowConfirmationWithCheckboxAsync(
                message,
                "Don't show this warning again for advanced power settings",
                "Advanced Setting Warning",
                "Unlock",
                "Cancel",
                "AlertCircle"
            );

            if (confirmed)
            {
                IsLocked = false;

                if (dontShowAgain)
                {
                    await _userPreferencesService.SetPreferenceAsync("AdvancedPowerSettingsUnlocked", true);
                    _logService.Log(LogLevel.Info, "User permanently unlocked advanced power settings");

                    if (ParentFeatureViewModel != null)
                    {
                        foreach (var setting in ParentFeatureViewModel.Settings)
                        {
                            if (setting.RequiresAdvancedUnlock && setting != this)
                            {
                                setting.IsLocked = false;
                            }
                        }
                    }
                }
            }
        }

        public void Dispose()
        {
            _debounceTokenSource?.Cancel();
            _debounceTokenSource?.Dispose();
            _tooltipUpdatedSubscription?.Dispose();
            _tooltipsBulkLoadedSubscription?.Dispose();
            _featureComposedSubscription?.Dispose();
            _settingAppliedSubscription?.Dispose();
        }
    }
}

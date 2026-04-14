using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;

using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Constants;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Common.Utilities;
using Winhance.UI.Features.Common.ViewModels;

namespace Winhance.UI.Features.Optimize.ViewModels;

public partial class SettingItemViewModel : BaseViewModel
{
    private readonly ISettingApplicationService _settingApplicationService;
    private readonly ILogService _logService;
    private readonly IDispatcherService _dispatcherService;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localizationService;
    private readonly IUserPreferencesService? _userPreferencesService;
    private readonly INewBadgeService? _newBadgeService;
    private readonly SettingStatusBannerManager _statusBannerManager;
    private readonly TechnicalDetailsManager _technicalDetailsManager;
    private volatile bool _isUpdatingFromEvent;
    private bool _hasChangedThisSession;
    private object? _pendingValue;

    public ISettingsFeatureViewModel? ParentFeatureViewModel { get; set; }

    public SettingDefinition? SettingDefinition { get; set; }

    [ObservableProperty]
    public partial string SettingId { get; set; }

    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial string Description { get; set; }

    [ObservableProperty]
    public partial string GroupName { get; set; }

    [ObservableProperty]
    public partial string Icon { get; set; }

    [ObservableProperty]
    public partial string IconPack { get; set; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    [ObservableProperty]
    public partial bool IsApplying { get; set; }

    [ObservableProperty]
    public partial string Status { get; set; }

    [ObservableProperty]
    public partial string? StatusBannerMessage { get; set; }

    [ObservableProperty]
    public partial InfoBarSeverity StatusBannerSeverity { get; set; }

    public bool HasStatusBanner => !string.IsNullOrEmpty(StatusBannerMessage);

    partial void OnStatusBannerMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasStatusBanner));
    }

    [ObservableProperty]
    public partial InputType InputType { get; set; }

    [ObservableProperty]
    public partial object? SelectedValue { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<ComboBoxDisplayOption> ComboBoxOptions { get; set; }

    [ObservableProperty]
    public partial int NumericValue { get; set; }

    [ObservableProperty]
    public partial int AcValue { get; set; }

    [ObservableProperty]
    public partial int DcValue { get; set; }

    [ObservableProperty]
    public partial int AcNumericValue { get; set; }

    [ObservableProperty]
    public partial int DcNumericValue { get; set; }

    [ObservableProperty]
    public partial bool HasBattery { get; set; }

    [ObservableProperty]
    public partial int MinValue { get; set; }

    [ObservableProperty]
    public partial int MaxValue { get; set; }

    [ObservableProperty]
    public partial string Units { get; set; }

    public string OnText { get; set; } = "On";
    public string OffText { get; set; } = "Off";
    public string ActionButtonText { get; set; } = "Apply";

    // Technical Details panel
    [ObservableProperty]
    public partial bool IsTechnicalDetailsExpanded { get; set; }

    [ObservableProperty]
    public partial bool IsTechnicalDetailsGloballyVisible { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<TechnicalDetailRow> TechnicalDetails { get; set; }

    public bool HasTechnicalDetails => TechnicalDetails.Count > 0;

    /// <summary>
    /// Controls visibility of the toggle bar: requires data AND global toggle to be on.
    /// </summary>
    public bool ShowTechnicalDetailsBar => HasTechnicalDetails && IsTechnicalDetailsGloballyVisible;

    /// <summary>
    /// Bottom corners rounded only when the expandable content is collapsed;
    /// when expanded, the content panel below carries the rounded corners.
    /// </summary>
    public Microsoft.UI.Xaml.CornerRadius TechnicalDetailsToggleCornerRadius =>
        IsTechnicalDetailsExpanded
            ? new Microsoft.UI.Xaml.CornerRadius(0)
            : new Microsoft.UI.Xaml.CornerRadius(0, 0, 4, 4);

    partial void OnIsTechnicalDetailsExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(TechnicalDetailsToggleCornerRadius));
    }

    partial void OnIsTechnicalDetailsGloballyVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowTechnicalDetailsBar));
        if (!value) IsTechnicalDetailsExpanded = false;
    }

    public string TechnicalDetailsLabel =>
        _localizationService.GetString("View_TechnicalDetails") ?? "Technical Details";

    public string OpenRegeditTooltip =>
        _localizationService.GetString("TechnicalDetails_OpenRegedit") ?? "Open in Registry Editor";

    public IRelayCommand<string> OpenRegeditCommand { get; }

    [ObservableProperty]
    public partial bool IsVisible { get; set; }

    [ObservableProperty]
    public partial bool IsEnabled { get; set; }

    // Pre-built message for cross-group child settings (built during initialization)
    public string? CrossGroupInfoMessage { get; set; }

    // New setting badge
    [ObservableProperty]
    public partial bool IsNew { get; set; }

    public string NewBadgeText => _localizationService.GetString("Badge_New") ?? "NEW";
    public string NewBadgeDismissTooltip => _localizationService.GetString("Badge_New_Dismiss") ?? "Dismiss";

    public IRelayCommand DismissNewBadgeCommand { get; private set; } = null!;

    // InfoBadge properties
    [ObservableProperty]
    public partial bool IsInfoBadgeGloballyVisible { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<BadgePillState> BadgeRow { get; set; } = Array.Empty<BadgePillState>();

    /// <summary>
    /// True if the setting has RecommendedValue/DefaultValue data to compare against.
    /// False for settings using NativePowerApiSettings, PowerShellScripts, or RegContents only.
    /// </summary>
    public bool HasBadgeData { get; set; }

    public bool ShowInfoBadge => IsInfoBadgeGloballyVisible && HasBadgeData;

    partial void OnIsInfoBadgeGloballyVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowInfoBadge));
        OnPropertyChanged(nameof(ShowNumericQuickSetButtons));
        OnPropertyChanged(nameof(ShowToggleQuickSetButtons));
        OnPropertyChanged(nameof(ShowSelectionQuickSetButtons));
        OnPropertyChanged(nameof(ShowAcSelectionQuickSetButtons));
        OnPropertyChanged(nameof(ShowDcSelectionQuickSetButtons));
    }

    // ───────── Quick-set buttons ─────────
    //
    // Every setting card shows "Set to Recommended" / "Set to Default" buttons in front
    // of its control when the ShowInfoBadges preference is on AND the setting has at
    // least one of Recommended/Default defined. Values come from:
    //   • RegistrySetting.RecommendedValue / DefaultValue        → Toggle / Numeric
    //   • ComboBoxOption.IsRecommended / IsDefault               → Selection
    //   • PowerCfgSetting.RecommendedValueAC/DC / DefaultValueAC/DC → AC/DC Numeric + Selection
    //
    // Tooltips use the localized "Set to Recommended ({0})" / "Set to Default ({0})"
    // template — {0} is the target value's display form (number, On/Off text, or
    // combobox option label). The string uses a literal "{0}" token (not .NET composite
    // format), so we use string.Replace at runtime.

    /// <summary>
    /// Recommended value for the single NumericRange spinner, or null if not available.
    /// Prefers PowerCfgSetting.RecommendedValueAC (non-separate) and falls back to the
    /// primary RegistrySetting.RecommendedValue.
    /// </summary>
    public int? NumericRecommendedValue
    {
        get
        {
            if (SettingDefinition == null) return null;
            // Non-separate PowerCfg uses AC value as the single value
            var pcfg = SettingDefinition.PowerCfgSettings?
                .FirstOrDefault(p => p.PowerModeSupport != PowerModeSupport.Separate);
            if (pcfg?.RecommendedValueAC is int rac) return rac;

            var reg = SettingDefinition.RegistrySettings?
                .FirstOrDefault(r => r.IsPrimary) ?? SettingDefinition.RegistrySettings?.FirstOrDefault();
            return TryConvertToInt(reg?.RecommendedValue);
        }
    }

    /// <summary>
    /// Default value for the single NumericRange spinner, or null if not available.
    /// </summary>
    public int? NumericDefaultValue
    {
        get
        {
            if (SettingDefinition == null) return null;
            var pcfg = SettingDefinition.PowerCfgSettings?
                .FirstOrDefault(p => p.PowerModeSupport != PowerModeSupport.Separate);
            if (pcfg?.DefaultValueAC is int dac) return dac;

            var reg = SettingDefinition.RegistrySettings?
                .FirstOrDefault(r => r.IsPrimary) ?? SettingDefinition.RegistrySettings?.FirstOrDefault();
            return TryConvertToInt(reg?.DefaultValue);
        }
    }

    /// <summary>
    /// AC-side recommended value for Separate PowerCfg NumericRange settings.
    /// </summary>
    public int? AcRecommendedValue =>
        SettingDefinition?.PowerCfgSettings?.FirstOrDefault()?.RecommendedValueAC;

    public int? AcDefaultValue =>
        SettingDefinition?.PowerCfgSettings?.FirstOrDefault()?.DefaultValueAC;

    public int? DcRecommendedValue =>
        SettingDefinition?.PowerCfgSettings?.FirstOrDefault()?.RecommendedValueDC;

    public int? DcDefaultValue =>
        SettingDefinition?.PowerCfgSettings?.FirstOrDefault()?.DefaultValueDC;

    private static int? TryConvertToInt(object? value)
    {
        if (value == null) return null;
        try { return Convert.ToInt32(value); }
        catch { return null; }
    }

    private string FormatValueTooltip(string key, object value)
    {
        var template = _localizationService?.GetString(key);
        if (!string.IsNullOrEmpty(template))
            return template.Replace("{0}", value?.ToString() ?? string.Empty);
        // Fallback if the key is missing
        return key == StringKeys.InfoBadge.NumericSetToRecommendedTooltip
            ? $"Set to Recommended ({value})"
            : $"Set to Default ({value})";
    }

    // Tooltips — computed live so language changes flow through OnLanguageChanged.
    public string RecommendedValueTooltip =>
        NumericRecommendedValue is int rec
            ? FormatValueTooltip(StringKeys.InfoBadge.NumericSetToRecommendedTooltip, rec)
            : string.Empty;

    public string DefaultValueTooltip =>
        NumericDefaultValue is int def
            ? FormatValueTooltip(StringKeys.InfoBadge.NumericSetToDefaultTooltip, def)
            : string.Empty;

    public string RecommendedAcValueTooltip =>
        AcRecommendedValue is int rec
            ? FormatValueTooltip(StringKeys.InfoBadge.NumericSetToRecommendedTooltip, rec)
            : string.Empty;

    public string DefaultAcValueTooltip =>
        AcDefaultValue is int def
            ? FormatValueTooltip(StringKeys.InfoBadge.NumericSetToDefaultTooltip, def)
            : string.Empty;

    public string RecommendedDcValueTooltip =>
        DcRecommendedValue is int rec
            ? FormatValueTooltip(StringKeys.InfoBadge.NumericSetToRecommendedTooltip, rec)
            : string.Empty;

    public string DefaultDcValueTooltip =>
        DcDefaultValue is int def
            ? FormatValueTooltip(StringKeys.InfoBadge.NumericSetToDefaultTooltip, def)
            : string.Empty;

    /// <summary>
    /// True when the NumericRange quick-set buttons should be visible: requires the
    /// global ShowInfoBadges preference to be on AND at least one of Recommended/Default
    /// to be available for this setting.
    /// </summary>
    public bool ShowNumericQuickSetButtons
    {
        get
        {
            if (!IsInfoBadgeGloballyVisible) return false;
            if (InputType != InputType.NumericRange) return false;
            return NumericRecommendedValue.HasValue
                || NumericDefaultValue.HasValue
                || AcRecommendedValue.HasValue
                || AcDefaultValue.HasValue
                || DcRecommendedValue.HasValue
                || DcDefaultValue.HasValue;
        }
    }

    /// <summary>
    /// Sets the single NumericValue to the Recommended value and runs the apply path.
    /// </summary>
    public IRelayCommand SetNumericToRecommendedCommand => _setNumericToRecommendedCommand ??=
        new RelayCommand(() =>
        {
            if (NumericRecommendedValue is int v)
            {
                NumericValue = v;
                HandleValueChangedAsync(v).FireAndForget(_logService);
            }
        });
    private RelayCommand? _setNumericToRecommendedCommand;

    public IRelayCommand SetNumericToDefaultCommand => _setNumericToDefaultCommand ??=
        new RelayCommand(() =>
        {
            if (NumericDefaultValue is int v)
            {
                NumericValue = v;
                HandleValueChangedAsync(v).FireAndForget(_logService);
            }
        });
    private RelayCommand? _setNumericToDefaultCommand;

    public IRelayCommand SetAcNumericToRecommendedCommand => _setAcNumericToRecommendedCommand ??=
        new RelayCommand(() =>
        {
            if (AcRecommendedValue is int v)
            {
                AcNumericValue = v;
                HandleACDCNumericChangedAsync().FireAndForget(_logService);
            }
        });
    private RelayCommand? _setAcNumericToRecommendedCommand;

    public IRelayCommand SetAcNumericToDefaultCommand => _setAcNumericToDefaultCommand ??=
        new RelayCommand(() =>
        {
            if (AcDefaultValue is int v)
            {
                AcNumericValue = v;
                HandleACDCNumericChangedAsync().FireAndForget(_logService);
            }
        });
    private RelayCommand? _setAcNumericToDefaultCommand;

    public IRelayCommand SetDcNumericToRecommendedCommand => _setDcNumericToRecommendedCommand ??=
        new RelayCommand(() =>
        {
            if (DcRecommendedValue is int v)
            {
                DcNumericValue = v;
                HandleACDCNumericChangedAsync().FireAndForget(_logService);
            }
        });
    private RelayCommand? _setDcNumericToRecommendedCommand;

    public IRelayCommand SetDcNumericToDefaultCommand => _setDcNumericToDefaultCommand ??=
        new RelayCommand(() =>
        {
            if (DcDefaultValue is int v)
            {
                DcNumericValue = v;
                HandleACDCNumericChangedAsync().FireAndForget(_logService);
            }
        });
    private RelayCommand? _setDcNumericToDefaultCommand;

    // ───────── Toggle quick-set buttons ─────────
    private RegistrySetting? PrimaryRegistrySetting =>
        SettingDefinition?.RegistrySettings?.FirstOrDefault(r => r.IsPrimary)
        ?? SettingDefinition?.RegistrySettings?.FirstOrDefault();

    /// <summary>
    /// True if Recommended maps to the enabled state, false if disabled, null if no
    /// recommendation is set. Resolution order:
    ///   1. <see cref="SettingDefinition.RecommendedToggleState"/> (explicit toggle-level flag)
    ///   2. Per-RegistrySetting RecommendedValue mapped strictly via EnabledValue/DisabledValue
    ///   3. null (no Recommended badge / button)
    /// The strict step never derives state from the EnabledValue/DisabledValue null sentinel —
    /// recommendations against the key-absent state must be expressed via RecommendedToggleState.
    /// </summary>
    public bool? ToggleRecommendedState
    {
        get
        {
            if (SettingDefinition?.RecommendedToggleState is bool explicitState) return explicitState;
            return ResolveToggleState(PrimaryRegistrySetting?.RecommendedValue, deriveFromKeyAbsent: false);
        }
    }

    /// <summary>
    /// True if Default maps to the enabled state, false if disabled, null if not derivable.
    /// When DefaultValue is null, the state is derived from which of EnabledValue /
    /// DisabledValue contains the null sentinel (key-absent convention).
    /// </summary>
    public bool? ToggleDefaultState => ResolveToggleState(
        PrimaryRegistrySetting?.DefaultValue, deriveFromKeyAbsent: true);

    private bool? ResolveToggleState(object? targetValue, bool deriveFromKeyAbsent)
    {
        var reg = PrimaryRegistrySetting;
        if (reg == null) return null;
        if (targetValue == null && !deriveFromKeyAbsent) return null;
        return ToggleTargetState(targetValue, reg.EnabledValue, reg.DisabledValue);
    }

    /// <summary>
    /// Resolves a target value into the toggle state it represents. When
    /// <paramref name="targetValue"/> is null, the result is derived from which of
    /// EnabledValue / DisabledValue contains the null sentinel — i.e. "key absent" implies
    /// that state. Callers pick whether to use this derivation: Default does, Recommended
    /// does not (see ResolveToggleState).
    /// </summary>
    internal static bool? ToggleTargetState(object? targetValue, object?[]? enabledValue, object?[]? disabledValue)
    {
        if (targetValue == null)
        {
            if (ArrayContainsNull(enabledValue)) return true;
            if (ArrayContainsNull(disabledValue)) return false;
            return null;
        }
        if (IsValueInArray(targetValue, enabledValue)) return true;
        if (IsValueInArray(targetValue, disabledValue)) return false;
        return null;
    }

    private static bool ArrayContainsNull(object?[]? array) => array?.Any(v => v == null) == true;

    private string ToggleStateText(bool state) => state ? OnText : OffText;

    public string ToggleRecommendedTooltip =>
        ToggleRecommendedState is bool s
            ? FormatValueTooltip(StringKeys.InfoBadge.NumericSetToRecommendedTooltip, ToggleStateText(s))
            : string.Empty;

    public string ToggleDefaultTooltip =>
        ToggleDefaultState is bool s
            ? FormatValueTooltip(StringKeys.InfoBadge.NumericSetToDefaultTooltip, ToggleStateText(s))
            : string.Empty;

    public bool ShowToggleQuickSetButtons
    {
        get
        {
            if (!IsInfoBadgeGloballyVisible) return false;
            if (InputType != InputType.Toggle && InputType != InputType.CheckBox) return false;
            return ToggleRecommendedState.HasValue || ToggleDefaultState.HasValue;
        }
    }

    public IRelayCommand SetToggleToRecommendedCommand => _setToggleToRecommendedCommand ??=
        new RelayCommand(() =>
        {
            if (ToggleRecommendedState is bool v)
                HandleToggleAsync(v).FireAndForget(_logService);
        });
    private RelayCommand? _setToggleToRecommendedCommand;

    public IRelayCommand SetToggleToDefaultCommand => _setToggleToDefaultCommand ??=
        new RelayCommand(() =>
        {
            if (ToggleDefaultState is bool v)
                HandleToggleAsync(v).FireAndForget(_logService);
        });
    private RelayCommand? _setToggleToDefaultCommand;

    // ───────── Selection quick-set buttons (single ComboBox) ─────────
    private int? FindOptionIndex(Func<Winhance.Core.Features.Common.Models.ComboBoxOption, bool> predicate)
    {
        var opts = SettingDefinition?.ComboBox?.Options;
        if (opts == null) return null;
        for (int i = 0; i < opts.Count; i++)
            if (predicate(opts[i])) return i;
        return null;
    }

    public int? SelectionRecommendedIndex => FindOptionIndex(o => o.IsRecommended);
    public int? SelectionDefaultIndex => FindOptionIndex(o => o.IsDefault);

    private string? OptionDisplayText(int? index)
    {
        if (index is not int i) return null;
        if (ComboBoxOptions == null || i < 0 || i >= ComboBoxOptions.Count) return null;
        return ComboBoxOptions[i].DisplayText;
    }

    public string SelectionRecommendedTooltip =>
        OptionDisplayText(SelectionRecommendedIndex) is { } label
            ? FormatValueTooltip(StringKeys.InfoBadge.NumericSetToRecommendedTooltip, label)
            : string.Empty;

    public string SelectionDefaultTooltip =>
        OptionDisplayText(SelectionDefaultIndex) is { } label
            ? FormatValueTooltip(StringKeys.InfoBadge.NumericSetToDefaultTooltip, label)
            : string.Empty;

    public bool ShowSelectionQuickSetButtons
    {
        get
        {
            if (!IsInfoBadgeGloballyVisible) return false;
            if (InputType != InputType.Selection) return false;
            if (IsPowerPlanSetting) return false; // PowerPlan has its own recommendation logic (TBD)
            if (SupportsSeparateACDC) return false; // Dual AC/DC selection uses per-mode buttons
            return SelectionRecommendedIndex.HasValue || SelectionDefaultIndex.HasValue;
        }
    }

    public IRelayCommand SetSelectionToRecommendedCommand => _setSelectionToRecommendedCommand ??=
        new RelayCommand(() =>
        {
            if (SelectionRecommendedIndex is int i)
                HandleValueChangedAsync(i).FireAndForget(_logService);
        });
    private RelayCommand? _setSelectionToRecommendedCommand;

    public IRelayCommand SetSelectionToDefaultCommand => _setSelectionToDefaultCommand ??=
        new RelayCommand(() =>
        {
            if (SelectionDefaultIndex is int i)
                HandleValueChangedAsync(i).FireAndForget(_logService);
        });
    private RelayCommand? _setSelectionToDefaultCommand;

    // ───────── AC/DC Selection quick-set buttons (PowerCfg Separate + Single AC) ─────────
    private int? FindPowerCfgOptionIndex(int? targetValue)
    {
        if (targetValue is not int target) return null;
        var opts = SettingDefinition?.ComboBox?.Options;
        if (opts == null) return null;
        for (int i = 0; i < opts.Count; i++)
        {
            if (opts[i].ValueMappings is { } m && m.TryGetValue("PowerCfgValue", out var v) && v != null)
            {
                try { if (Convert.ToInt32(v) == target) return i; }
                catch { }
            }
        }
        return null;
    }

    public int? AcSelectionRecommendedIndex =>
        FindPowerCfgOptionIndex(SettingDefinition?.PowerCfgSettings?.FirstOrDefault()?.RecommendedValueAC);

    public int? AcSelectionDefaultIndex =>
        FindPowerCfgOptionIndex(SettingDefinition?.PowerCfgSettings?.FirstOrDefault()?.DefaultValueAC);

    public int? DcSelectionRecommendedIndex =>
        FindPowerCfgOptionIndex(SettingDefinition?.PowerCfgSettings?.FirstOrDefault()?.RecommendedValueDC);

    public int? DcSelectionDefaultIndex =>
        FindPowerCfgOptionIndex(SettingDefinition?.PowerCfgSettings?.FirstOrDefault()?.DefaultValueDC);

    public string AcSelectionRecommendedTooltip =>
        OptionDisplayText(AcSelectionRecommendedIndex) is { } label
            ? FormatValueTooltip(StringKeys.InfoBadge.NumericSetToRecommendedTooltip, label)
            : string.Empty;

    public string AcSelectionDefaultTooltip =>
        OptionDisplayText(AcSelectionDefaultIndex) is { } label
            ? FormatValueTooltip(StringKeys.InfoBadge.NumericSetToDefaultTooltip, label)
            : string.Empty;

    public string DcSelectionRecommendedTooltip =>
        OptionDisplayText(DcSelectionRecommendedIndex) is { } label
            ? FormatValueTooltip(StringKeys.InfoBadge.NumericSetToRecommendedTooltip, label)
            : string.Empty;

    public string DcSelectionDefaultTooltip =>
        OptionDisplayText(DcSelectionDefaultIndex) is { } label
            ? FormatValueTooltip(StringKeys.InfoBadge.NumericSetToDefaultTooltip, label)
            : string.Empty;

    public bool ShowAcSelectionQuickSetButtons
    {
        get
        {
            if (!IsInfoBadgeGloballyVisible) return false;
            if (InputType != InputType.Selection) return false;
            if (SettingDefinition?.PowerCfgSettings?.Any() != true) return false;
            return AcSelectionRecommendedIndex.HasValue || AcSelectionDefaultIndex.HasValue;
        }
    }

    public bool ShowDcSelectionQuickSetButtons
    {
        get
        {
            if (!IsInfoBadgeGloballyVisible) return false;
            if (InputType != InputType.Selection) return false;
            if (!SupportsSeparateACDC) return false;
            return DcSelectionRecommendedIndex.HasValue || DcSelectionDefaultIndex.HasValue;
        }
    }

    public IRelayCommand SetAcSelectionToRecommendedCommand => _setAcSelectionToRecommendedCommand ??=
        new RelayCommand(() =>
        {
            if (AcSelectionRecommendedIndex is int i)
            {
                AcValue = i;
                HandleACDCSelectionChangedAsync().FireAndForget(_logService);
            }
        });
    private RelayCommand? _setAcSelectionToRecommendedCommand;

    public IRelayCommand SetAcSelectionToDefaultCommand => _setAcSelectionToDefaultCommand ??=
        new RelayCommand(() =>
        {
            if (AcSelectionDefaultIndex is int i)
            {
                AcValue = i;
                HandleACDCSelectionChangedAsync().FireAndForget(_logService);
            }
        });
    private RelayCommand? _setAcSelectionToDefaultCommand;

    public IRelayCommand SetDcSelectionToRecommendedCommand => _setDcSelectionToRecommendedCommand ??=
        new RelayCommand(() =>
        {
            if (DcSelectionRecommendedIndex is int i)
            {
                DcValue = i;
                HandleACDCSelectionChangedAsync().FireAndForget(_logService);
            }
        });
    private RelayCommand? _setDcSelectionToRecommendedCommand;

    public IRelayCommand SetDcSelectionToDefaultCommand => _setDcSelectionToDefaultCommand ??=
        new RelayCommand(() =>
        {
            if (DcSelectionDefaultIndex is int i)
            {
                DcValue = i;
                HandleACDCSelectionChangedAsync().FireAndForget(_logService);
            }
        });
    private RelayCommand? _setDcSelectionToDefaultCommand;


    // Advanced unlock support
    [ObservableProperty]
    public partial bool IsLocked { get; set; }

    public bool RequiresAdvancedUnlock => SettingDefinition?.RequiresAdvancedUnlock == true;
    public string ClickToUnlockText => _localizationService.GetString("Common_ClickToUnlock") ?? "Click to unlock";
    public IAsyncRelayCommand UnlockCommand { get; }

    // Review mode properties
    [ObservableProperty]
    public partial bool IsInReviewMode { get; set; }

    [ObservableProperty]
    public partial bool HasReviewDiff { get; set; }

    [ObservableProperty]
    public partial string? ReviewDiffMessage { get; set; }

    [ObservableProperty]
    public partial bool IsReviewApproved { get; set; }

    [ObservableProperty]
    public partial bool IsReviewRejected { get; set; }

    public bool IsReviewDecisionMade => IsReviewApproved || IsReviewRejected;

    // Review action properties (for action settings like wallpaper that appear alongside a diff)
    [ObservableProperty]
    public partial bool HasReviewAction { get; set; }

    [ObservableProperty]
    public partial string? ReviewActionMessage { get; set; }

    [ObservableProperty]
    public partial bool IsReviewActionApproved { get; set; }

    [ObservableProperty]
    public partial bool IsReviewActionRejected { get; set; }

    public bool IsReviewActionDecisionMade => IsReviewActionApproved || IsReviewActionRejected;

    public string ReviewActionGroupName => $"{SettingId}_action";

    /// <summary>
    /// Raised when the user changes the review action approval state.
    /// </summary>
    public event EventHandler<bool>? ReviewActionApprovalChanged;

    partial void OnIsReviewActionApprovedChanged(bool value)
    {
        if (value && IsReviewActionRejected)
            IsReviewActionRejected = false;

        OnPropertyChanged(nameof(IsReviewActionDecisionMade));
        ReviewActionApprovalChanged?.Invoke(this, value);
    }

    partial void OnIsReviewActionRejectedChanged(bool value)
    {
        if (value && IsReviewActionApproved)
            IsReviewActionApproved = false;

        OnPropertyChanged(nameof(IsReviewActionDecisionMade));
        if (value)
            ReviewActionApprovalChanged?.Invoke(this, false);
    }

    partial void OnIsInReviewModeChanged(bool value)
    {
        OnPropertyChanged(nameof(EffectiveIsEnabled));
    }

    partial void OnIsReviewApprovedChanged(bool value)
    {
        if (value && IsReviewRejected)
            IsReviewRejected = false;

        OnPropertyChanged(nameof(IsReviewDecisionMade));
        // Notify the ConfigReviewService when approval changes
        ReviewApprovalChanged?.Invoke(this, value);
    }

    partial void OnIsReviewRejectedChanged(bool value)
    {
        if (value && IsReviewApproved)
            IsReviewApproved = false;

        OnPropertyChanged(nameof(IsReviewDecisionMade));
        // When rejecting, notify with approved=false
        if (value)
            ReviewApprovalChanged?.Invoke(this, false);
    }

    /// <summary>
    /// Raised when the user changes the review approval state for this setting.
    /// The ConfigReviewService subscribes to this to update its approval counts.
    /// </summary>
    public event EventHandler<bool>? ReviewApprovalChanged;

    /// <summary>
    /// Clears all review mode state including event handlers.
    /// Used when exiting review mode to ensure clean state for subsequent imports.
    /// Nulls event handler first to prevent stale notifications during property resets.
    /// </summary>
    public void ClearReviewState()
    {
        // Clear event handler BEFORE resetting properties to prevent
        // OnIsReviewApprovedChanged/OnIsReviewRejectedChanged from
        // invoking stale subscribers during cleanup.
        ReviewApprovalChanged = null;
        ReviewActionApprovalChanged = null;

        IsInReviewMode = false;
        HasReviewDiff = false;
        ReviewDiffMessage = null;
        IsReviewApproved = false;
        IsReviewRejected = false;
        HasReviewAction = false;
        ReviewActionMessage = null;
        IsReviewActionApproved = false;
        IsReviewActionRejected = false;
    }

    partial void OnIsEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(EffectiveIsEnabled));
    }

    [ObservableProperty]
    public partial bool ParentIsEnabled { get; set; }

    partial void OnParentIsEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(EffectiveIsEnabled));
    }

    public bool EffectiveIsEnabled => IsEnabled && ParentIsEnabled && !IsInReviewMode;
    public bool IsToggleType => InputType == InputType.Toggle;
    public bool IsSelectionType => InputType == InputType.Selection;
    public bool IsNumericType => InputType == InputType.NumericRange;
    public bool IsActionType => InputType == InputType.Action;
    public bool IsCheckBoxType => InputType == InputType.CheckBox;
    public bool IsSubSetting => !string.IsNullOrEmpty(SettingDefinition?.ParentSettingId);

    [ObservableProperty]
    public partial ObservableCollection<SettingItemViewModel>? Children { get; set; }

    public bool IsParentSetting => Children != null && Children.Count > 0;

    [ObservableProperty]
    public partial bool IsExpanderExpanded { get; set; } = true;

    [ObservableProperty]
    public partial bool IsLastChild { get; set; }

    public Microsoft.UI.Xaml.CornerRadius ChildCornerRadius =>
        IsLastChild ? new Microsoft.UI.Xaml.CornerRadius(0, 0, 4, 4) : new Microsoft.UI.Xaml.CornerRadius(0);

    partial void OnIsLastChildChanged(bool value) => OnPropertyChanged(nameof(ChildCornerRadius));

    public void ToggleExpander(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) => IsExpanderExpanded = !IsExpanderExpanded;

    public bool IsPowerPlanSetting => InputType == InputType.Selection &&
        SettingDefinition?.Recommendation?.LoadDynamicOptions == true;

    public bool SupportsSeparateACDC =>
        SettingDefinition?.PowerCfgSettings?.Any(p =>
            p.PowerModeSupport == PowerModeSupport.Separate) == true;

    public string PluggedInText =>
        _localizationService.GetString("PowerStatus_PluggedIn") ?? "Plugged In";
    public string OnBatteryText =>
        _localizationService.GetString("PowerStatus_OnBattery") ?? "On Battery";

    public IAsyncRelayCommand ExecuteActionCommand { get; }

    public SettingItemViewModel(
        SettingItemViewModelConfig config,
        ISettingApplicationService settingApplicationService,
        ILogService logService,
        IDispatcherService dispatcherService,
        IDialogService dialogService,
        ILocalizationService localizationService,
        IEventBus? eventBus = null,
        IUserPreferencesService? userPreferencesService = null,
        IRegeditLauncher? regeditLauncher = null,
        INewBadgeService? newBadgeService = null)
    {
        _settingApplicationService = settingApplicationService;
        _logService = logService;
        _dispatcherService = dispatcherService;
        _dialogService = dialogService;
        _localizationService = localizationService;
        _userPreferencesService = userPreferencesService;
        _newBadgeService = newBadgeService;

        _localizationService.LanguageChanged += OnLanguageChanged;

        // Unpack config data
        SettingDefinition = config.SettingDefinition;
        ParentFeatureViewModel = config.ParentFeatureViewModel;
        SettingId = config.SettingId;
        Name = config.Name;
        Description = config.Description;
        GroupName = config.GroupName;
        Icon = config.Icon;
        IconPack = config.IconPack;
        InputType = config.InputType;
        IsSelected = config.IsSelected;
        OnText = config.OnText;
        OffText = config.OffText;
        ActionButtonText = config.ActionButtonText;

        // Initialize remaining defaults
        Status = string.Empty;
        ComboBoxOptions = new ObservableCollection<ComboBoxDisplayOption>();
        MaxValue = 100;
        Units = string.Empty;
        TechnicalDetails = new ObservableCollection<TechnicalDetailRow>();
        IsVisible = true;
        IsEnabled = true;
        ParentIsEnabled = true;

        ExecuteActionCommand = new AsyncRelayCommand(HandleActionAsync);
        UnlockCommand = new AsyncRelayCommand(HandleUnlockAsync);
        DismissNewBadgeCommand = new RelayCommand(() =>
        {
            IsNew = false;
            _newBadgeService?.DismissBadge(SettingId);
        });

        // Check if this setting is new in the current release
        IsNew = _newBadgeService?.IsSettingNew(
            config.SettingDefinition?.AddedInVersion, config.SettingId) == true;

        _statusBannerManager = new SettingStatusBannerManager(localizationService);
        _technicalDetailsManager = new TechnicalDetailsManager(
            () => SettingId,
            newDetails => { TechnicalDetails = newDetails; OnPropertyChanged(nameof(HasTechnicalDetails)); OnPropertyChanged(nameof(ShowTechnicalDetailsBar)); },
            logService,
            dispatcherService,
            regeditLauncher,
            eventBus,
            new TechnicalDetailLabels
            {
                Path = _localizationService.GetString("TechnicalDetails_Path") ?? "Path",
                Value = _localizationService.GetString("TechnicalDetails_Value") ?? "Value",
                Current = _localizationService.GetString("TechnicalDetails_Current") ?? "Current",
                Recommended = _localizationService.GetString("TechnicalDetails_Recommended") ?? "Recommended",
                Default = _localizationService.GetString("TechnicalDetails_DefaultValue") ?? "Default",
                ValueNotExist = _localizationService.GetString("TechnicalDetails_ValueNotExist") ?? "doesn't exist",
                On = _localizationService.GetString("Common_On") ?? "On",
                Off = _localizationService.GetString("Common_Off") ?? "Off"
            });
        OpenRegeditCommand = _technicalDetailsManager.OpenRegeditCommand;

        // Initialize badge data availability and compute initial state
        InitializeHasBadgeData();
        ComputeBadgeState();
    }

    public void UpdateVisibility(string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            IsVisible = true;
            return;
        }

        IsVisible = Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                   Description.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                   (!string.IsNullOrEmpty(GroupName) && GroupName.Contains(searchText, StringComparison.OrdinalIgnoreCase));
    }

    // Updates setting state from external events (bypasses apply logic since change already happened)
    public void UpdateStateFromEvent(bool isEnabled, object? value)
    {
        _isUpdatingFromEvent = true;
        try
        {
            if (InputType == InputType.Toggle || InputType == InputType.CheckBox)
            {
                IsSelected = isEnabled;
            }
            else if (InputType == InputType.Selection && value != null)
            {
                SelectedValue = value;
            }
            else if (InputType == InputType.NumericRange && value is int intValue)
            {
                NumericValue = intValue;
            }
        }
        finally
        {
            _isUpdatingFromEvent = false;
            ComputeBadgeState();
        }
    }

    // Updates setting state from a fresh system state read (used during navigation refresh)
    public void UpdateStateFromSystemState(SettingStateResult state)
    {
        if (!state.Success) return;
        _isUpdatingFromEvent = true;
        try
        {
            switch (InputType)
            {
                case InputType.Toggle:
                case InputType.CheckBox:
                    IsSelected = state.IsEnabled;
                    break;
                case InputType.Selection:
                    if (SupportsSeparateACDC && state.RawValues != null &&
                        SettingDefinition?.ComboBox?.Options is { } selectionOptions)
                    {
                        if (state.RawValues.TryGetValue("ACValue", out var acRaw) && acRaw != null)
                            AcValue = FindIndexForPowerCfgValue(selectionOptions, Convert.ToInt32(acRaw));
                        if (state.RawValues.TryGetValue("DCValue", out var dcRaw) && dcRaw != null)
                            DcValue = FindIndexForPowerCfgValue(selectionOptions, Convert.ToInt32(dcRaw));
                    }
                    else if (state.CurrentValue != null)
                    {
                        SelectedValue = state.CurrentValue;
                    }
                    break;
                case InputType.NumericRange:
                    if (SupportsSeparateACDC && state.RawValues != null)
                    {
                        if (state.RawValues.TryGetValue("ACValue", out var acNum) && acNum is int acInt)
                            AcNumericValue = ConvertFromSystemUnits(acInt);
                        if (state.RawValues.TryGetValue("DCValue", out var dcNum) && dcNum is int dcInt)
                            DcNumericValue = ConvertFromSystemUnits(dcInt);
                    }
                    else if (state.CurrentValue is int intValue)
                    {
                        NumericValue = ConvertFromSystemUnits(intValue);
                    }
                    break;
            }
        }
        finally
        {
            _isUpdatingFromEvent = false;
            ComputeBadgeState();
        }
    }

    private static int FindIndexForPowerCfgValue(IReadOnlyList<Winhance.Core.Features.Common.Models.ComboBoxOption> options, int targetValue)
    {
        for (int i = 0; i < options.Count; i++)
        {
            var mapping = options[i].ValueMappings;
            if (mapping != null
                && mapping.TryGetValue("PowerCfgValue", out var val)
                && val != null
                && Convert.ToInt32(val) == targetValue)
            {
                return i;
            }
        }
        return 0;
    }

    private int ConvertFromSystemUnits(int systemValue)
    {
        var displayUnits = SettingDefinition?.NumericRange?.Units;
        return UnitConversionHelper.ConvertFromSystemUnits(systemValue, displayUnits);
    }

    #region UI Event Handlers

    public void OnToggleSwitchToggled(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggle)
            HandleToggleAsync(toggle.IsOn).FireAndForget(_logService);
    }

    public void OnCheckBoxClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox)
            HandleToggleAsync(checkBox.IsChecked == true).FireAndForget(_logService);
    }

    // Announce ComboBox option changes for screen readers (arrow key navigation on closed ComboBox)
    public void OnComboBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Only announce when the user is actively interacting (keyboard-focused), not during init
        if (sender is not ComboBox comboBox || comboBox.FocusState == Microsoft.UI.Xaml.FocusState.Unfocused)
            return;

        if (e.AddedItems.Count > 0 && e.AddedItems[0] is ComboBoxDisplayOption option)
        {
            var peer = FrameworkElementAutomationPeer.FromElement(comboBox)
                       ?? FrameworkElementAutomationPeer.CreatePeerForElement(comboBox);
            peer?.RaiseNotificationEvent(
                AutomationNotificationKind.ActionCompleted,
                AutomationNotificationProcessing.CurrentThenMostRecent,
                option.DisplayText,
                "ComboBoxSelection");
        }
    }

    // Using DropDownClosed instead of SelectionChanged because SelectionChanged fires during initialization
    public void OnComboBoxDropDownClosed(object sender, object e)
    {
        if (sender is ComboBox comboBox && comboBox.SelectedValue is { } value)
            HandleValueChangedAsync(value).FireAndForget(_logService);
    }

    public void ApplySelectionValue(object value)
    {
        _logService.LogDebug($"[SettingItemViewModel] ApplySelectionValue called with value={value}, SettingId={SettingId}");
        HandleValueChangedAsync(value).FireAndForget(_logService);
    }

    public void OnNumberBoxValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs e)
    {
        if (!double.IsNaN(e.NewValue))
            HandleValueChangedAsync((int)e.NewValue).FireAndForget(_logService);
    }

    public void OnACComboBoxDropDownClosed(object sender, object e)
    {
        if (sender is ComboBox cb && cb.SelectedIndex >= 0)
        {
            AcValue = cb.SelectedIndex;
            HandleACDCSelectionChangedAsync().FireAndForget(_logService);
        }
    }

    public void OnDCComboBoxDropDownClosed(object sender, object e)
    {
        if (sender is ComboBox cb && cb.SelectedIndex >= 0)
        {
            DcValue = cb.SelectedIndex;
            HandleACDCSelectionChangedAsync().FireAndForget(_logService);
        }
    }

    public void OnACNumberBoxValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs e)
    {
        if (!double.IsNaN(e.NewValue))
        {
            AcNumericValue = (int)e.NewValue;
            HandleACDCNumericChangedAsync().FireAndForget(_logService);
        }
    }

    public void OnDCNumberBoxValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs e)
    {
        if (!double.IsNaN(e.NewValue))
        {
            DcNumericValue = (int)e.NewValue;
            HandleACDCNumericChangedAsync().FireAndForget(_logService);
        }
    }

    #endregion

    #region Apply Logic

    private async Task HandleToggleAsync(bool newValue)
    {
        if (IsApplying || _isUpdatingFromEvent || SettingDefinition == null) return;

        if (newValue == IsSelected) return;

        try
        {
            var (confirmed, checkboxChecked) = await HandleConfirmationIfNeededAsync(newValue);
            if (!confirmed)
            {
                OnPropertyChanged(nameof(IsSelected));
                return;
            }

            IsApplying = true;
            _logService.Log(LogLevel.Info, $"Toggling setting: {SettingId} to {newValue}");

            var result = await _settingApplicationService.ApplySettingAsync(new ApplySettingRequest { SettingId = SettingId, Enable = newValue, CheckboxResult = checkboxChecked });

            if (!result.Success)
            {
                _logService.Log(LogLevel.Warning, $"Setting '{SettingId}' apply failed: {result.ErrorMessage}. Reverting UI state.");
                OnPropertyChanged(nameof(IsSelected));
                return;
            }

            IsSelected = newValue;
            _hasChangedThisSession = true;
            ComputeBadgeState();
            ShowRestartBannerIfNeeded();
            _logService.Log(LogLevel.Info, $"Successfully toggled setting {SettingId} to {newValue}");
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error toggling setting {SettingId}: {ex.Message}");
            OnPropertyChanged(nameof(IsSelected));
        }
        finally
        {
            IsApplying = false;
        }
    }

    private async Task HandleValueChangedAsync(object? value)
    {
        _logService.LogDebug($"[SettingItemViewModel] HandleValueChangedAsync called: value={value}, IsApplying={IsApplying}, SettingDefinition={(SettingDefinition == null ? "null" : "not null")}, SelectedValue={SelectedValue}");

        if (_isUpdatingFromEvent || SettingDefinition == null || value == null)
        {
            _logService.LogDebug($"[SettingItemViewModel] HandleValueChangedAsync early return: _isUpdatingFromEvent={_isUpdatingFromEvent}, SettingDefinition={(SettingDefinition == null ? "null" : "not null")}, value={(value == null ? "null" : "not null")}");
            return;
        }

        // Queue the value if another apply is in progress instead of dropping it
        if (IsApplying)
        {
            _logService.LogDebug($"[SettingItemViewModel] HandleValueChangedAsync: queuing pending value {value} for {SettingId}");
            _pendingValue = value;
            return;
        }

        if (Equals(value, SelectedValue))
        {
            _logService.LogDebug($"[SettingItemViewModel] HandleValueChangedAsync: value equals SelectedValue, skipping");
            return;
        }

        _logService.LogDebug($"[SettingItemViewModel] HandleValueChangedAsync: proceeding with value change");
        try
        {
            var (confirmed, checkboxChecked) = await HandleConfirmationIfNeededAsync(value);
            if (!confirmed)
            {
                OnPropertyChanged(nameof(SelectedValue));
                OnPropertyChanged(nameof(NumericValue));
                return;
            }

            IsApplying = true;
            _logService.Log(LogLevel.Info, $"Changing value for setting: {SettingId} to {value}");
            _logService.LogDebug($"[SettingItemViewModel] Calling ApplySettingAsync for {SettingId} with value={value}");

            var result = await _settingApplicationService.ApplySettingAsync(new ApplySettingRequest { SettingId = SettingId, Enable = true, Value = value, CheckboxResult = checkboxChecked });

            _logService.LogDebug($"[SettingItemViewModel] ApplySettingAsync completed for {SettingId}");

            if (!result.Success)
            {
                _logService.Log(LogLevel.Warning, $"Setting '{SettingId}' value change failed: {result.ErrorMessage}. Reverting UI state.");
                OnPropertyChanged(nameof(SelectedValue));
                OnPropertyChanged(nameof(NumericValue));
                return;
            }

            SelectedValue = value;

            if (value is int intValue)
            {
                NumericValue = intValue;

                // Remove the Custom option once the user picks a defined value
                if (intValue != ComboBoxConstants.CustomStateIndex)
                {
                    var customOption = ComboBoxOptions.FirstOrDefault(
                        o => o.Value is int v && v == ComboBoxConstants.CustomStateIndex);
                    if (customOption != null)
                        ComboBoxOptions.Remove(customOption);
                }
            }

            _hasChangedThisSession = true;
            ComputeBadgeState();
            UpdateStatusBanner(value);
            ShowRestartBannerIfNeeded();

            _logService.Log(LogLevel.Info, $"Successfully changed value for setting {SettingId}");
            _logService.LogDebug($"[SettingItemViewModel] SelectedValue set to {value} for {SettingId}");
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error changing value for setting {SettingId}: {ex.Message}");
            OnPropertyChanged(nameof(SelectedValue));
            OnPropertyChanged(nameof(NumericValue));
        }
        finally
        {
            IsApplying = false;
            await ProcessPendingValueAsync();
        }
    }

    /// <summary>
    /// If a value change was queued while a previous apply was in progress,
    /// drain and apply it now.
    /// </summary>
    private async Task ProcessPendingValueAsync()
    {
        var pending = _pendingValue;
        _pendingValue = null;

        if (pending != null && !Equals(pending, SelectedValue))
        {
            _logService.LogDebug($"[SettingItemViewModel] Processing pending value {pending} for {SettingId}");
            await HandleValueChangedAsync(pending);
        }
    }

    private async Task HandleACDCSelectionChangedAsync()
    {
        if (IsApplying || _isUpdatingFromEvent || SettingDefinition == null) return;

        try
        {
            IsApplying = true;
            var dict = new Dictionary<string, object?> { ["ACValue"] = AcValue, ["DCValue"] = DcValue };
            _logService.Log(LogLevel.Info, $"Changing AC/DC selection for setting: {SettingId} AC={AcValue}, DC={DcValue}");
            var result = await _settingApplicationService.ApplySettingAsync(new ApplySettingRequest { SettingId = SettingId, Enable = true, Value = dict });

            if (!result.Success)
            {
                _logService.Log(LogLevel.Warning, $"Setting '{SettingId}' AC/DC selection failed: {result.ErrorMessage}. Reverting UI state.");
                OnPropertyChanged(nameof(AcValue));
                OnPropertyChanged(nameof(DcValue));
                return;
            }

            _hasChangedThisSession = true;
            ComputeBadgeState();
            ShowRestartBannerIfNeeded();
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error changing AC/DC selection for setting {SettingId}: {ex.Message}");
        }
        finally
        {
            IsApplying = false;
        }
    }

    private async Task HandleACDCNumericChangedAsync()
    {
        if (IsApplying || _isUpdatingFromEvent || SettingDefinition == null) return;

        try
        {
            IsApplying = true;
            var dict = new Dictionary<string, object?> { ["ACValue"] = AcNumericValue, ["DCValue"] = DcNumericValue };
            _logService.Log(LogLevel.Info, $"Changing AC/DC numeric for setting: {SettingId} AC={AcNumericValue}, DC={DcNumericValue}");
            var result = await _settingApplicationService.ApplySettingAsync(new ApplySettingRequest { SettingId = SettingId, Enable = true, Value = dict });

            if (!result.Success)
            {
                _logService.Log(LogLevel.Warning, $"Setting '{SettingId}' AC/DC numeric failed: {result.ErrorMessage}. Reverting UI state.");
                OnPropertyChanged(nameof(AcNumericValue));
                OnPropertyChanged(nameof(DcNumericValue));
                return;
            }

            _hasChangedThisSession = true;
            ComputeBadgeState();
            ShowRestartBannerIfNeeded();
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error changing AC/DC numeric for setting {SettingId}: {ex.Message}");
        }
        finally
        {
            IsApplying = false;
        }
    }

    private async Task HandleActionAsync()
    {
        if (IsApplying || SettingDefinition == null) return;

        try
        {
            var (confirmed, checkboxChecked) = await HandleConfirmationIfNeededAsync(null);
            if (!confirmed)
                return;

            IsApplying = true;
            _logService.Log(LogLevel.Info, $"Executing action for setting: {SettingId}");

            await _settingApplicationService.ApplySettingAsync(new ApplySettingRequest
            {
                SettingId = SettingId,
                Enable = true,
                CheckboxResult = checkboxChecked,
                CommandString = SettingDefinition.ActionCommand,
                ApplyRecommended = checkboxChecked
            });

            _logService.Log(LogLevel.Info, $"Successfully executed action for setting {SettingId}");

            if (checkboxChecked && ParentFeatureViewModel != null)
            {
                _logService.Log(LogLevel.Info, $"Refreshing parent ViewModel after applying recommended settings for {SettingId}");
                await ParentFeatureViewModel.RefreshSettingsAsync();
            }
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error executing action for setting {SettingId}: {ex.Message}");
        }
        finally
        {
            IsApplying = false;
        }
    }

    private async Task<(bool confirmed, bool checkboxChecked)> HandleConfirmationIfNeededAsync(object? value)
    {
        if (SettingDefinition == null || !SettingDefinition.RequiresConfirmation)
            return (true, false);

        var title = _localizationService.GetString($"Setting_{SettingId}_ConfirmTitle");
        var message = _localizationService.GetString($"Setting_{SettingId}_ConfirmMessage");
        var checkboxText = _localizationService.GetString($"Setting_{SettingId}_ConfirmCheckbox");

        if (SettingId == SettingIds.ThemeModeWindows && value is int comboBoxIndex)
        {
            var themeMode = comboBoxIndex == 1
                ? _localizationService.GetString("Setting_theme-mode-windows_Option_1")
                : _localizationService.GetString("Setting_theme-mode-windows_Option_0");
            message = message.Replace("{themeMode}", themeMode);
            checkboxText = checkboxText.Replace("{themeMode}", themeMode);
        }

        var continueText = _localizationService.GetString("Button_Continue");
        var cancelText = _localizationService.GetString("Button_Cancel");

        return await _dialogService.ShowConfirmationWithCheckboxAsync(
            message,
            checkboxText,
            title,
            continueText,
            cancelText);
    }

    #endregion

    #region Advanced Unlock

    private async Task HandleUnlockAsync()
    {
        if (!IsLocked) return;

        var message = _localizationService.GetString("Dialog_AdvancedPowerWarning_Message");
        var checkboxText = _localizationService.GetString("Dialog_AdvancedPowerWarning_DontShowAgain");
        var title = _localizationService.GetString("Dialog_AdvancedPowerWarning_Title");
        var unlockText = _localizationService.GetString("Button_Unlock") ?? "Unlock";
        var cancelText = _localizationService.GetString("Button_Cancel") ?? "Cancel";

        var (confirmed, dontShowAgain) = await _dialogService.ShowConfirmationWithCheckboxAsync(
            message,
            checkboxText,
            title,
            unlockText,
            cancelText);

        if (!confirmed) return;

        IsLocked = false;
        _logService.Log(LogLevel.Info, $"Unlocked advanced setting: {SettingId}");

        if (dontShowAgain && _userPreferencesService != null)
        {
            await _userPreferencesService.SetPreferenceAsync("AdvancedPowerSettingsUnlocked", true);
            _logService.Log(LogLevel.Info, "User permanently unlocked advanced power settings");

            // Unlock all other advanced settings in the same feature
            if (ParentFeatureViewModel != null)
            {
                foreach (var setting in ParentFeatureViewModel.Settings.OfType<SettingItemViewModel>())
                {
                    if (setting.RequiresAdvancedUnlock && setting != this)
                    {
                        setting.IsLocked = false;
                    }
                }
            }
        }
    }

    #endregion

    #region Status Banner

    public void InitializeCompatibilityBanner()
    {
        var banner = _statusBannerManager.GetCompatibilityBanner(SettingDefinition);
        if (banner.HasValue) ApplyBanner(banner.Value);
    }

    public void UpdateStatusBanner(object? value)
    {
        var banner = _statusBannerManager.ComputeBannerForValue(SettingDefinition, value, CrossGroupInfoMessage);
        if (banner.HasValue) ApplyBanner(banner.Value);
    }

    private void ShowRestartBannerIfNeeded()
    {
        var banner = _statusBannerManager.GetRestartBanner(SettingDefinition, _hasChangedThisSession);
        if (banner.HasValue) ApplyBanner(banner.Value);
    }

    private void ApplyBanner(SettingStatusBannerManager.BannerState state)
    {
        StatusBannerMessage = state.Message;
        StatusBannerSeverity = state.Severity;
    }

    #endregion

    #region InfoBadge State Computation

    /// <summary>
    /// Computes the badge state by comparing the current UI state against
    /// recommended and default values from the SettingDefinition.
    /// </summary>
    public void ComputeBadgeState()
    {
        if (!HasBadgeData || SettingDefinition == null)
            return;

        bool matchesRecommended = true;
        bool matchesDefault = true;

        // Check RegistrySettings
        foreach (var reg in SettingDefinition.RegistrySettings)
        {
            // For Selection settings, Recommended/Default live on ComboBoxMetadata.Options[i],
            // so reg.RecommendedValue/DefaultValue are expected to be null — don't skip.
            if (reg.RecommendedValue == null && reg.DefaultValue == null
                && InputType != InputType.Selection)
                continue;

            var (currentMatchesRecommended, currentMatchesDefault) = EvaluateRegistrySetting(reg);
            if (!currentMatchesRecommended) matchesRecommended = false;
            if (!currentMatchesDefault) matchesDefault = false;
        }

        // Check ScheduledTaskSettings
        foreach (var task in SettingDefinition.ScheduledTaskSettings)
        {
            if (task.RecommendedState.HasValue)
            {
                // For tasks, recommended typically means disabled (IsSelected=true means the optimization is on,
                // which disables the task). The RecommendedState represents whether the task should be enabled.
                bool currentTaskEnabled = !IsSelected; // Toggle ON = task disabled
                if (currentTaskEnabled != task.RecommendedState.Value)
                    matchesRecommended = false;
            }

            if (task.DefaultState.HasValue)
            {
                bool currentTaskEnabled = !IsSelected;
                if (currentTaskEnabled != task.DefaultState.Value)
                    matchesDefault = false;
            }
        }

        // Check PowerCfgSettings
        if (SettingDefinition.PowerCfgSettings != null)
        {
            foreach (var pcfg in SettingDefinition.PowerCfgSettings)
            {
                if (pcfg.PowerModeSupport == PowerModeSupport.Separate)
                {
                    if (InputType == InputType.Selection)
                    {
                        // AC/DC selection - compare indices against recommended/default PowerCfg values
                        if (pcfg.RecommendedValueAC.HasValue || pcfg.RecommendedValueDC.HasValue)
                        {
                            if (pcfg.RecommendedValueAC.HasValue && !PowerCfgIndexMatchesValue(AcValue, pcfg.RecommendedValueAC.Value))
                                matchesRecommended = false;
                            if (pcfg.RecommendedValueDC.HasValue && !PowerCfgIndexMatchesValue(DcValue, pcfg.RecommendedValueDC.Value))
                                matchesRecommended = false;
                        }
                        if (pcfg.DefaultValueAC.HasValue || pcfg.DefaultValueDC.HasValue)
                        {
                            if (pcfg.DefaultValueAC.HasValue && !PowerCfgIndexMatchesValue(AcValue, pcfg.DefaultValueAC.Value))
                                matchesDefault = false;
                            if (pcfg.DefaultValueDC.HasValue && !PowerCfgIndexMatchesValue(DcValue, pcfg.DefaultValueDC.Value))
                                matchesDefault = false;
                        }
                    }
                    else if (InputType == InputType.NumericRange)
                    {
                        if (pcfg.RecommendedValueAC.HasValue && AcNumericValue != pcfg.RecommendedValueAC.Value)
                            matchesRecommended = false;
                        if (pcfg.RecommendedValueDC.HasValue && DcNumericValue != pcfg.RecommendedValueDC.Value)
                            matchesRecommended = false;
                        if (pcfg.DefaultValueAC.HasValue && AcNumericValue != pcfg.DefaultValueAC.Value)
                            matchesDefault = false;
                        if (pcfg.DefaultValueDC.HasValue && DcNumericValue != pcfg.DefaultValueDC.Value)
                            matchesDefault = false;
                    }
                }
                else
                {
                    // Non-separate AC/DC: use the AC value as the single value
                    if (InputType == InputType.NumericRange)
                    {
                        if (pcfg.RecommendedValueAC.HasValue && NumericValue != pcfg.RecommendedValueAC.Value)
                            matchesRecommended = false;
                        if (pcfg.DefaultValueAC.HasValue && NumericValue != pcfg.DefaultValueAC.Value)
                            matchesDefault = false;
                    }
                    else if (InputType == InputType.Selection)
                    {
                        if (pcfg.RecommendedValueAC.HasValue && SelectedValue is int selVal && selVal != pcfg.RecommendedValueAC.Value)
                            matchesRecommended = false;
                        if (pcfg.DefaultValueAC.HasValue && SelectedValue is int selVal2 && selVal2 != pcfg.DefaultValueAC.Value)
                            matchesDefault = false;
                    }
                }
            }
        }

        var row = new List<BadgePillState>(capacity: 4);

        if (SettingDefinition.IsSubjectivePreference)
        {
            var (label, tooltip) = ResolvePillStrings(SettingBadgeKind.Preference);
            row.Add(new BadgePillState(SettingBadgeKind.Preference, IsHighlighted: true, label, tooltip));
        }

        if (HasAnyRecommendedData())
        {
            var (label, tooltip) = ResolvePillStrings(SettingBadgeKind.Recommended);
            row.Add(new BadgePillState(SettingBadgeKind.Recommended, IsHighlighted: matchesRecommended, label, tooltip));
        }

        if (HasAnyDefaultData())
        {
            var (label, tooltip) = ResolvePillStrings(SettingBadgeKind.Default);
            row.Add(new BadgePillState(SettingBadgeKind.Default, IsHighlighted: matchesDefault, label, tooltip));
        }

        {
            var (label, tooltip) = ResolvePillStrings(SettingBadgeKind.Custom);
            row.Add(new BadgePillState(SettingBadgeKind.Custom, IsHighlighted: !IsKnownSelectionValue(), label, tooltip));
        }

        BadgeRow = row;
    }

    private (bool matchesRecommended, bool matchesDefault) EvaluateRegistrySetting(RegistrySetting reg)
    {
        bool matchesRecommended = true;
        bool matchesDefault = true;

        if (InputType == InputType.Toggle || InputType == InputType.CheckBox)
        {
            // Resolution order for Recommended:
            //   1. SettingDefinition.RecommendedToggleState (explicit toggle-level flag)
            //   2. Per-RegistrySetting RecommendedValue mapped strictly (no null-sentinel derivation)
            //   3. null → no badge match
            // Default still derives from the null sentinel via ToggleTargetState — settings
            // that ship with the registry key absent (e.g. EnabledValue = [1, null],
            // DefaultValue = null) produce a Default badge matching the key-absent state.
            bool? recommendedState = SettingDefinition?.RecommendedToggleState
                ?? (reg.RecommendedValue == null
                    ? (bool?)null
                    : ToggleTargetState(reg.RecommendedValue, reg.EnabledValue, reg.DisabledValue));
            matchesRecommended = recommendedState == IsSelected;

            var defaultState = ToggleTargetState(reg.DefaultValue, reg.EnabledValue, reg.DisabledValue);
            matchesDefault = defaultState == IsSelected;
        }
        else if (InputType == InputType.Selection)
        {
            // Recommended/Default live on ComboBoxOption flags. Multiple options may carry
            // either flag (e.g. measurement-system marks both Metric and Imperial IsDefault
            // because the factory default depends on locale). We light the pill whenever
            // the currently-selected option has the flag — no special-casing for subjective
            // settings; the Preference pill is added separately at row-build time.
            var options = SettingDefinition.ComboBox?.Options;
            if (options != null && SelectedValue is int currentIndex
                && currentIndex >= 0 && currentIndex < options.Count)
            {
                matchesRecommended = options.Any(o => o.IsRecommended) && options[currentIndex].IsRecommended;
                matchesDefault    = options.Any(o => o.IsDefault)     && options[currentIndex].IsDefault;
            }
            else
            {
                // Unmapped value or missing options — no match. Custom pill handles the
                // "unmapped" signal at row-build level.
                matchesRecommended = false;
                matchesDefault = false;
            }
        }
        else if (InputType == InputType.NumericRange)
        {
            if (reg.RecommendedValue != null)
                matchesRecommended = ValuesEqual(NumericValue, reg.RecommendedValue);
            else
                matchesRecommended = false;

            if (reg.DefaultValue != null)
                matchesDefault = ValuesEqual(NumericValue, reg.DefaultValue);
            else
                matchesDefault = false;
        }

        return (matchesRecommended, matchesDefault);
    }

    private static bool IsValueInArray(object value, object?[]? array)
    {
        if (array == null) return false;
        return array.Any(v => ValuesEqual(value, v));
    }

    private static bool ValuesEqual(object? a, object? b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        if (Equals(a, b)) return true;

        // Handle numeric type mismatches (int vs long, etc.)
        try
        {
            var aVal = Convert.ToInt64(a);
            var bVal = Convert.ToInt64(b);
            return aVal == bVal;
        }
        catch
        {
            return string.Equals(a.ToString(), b.ToString(), StringComparison.OrdinalIgnoreCase);
        }
    }

    private bool PowerCfgIndexMatchesValue(int index, int targetPowerCfgValue)
    {
        var options = SettingDefinition?.ComboBox?.Options;
        if (options == null || index < 0 || index >= options.Count) return false;

        if (options[index].ValueMappings is { } mapping &&
            mapping.TryGetValue("PowerCfgValue", out var val) && val != null)
        {
            return Convert.ToInt32(val) == targetPowerCfgValue;
        }
        return false;
    }

    private bool HasAnyRecommendedData()
    {
        // Toggle-level explicit flag wins.
        if ((InputType == InputType.Toggle || InputType == InputType.CheckBox)
            && SettingDefinition.RecommendedToggleState.HasValue)
            return true;
        // Recommended is strict otherwise — explicit non-null per-RegistrySetting value.
        if (SettingDefinition.RegistrySettings.Any(r => r.RecommendedValue != null))
            return true;
        if (InputType == InputType.Selection
            && SettingDefinition.ComboBox?.Options?.Any(o => o.IsRecommended) == true)
            return true;
        if (SettingDefinition.ScheduledTaskSettings.Any(t => t.RecommendedState.HasValue))
            return true;
        if (SettingDefinition.PowerCfgSettings?.Any(
                p => p.RecommendedValueAC.HasValue || p.RecommendedValueDC.HasValue) == true)
            return true;
        return false;
    }

    private bool HasAnyDefaultData()
    {
        bool isToggleLike = InputType == InputType.Toggle || InputType == InputType.CheckBox;
        if (SettingDefinition.RegistrySettings.Any(r =>
                isToggleLike
                    ? ToggleTargetState(r.DefaultValue, r.EnabledValue, r.DisabledValue).HasValue
                    : r.DefaultValue != null))
            return true;
        if (InputType == InputType.Selection
            && SettingDefinition.ComboBox?.Options?.Any(o => o.IsDefault) == true)
            return true;
        if (SettingDefinition.ScheduledTaskSettings.Any(t => t.DefaultState.HasValue))
            return true;
        if (SettingDefinition.PowerCfgSettings?.Any(
                p => p.DefaultValueAC.HasValue || p.DefaultValueDC.HasValue) == true)
            return true;
        return false;
    }

    private bool IsKnownSelectionValue()
    {
        if (InputType != InputType.Selection) return true;
        var options = SettingDefinition.ComboBox?.Options;
        if (options == null || options.Count == 0) return true;
        return SelectedValue is int idx && idx >= 0 && idx < options.Count;
    }

    private (string Label, string Tooltip) ResolvePillStrings(SettingBadgeKind kind) => kind switch
    {
        SettingBadgeKind.Recommended => (
            _localizationService?.GetString(StringKeys.InfoBadge.Recommended) ?? "Recommended",
            _localizationService?.GetString(StringKeys.InfoBadge.RecommendedTooltip) ?? "Winhance's recommended value"),
        SettingBadgeKind.Default => (
            _localizationService?.GetString(StringKeys.InfoBadge.Default) ?? "Default",
            _localizationService?.GetString(StringKeys.InfoBadge.DefaultTooltip) ?? "Windows factory value"),
        SettingBadgeKind.Custom => (
            _localizationService?.GetString(StringKeys.InfoBadge.Custom) ?? "Custom",
            _localizationService?.GetString(StringKeys.InfoBadge.CustomTooltip) ?? "Custom value (not a known option)"),
        SettingBadgeKind.Preference => (
            _localizationService?.GetString(StringKeys.InfoBadge.Preference) ?? "Preference",
            _localizationService?.GetString(StringKeys.InfoBadge.PreferenceTooltip) ?? "Personal preference"),
        _ => ("", ""),
    };

    /// <summary>
    /// Initializes HasBadgeData based on whether the definition has comparable
    /// recommended/default data in RegistrySettings, ScheduledTaskSettings, or PowerCfgSettings.
    /// </summary>
    private void InitializeHasBadgeData()
    {
        if (SettingDefinition == null)
        {
            HasBadgeData = false;
            return;
        }

        // Check RegistrySettings for RecommendedValue or DefaultValue.
        // For Toggle/CheckBox the Default check uses the key-absent convention via
        // ToggleTargetState — a Default badge is "data" when EnabledValue or DisabledValue
        // contains the null sentinel even if DefaultValue itself is null. Recommended is
        // strict per-RegistrySetting; the toggle-level RecommendedToggleState flag is
        // checked separately below.
        bool isToggleLike = SettingDefinition.InputType == InputType.Toggle
            || SettingDefinition.InputType == InputType.CheckBox;
        bool hasRegistryData = SettingDefinition.RegistrySettings.Any(r =>
            r.RecommendedValue != null
            || (isToggleLike
                ? ToggleTargetState(r.DefaultValue, r.EnabledValue, r.DisabledValue).HasValue
                : r.DefaultValue != null));

        // Toggle-level explicit recommendation also counts as badge-worthy data.
        if (isToggleLike && SettingDefinition.RecommendedToggleState.HasValue)
            hasRegistryData = true;

        // Selection settings carry Recommended/Default on ComboBoxMetadata.Options[i] rather than on
        // RegistrySetting, so consider ComboBox options as badge-worthy data too.
        bool hasSelectionOptionData = SettingDefinition.InputType == InputType.Selection
            && SettingDefinition.ComboBox?.Options?.Any(o => o.IsRecommended || o.IsDefault) == true;

        // Check ScheduledTaskSettings for RecommendedState or DefaultState
        bool hasTaskData = SettingDefinition.ScheduledTaskSettings.Any(t =>
            t.RecommendedState.HasValue || t.DefaultState.HasValue);

        // Check PowerCfgSettings for RecommendedValueAC or DefaultValueAC
        bool hasPowerCfgData = SettingDefinition.PowerCfgSettings?.Any(p =>
            p.RecommendedValueAC.HasValue || p.DefaultValueAC.HasValue) == true;

        HasBadgeData = hasRegistryData || hasSelectionOptionData || hasTaskData || hasPowerCfgData;
    }

    #endregion

    #region Technical Details

    public void ToggleTechnicalDetails() => IsTechnicalDetailsExpanded = !IsTechnicalDetailsExpanded;

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(NewBadgeText));
        OnPropertyChanged(nameof(NewBadgeDismissTooltip));
        OnPropertyChanged(nameof(TechnicalDetailsLabel));
        OnPropertyChanged(nameof(OpenRegeditTooltip));
        OnPropertyChanged(nameof(ClickToUnlockText));
        OnPropertyChanged(nameof(PluggedInText));
        OnPropertyChanged(nameof(OnBatteryText));
        OnPropertyChanged(nameof(RecommendedValueTooltip));
        OnPropertyChanged(nameof(DefaultValueTooltip));
        OnPropertyChanged(nameof(RecommendedAcValueTooltip));
        OnPropertyChanged(nameof(DefaultAcValueTooltip));
        OnPropertyChanged(nameof(RecommendedDcValueTooltip));
        OnPropertyChanged(nameof(DefaultDcValueTooltip));
        OnPropertyChanged(nameof(ToggleRecommendedTooltip));
        OnPropertyChanged(nameof(ToggleDefaultTooltip));
        OnPropertyChanged(nameof(SelectionRecommendedTooltip));
        OnPropertyChanged(nameof(SelectionDefaultTooltip));
        OnPropertyChanged(nameof(AcSelectionRecommendedTooltip));
        OnPropertyChanged(nameof(AcSelectionDefaultTooltip));
        OnPropertyChanged(nameof(DcSelectionRecommendedTooltip));
        OnPropertyChanged(nameof(DcSelectionDefaultTooltip));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _localizationService.LanguageChanged -= OnLanguageChanged;
            _technicalDetailsManager.Dispose();
        }
        base.Dispose(disposing);
    }

    #endregion
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Windows.Globalization.NumberFormatting;

using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
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
    private readonly IApplicationModeService? _applicationModeService;
    private readonly SettingStatusBannerManager _statusBannerManager;
    private readonly TechnicalDetailsManager _technicalDetailsManager;
    // Live Windows build (from config), for build-aware default/badge resolution of merged Selections
    // (e.g. theme-mode-windows). Set once in the ctor before the initial badge computation.
    private readonly WinBuild _build;
    private volatile bool _isUpdatingFromEvent;
    private bool _hasChangedThisSession;
    private object? _pendingValue;

    public ISettingsFeatureViewModel? ParentFeatureViewModel { get; set; }

    public Setting? Setting { get; set; }

    /// <summary>Per-selection-option warning text from the config, index-aligned with the options
    /// (null entries = no warning). Fed to the status banner.</summary>
    public IReadOnlyList<string?>? OptionWarnings { get; }

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

    /// <summary>Detection could not place this setting on any known state (an unrecognized value).
    /// For a toggle this renders the neutral overlay + Enabled/Disabled/Cancel dialog flow; for a
    /// selection it drives the info adornment beside the ComboBox. Cleared when the user picks a real
    /// state and the apply succeeds, or a refresh resolves a known state.</summary>
    [ObservableProperty]
    public partial bool IsCustomState { get; set; }

    [ObservableProperty]
    public partial bool IsApplying { get; set; }

    [ObservableProperty]
    public partial string Status { get; set; }

    [ObservableProperty]
    public partial string? StatusBannerMessage { get; set; }

    [ObservableProperty]
    public partial InfoBarSeverity StatusBannerSeverity { get; set; }

    /// <summary>Icon override for the status banner: the QuestionCircle color icon while the Custom
    /// banner shows (coherent with the toggle overlay knob / selection adornment), null otherwise -
    /// InfoBar treats a null IconSource as "use the severity's native icon", which Warning/Error
    /// banners must keep. Set ONLY by ApplyBanner (the single banner funnel).</summary>
    [ObservableProperty]
    public partial IconSource? StatusBannerIconSource { get; set; }

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
    public partial IReadOnlyList<TechnicalDetailSection> TechnicalDetailSections { get; set; }
        = Array.Empty<TechnicalDetailSection>();

    public bool HasTechnicalDetails => TechnicalDetailSections.Count > 0;

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

    // Windows-version compatibility warning text (set by the loading bridge when the
    // version filter is off). Surfaced as a Warning banner.
    public string? CompatibilityMessage { get; set; }

    // New setting badge
    [ObservableProperty]
    public partial bool IsNew { get; set; }

    [ObservableProperty]
    public partial bool IsNewBadgeGloballyVisible { get; set; } = true;

    public string NewBadgeText => _localizationService.GetString("Badge_New") ?? "NEW";

    public bool ShowNewBadge => IsNew && IsNewBadgeGloballyVisible;

    partial void OnIsNewChanged(bool value) => OnPropertyChanged(nameof(ShowNewBadge));
    partial void OnIsNewBadgeGloballyVisibleChanged(bool value) => OnPropertyChanged(nameof(ShowNewBadge));

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
    /// Reads the Always-context Numeric recommended target (system units). Real numerics are all
    /// powercfg-separate AC/DC, so there is no Always-context value -> this returns null, as before.
    /// </summary>
    public int? NumericRecommendedValue =>
        Setting?.Numeric?.Recommended.FirstOrDefault(cv => cv.Context == PowerContext.Always) is { } cv
            ? ConvertToSystemUnits(cv.Value) : null;

    /// <summary>
    /// Default value for the single NumericRange spinner, or null if not available.
    /// Reads the Always-context Numeric default target (system units); null for powercfg-separate settings.
    /// </summary>
    public int? NumericDefaultValue =>
        Setting?.Numeric?.WindowsDefault.FirstOrDefault(cv => cv.Context == PowerContext.Always) is { } cv
            ? ConvertToSystemUnits(cv.Value) : null;

    /// <summary>
    /// AC-side recommended value for Separate PowerCfg NumericRange settings, in SYSTEM units.
    /// Reconstructed from the per-context Numeric target (display units -> system units),
    /// so the call sites' ConvertFromSystemUnits re-derives the same display value. Null when
    /// that mode carries no ContextValue.
    /// </summary>
    public int? AcRecommendedValue =>
        PairedNumericValue(Setting?.Numeric?.Recommended, PowerContext.AC);

    public int? AcDefaultValue =>
        PairedNumericValue(Setting?.Numeric?.WindowsDefault, PowerContext.AC);

    public int? DcRecommendedValue =>
        PairedNumericValue(Setting?.Numeric?.Recommended, PowerContext.DC);

    public int? DcDefaultValue =>
        PairedNumericValue(Setting?.Numeric?.WindowsDefault, PowerContext.DC);

    // Returns the per-context numeric target reconstructed to SYSTEM units. Returns null when the
    // setting is not numeric or the mode carries no matching ContextValue - a ContextValue is authored only
    // when the matching per-mode value was set, so this returns null for an absent AC/DC value.
    private int? PairedNumericValue(IReadOnlyList<ContextValue>? values, PowerContext context)
    {
        if (Setting?.Numeric is null || values is null) return null;
        foreach (var cv in values)
            if (cv.Context == context) return ConvertToSystemUnits(cv.Value);
        return null;
    }

    private string FormatValueTooltip(string key, object value)
    {
        var template = _localizationService?.GetString(key);
        if (!string.IsNullOrEmpty(template))
            return template.Replace("{0}", value?.ToString() ?? string.Empty);
        // Fallback if the key is missing
        return key == "InfoBadge_Numeric_SetToRecommended_Tooltip"
            ? $"Set to Recommended ({value})"
            : $"Set to Default ({value})";
    }

    // Tooltips — computed live so language changes flow through OnLanguageChanged.
    // NumericRange pcfg values are raw system units (e.g. Seconds); tooltips show display units.
    public string RecommendedValueTooltip =>
        NumericRecommendedValue is int rec
            ? FormatValueTooltip("InfoBadge_Numeric_SetToRecommended_Tooltip", ConvertFromSystemUnits(rec))
            : string.Empty;

    public string DefaultValueTooltip =>
        NumericDefaultValue is int def
            ? FormatValueTooltip("InfoBadge_Numeric_SetToDefault_Tooltip", ConvertFromSystemUnits(def))
            : string.Empty;

    public string RecommendedAcValueTooltip =>
        AcRecommendedValue is int rec
            ? FormatValueTooltip("InfoBadge_Numeric_SetToRecommended_Tooltip", ConvertFromSystemUnits(rec))
            : string.Empty;

    public string DefaultAcValueTooltip =>
        AcDefaultValue is int def
            ? FormatValueTooltip("InfoBadge_Numeric_SetToDefault_Tooltip", ConvertFromSystemUnits(def))
            : string.Empty;

    public string RecommendedDcValueTooltip =>
        DcRecommendedValue is int rec
            ? FormatValueTooltip("InfoBadge_Numeric_SetToRecommended_Tooltip", ConvertFromSystemUnits(rec))
            : string.Empty;

    public string DefaultDcValueTooltip =>
        DcDefaultValue is int def
            ? FormatValueTooltip("InfoBadge_Numeric_SetToDefault_Tooltip", ConvertFromSystemUnits(def))
            : string.Empty;

    // -- Accessibility names (issue #647 follow-up) ------------------------------------
    // The quick-set buttons inside SettingsCardItem inherit no context from their
    // parent SettingsCard, so Narrator was announcing only the action ("Set to
    // Recommended button") without saying which setting it applied to. These helpers
    // compose "<Setting name>: <action>" (and "<Setting name> (Plugged In|On Battery):
    // <action>" for Dual AC/DC variants) for AutomationProperties.Name. Visible
    // ToolTipService.ToolTip strings stay short (action only).
    //
    // Used via x:Bind function-call syntax in SettingsCardItem.xaml — e.g.
    //   AutomationProperties.Name="{x:Bind A11yName(ToggleRecommendedTooltip), Mode=OneWay}"
    // x:Bind re-evaluates when the argument's PropertyChanged fires (language change).

    public string A11yName(string? action) =>
        string.IsNullOrEmpty(action) ? Name : $"{Name}: {action}";

    public string A11yAcName(string? action) =>
        string.IsNullOrEmpty(action)
            ? $"{Name} ({PluggedInText})"
            : $"{Name} ({PluggedInText}): {action}";

    public string A11yDcName(string? action) =>
        string.IsNullOrEmpty(action)
            ? $"{Name} ({OnBatteryText})"
            : $"{Name} ({OnBatteryText}): {action}";

    // Direct AutomationProperties.Name source for the AC/DC input controls
    // (ComboBox / NumberBox) inside Dual templates — disambiguates the two
    // sibling controls within one setting. Single-AC/DC templates bind to Name
    // directly.
    public string AcInputAutomationName => $"{Name} ({PluggedInText})";
    public string DcInputAutomationName => $"{Name} ({OnBatteryText})";

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
                var display = ConvertFromSystemUnits(v);
                NumericValue = display;
                HandleValueChangedAsync(display).FireAndForget(_logService);
            }
        });
    private RelayCommand? _setNumericToRecommendedCommand;

    public IRelayCommand SetNumericToDefaultCommand => _setNumericToDefaultCommand ??=
        new RelayCommand(() =>
        {
            if (NumericDefaultValue is int v)
            {
                var display = ConvertFromSystemUnits(v);
                NumericValue = display;
                HandleValueChangedAsync(display, resetToDefault: true).FireAndForget(_logService);
            }
        });
    private RelayCommand? _setNumericToDefaultCommand;

    public IRelayCommand SetAcNumericToRecommendedCommand => _setAcNumericToRecommendedCommand ??=
        new RelayCommand(() =>
        {
            if (AcRecommendedValue is int v)
            {
                AcNumericValue = ConvertFromSystemUnits(v);
                HandleACDCNumericChangedAsync().FireAndForget(_logService);
            }
        });
    private RelayCommand? _setAcNumericToRecommendedCommand;

    public IRelayCommand SetAcNumericToDefaultCommand => _setAcNumericToDefaultCommand ??=
        new RelayCommand(() =>
        {
            if (AcDefaultValue is int v)
            {
                AcNumericValue = ConvertFromSystemUnits(v);
                HandleACDCNumericChangedAsync(resetToDefault: true).FireAndForget(_logService);
            }
        });
    private RelayCommand? _setAcNumericToDefaultCommand;

    public IRelayCommand SetDcNumericToRecommendedCommand => _setDcNumericToRecommendedCommand ??=
        new RelayCommand(() =>
        {
            if (DcRecommendedValue is int v)
            {
                DcNumericValue = ConvertFromSystemUnits(v);
                HandleACDCNumericChangedAsync().FireAndForget(_logService);
            }
        });
    private RelayCommand? _setDcNumericToRecommendedCommand;

    public IRelayCommand SetDcNumericToDefaultCommand => _setDcNumericToDefaultCommand ??=
        new RelayCommand(() =>
        {
            if (DcDefaultValue is int v)
            {
                DcNumericValue = ConvertFromSystemUnits(v);
                HandleACDCNumericChangedAsync(resetToDefault: true).FireAndForget(_logService);
            }
        });
    private RelayCommand? _setDcNumericToDefaultCommand;

    // ───────── Toggle quick-set buttons ─────────
    /// <summary>
    /// True if Recommended maps to the enabled state, false if disabled, null if no
    /// recommendation is set. Derived from the recommended role on the matching state.
    /// </summary>
    public bool? ToggleRecommendedState =>
        Setting is { } s ? RoleToggleState(s, RoleKind.Recommended, _build) : null;

    /// <summary>
    /// True if Default maps to the enabled state, false if disabled, null if not derivable.
    /// Derived from the WindowsDefault role on the matching state.
    /// </summary>
    public bool? ToggleDefaultState =>
        Setting is { } s ? RoleToggleState(s, RoleKind.WindowsDefault, _build) : null;

    // A toggle's recommended/default maps to whichever "Enabled"/"Disabled" state carries the role: the role-bearing
    // state's Label ("Enabled"=>true / "Disabled"=>false / no role=>null). HasRole defaults to PowerContext.Always so
    // PowerCfg AC/DC roles never match; a non-Enabled/Disabled role label (e.g. a
    // Selection) yields null - these accessors are Toggle/CheckBox-only consumed.
    private static bool? RoleToggleState(Setting setting, RoleKind kind, WinBuild build)
    {
        foreach (var st in setting.States)
            if (st.HasRole(kind, build))
                return st.Label switch { "Enabled" => true, "Disabled" => false, _ => (bool?)null };
        return null;
    }

    private string ToggleStateText(bool state) => state ? OnText : OffText;

    public string ToggleRecommendedTooltip =>
        ToggleRecommendedState is bool s
            ? FormatValueTooltip("InfoBadge_Numeric_SetToRecommended_Tooltip", ToggleStateText(s))
            : string.Empty;

    public string ToggleDefaultTooltip =>
        ToggleDefaultState is bool s
            ? FormatValueTooltip("InfoBadge_Numeric_SetToDefault_Tooltip", ToggleStateText(s))
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
                // fromCustomState while Custom: bypass the newValue==IsSelected guard (a Custom toggle
                // sits at IsSelected=false, so a Disabled target would be silently swallowed - no write,
                // no feedback) and clear the overlay on success. Quick-set is an explicit state pick,
                // so no dialog (same reasoning as the Custom dialog flow's no-double-confirm).
                HandleToggleAsync(v, fromCustomState: IsCustomState).FireAndForget(_logService);
        });
    private RelayCommand? _setToggleToRecommendedCommand;

    public IRelayCommand SetToggleToDefaultCommand => _setToggleToDefaultCommand ??=
        new RelayCommand(() =>
        {
            if (ToggleDefaultState is bool v)
                // fromCustomState while Custom: see SetToggleToRecommendedCommand.
                HandleToggleAsync(v, resetToDefault: true, fromCustomState: IsCustomState).FireAndForget(_logService);
        });
    private RelayCommand? _setToggleToDefaultCommand;

    // ───────── Selection quick-set buttons (single ComboBox) ─────────

    // Per-state roles drive recommended/default. States order matches the option order 1:1, so the index
    // matches. HasRole defaults to PowerContext.Always - standard selections match here; PowerCfg AC/DC-scoped
    // roles do NOT (their recommended/default surface via the AcSelection*/DcSelection* accessors).
    private static int? FindStateIndexWithRole(Setting setting, RoleKind kind, WinBuild build)
    {
        var states = setting.States;
        for (int i = 0; i < states.Count; i++)
            if (states[i].HasRole(kind, build)) return i;
        return null;
    }

    // Context-scoped variant for AC/DC powercfg selections. States order == option order, and the
    // option whose PowerCfgValue == RecommendedValueAC/DefaultValueDC/... carries HasRole(kind, AC/DC).
    // Null when no state carries the role.
    private static int? FindStateIndexWithRole(Setting setting, RoleKind kind, PowerContext context)
    {
        var states = setting.States;
        for (int i = 0; i < states.Count; i++)
            if (states[i].HasRole(kind, context)) return i;
        return null;
    }

    public int? SelectionRecommendedIndex =>
        Setting is { } s ? FindStateIndexWithRole(s, RoleKind.Recommended, _build) : null;
    public int? SelectionDefaultIndex =>
        Setting is { } s ? FindStateIndexWithRole(s, RoleKind.WindowsDefault, _build) : null;

    private string? OptionDisplayText(int? index)
    {
        if (index is not int i) return null;
        if (ComboBoxOptions == null || i < 0 || i >= ComboBoxOptions.Count) return null;
        return ComboBoxOptions[i].DisplayText;
    }

    public string SelectionRecommendedTooltip =>
        OptionDisplayText(SelectionRecommendedIndex) is { } label
            ? FormatValueTooltip("InfoBadge_Numeric_SetToRecommended_Tooltip", label)
            : string.Empty;

    public string SelectionDefaultTooltip =>
        OptionDisplayText(SelectionDefaultIndex) is { } label
            ? FormatValueTooltip("InfoBadge_Numeric_SetToDefault_Tooltip", label)
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
                HandleValueChangedAsync(i, resetToDefault: true).FireAndForget(_logService);
        });
    private RelayCommand? _setSelectionToDefaultCommand;

    // ───────── AC/DC Selection quick-set buttons (PowerCfg Separate + Single AC) ─────────

    public int? AcSelectionRecommendedIndex =>
        Setting is { } s ? FindStateIndexWithRole(s, RoleKind.Recommended, PowerContext.AC) : null;

    public int? AcSelectionDefaultIndex =>
        Setting is { } s ? FindStateIndexWithRole(s, RoleKind.WindowsDefault, PowerContext.AC) : null;

    public int? DcSelectionRecommendedIndex =>
        Setting is { } s ? FindStateIndexWithRole(s, RoleKind.Recommended, PowerContext.DC) : null;

    public int? DcSelectionDefaultIndex =>
        Setting is { } s ? FindStateIndexWithRole(s, RoleKind.WindowsDefault, PowerContext.DC) : null;

    public string AcSelectionRecommendedTooltip =>
        OptionDisplayText(AcSelectionRecommendedIndex) is { } label
            ? FormatValueTooltip("InfoBadge_Numeric_SetToRecommended_Tooltip", label)
            : string.Empty;

    public string AcSelectionDefaultTooltip =>
        OptionDisplayText(AcSelectionDefaultIndex) is { } label
            ? FormatValueTooltip("InfoBadge_Numeric_SetToDefault_Tooltip", label)
            : string.Empty;

    public string DcSelectionRecommendedTooltip =>
        OptionDisplayText(DcSelectionRecommendedIndex) is { } label
            ? FormatValueTooltip("InfoBadge_Numeric_SetToRecommended_Tooltip", label)
            : string.Empty;

    public string DcSelectionDefaultTooltip =>
        OptionDisplayText(DcSelectionDefaultIndex) is { } label
            ? FormatValueTooltip("InfoBadge_Numeric_SetToDefault_Tooltip", label)
            : string.Empty;

    public bool ShowAcSelectionQuickSetButtons
    {
        get
        {
            if (!IsInfoBadgeGloballyVisible) return false;
            if (InputType != InputType.Selection) return false;
            if (!IsPowerCfgSetting) return false;
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
                HandleACDCSelectionChangedAsync(resetToDefault: true).FireAndForget(_logService);
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
                HandleACDCSelectionChangedAsync(resetToDefault: true).FireAndForget(_logService);
            }
        });
    private RelayCommand? _setDcSelectionToDefaultCommand;

    // ───────── Page-level Quick Actions support (bulk recommended/defaults) ─────────

    /// <summary>
    /// True when this setting has a recommended value reachable through the quick-set
    /// pipeline. Mirrors the per-card quick-set button availability across all input
    /// types (PowerPlan is excluded — it has its own recommendation logic).
    /// </summary>
    public bool HasRecommendedQuickSetTarget => HasQuickSetTarget(recommended: true);

    /// <summary>
    /// True when this setting has a default value reachable through the quick-set pipeline.
    /// </summary>
    public bool HasDefaultQuickSetTarget => HasQuickSetTarget(recommended: false);

    private bool HasQuickSetTarget(bool recommended) => InputType switch
    {
        InputType.Toggle or InputType.CheckBox =>
            (recommended ? ToggleRecommendedState : ToggleDefaultState).HasValue,
        InputType.Selection when IsPowerCfgSetting =>
            (recommended ? AcSelectionRecommendedIndex : AcSelectionDefaultIndex).HasValue
            || (SupportsSeparateACDC && (recommended ? DcSelectionRecommendedIndex : DcSelectionDefaultIndex).HasValue),
        InputType.Selection when !IsPowerPlanSetting =>
            (recommended ? SelectionRecommendedIndex : SelectionDefaultIndex).HasValue,
        InputType.NumericRange when SupportsSeparateACDC =>
            (recommended ? AcRecommendedValue : AcDefaultValue).HasValue
            || (recommended ? DcRecommendedValue : DcDefaultValue).HasValue,
        InputType.NumericRange =>
            (recommended ? NumericRecommendedValue : NumericDefaultValue).HasValue,
        _ => false
    };

    /// <summary>
    /// Sets this setting's UI to its recommended value by executing the same commands as
    /// the per-card quick-set buttons. Every path runs through the guarded apply pipeline,
    /// so in Builder mode this records a builder edit and never touches the system.
    /// Returns true when a recommended target existed.
    /// </summary>
    public bool TrySetToRecommended() => TryExecuteQuickSet(recommended: true);

    /// <summary>
    /// Sets this setting's UI to its default value via the quick-set pipeline.
    /// See <see cref="TrySetToRecommended"/> for Builder-mode semantics.
    /// </summary>
    public bool TrySetToDefault() => TryExecuteQuickSet(recommended: false);

    private bool TryExecuteQuickSet(bool recommended)
    {
        if (!HasQuickSetTarget(recommended)) return false;

        switch (InputType)
        {
            case InputType.Toggle:
            case InputType.CheckBox:
                (recommended ? SetToggleToRecommendedCommand : SetToggleToDefaultCommand).Execute(null);
                return true;

            case InputType.Selection when IsPowerCfgSetting:
                (recommended ? SetAcSelectionToRecommendedCommand : SetAcSelectionToDefaultCommand).Execute(null);
                if (SupportsSeparateACDC)
                    (recommended ? SetDcSelectionToRecommendedCommand : SetDcSelectionToDefaultCommand).Execute(null);
                return true;

            case InputType.Selection:
                (recommended ? SetSelectionToRecommendedCommand : SetSelectionToDefaultCommand).Execute(null);
                return true;

            case InputType.NumericRange when SupportsSeparateACDC:
                (recommended ? SetAcNumericToRecommendedCommand : SetAcNumericToDefaultCommand).Execute(null);
                (recommended ? SetDcNumericToRecommendedCommand : SetDcNumericToDefaultCommand).Execute(null);
                return true;

            case InputType.NumericRange:
                (recommended ? SetNumericToRecommendedCommand : SetNumericToDefaultCommand).Execute(null);
                return true;

            default:
                return false;
        }
    }


    // Advanced unlock support
    [ObservableProperty]
    public partial bool IsLocked { get; set; }

    public bool RequiresAdvancedUnlock =>
        Setting?.Availability.RequiresAdvancedUnlock ?? false;
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

        // When entering Review Mode, force every expander open so children carrying
        // review diffs are visible. A parent collapsed before import would otherwise
        // hide its children behind a disabled card and Apply Config would stay gated.
        // The user can still toggle the chevron overlay to collapse a subtree after.
        if (value)
            IsExpanderExpanded = true;
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

    // Builder mode records desired state into the UI without applying to the system.
    private bool IsBuilderMode => _applicationModeService?.CurrentMode == WinhanceMode.Builder;

    /// <summary>
    /// When this Selection setting was seeded at the Custom index (live state matched no
    /// predefined option), the raw state values captured at seed time. Used by Builder-mode
    /// serialization to emit the custom value without re-reading the system. Null otherwise.
    /// </summary>
    public Dictionary<string, object>? CapturedCustomStateValues { get; set; }
    /// <summary>The UI parent this setting nests under: its UiParentId. Null = top-level.
    /// Single source for IsSubSetting and the parent-child tree-build in BaseSettingsFeatureViewModel.</summary>
    public string? EffectiveUiParentId => Setting?.UiParentId;

    public bool IsSubSetting => !string.IsNullOrEmpty(EffectiveUiParentId);

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

    public bool IsPowerPlanSetting => Setting?.OptionSource is not null;

    // A powercfg setting carries exactly one PowerCfgTarget whose Mode is PowerModeSupport. Non-powercfg
    // settings have no PowerCfgTarget -> false.
    public bool SupportsSeparateACDC =>
        Setting?.Targets.OfType<PowerCfgTarget>().FirstOrDefault()?.Mode == PowerModeSupport.Separate;

    private bool IsPowerCfgSetting =>
        Setting?.Targets.OfType<PowerCfgTarget>().Any() == true;

    /// <summary>The localized "Custom" state text - the toggle overlay's tooltip/automation name and
    /// the a11y state announcement (SettingsCardItem.GetSettingStateText).</summary>
    public string CustomStateText =>
        _localizationService.GetString("Common_CustomState") ?? "Custom";

    /// <summary>Tooltip for the selection Custom info adornment (the selection Custom banner string).</summary>
    public string CustomStateSelectionTooltip =>
        _localizationService.GetString("Common_CustomBanner_Selection") ?? string.Empty;

    public string PluggedInText =>
        _localizationService.GetString("PowerStatus_PluggedIn") ?? "Plugged In";
    public string OnBatteryText =>
        _localizationService.GetString("PowerStatus_OnBattery") ?? "On Battery";

    public IAsyncRelayCommand RunActionCommand { get; }

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
        INewBadgeService? newBadgeService = null,
        IApplicationModeService? applicationModeService = null)
    {
        _settingApplicationService = settingApplicationService;
        _logService = logService;
        _dispatcherService = dispatcherService;
        _dialogService = dialogService;
        _localizationService = localizationService;
        _userPreferencesService = userPreferencesService;
        _newBadgeService = newBadgeService;
        _applicationModeService = applicationModeService;

        _localizationService.LanguageChanged += OnLanguageChanged;

        // Unpack config data
        Setting = config.Setting;
        _build = config.Build;
        OptionWarnings = config.OptionWarnings;
        ParentFeatureViewModel = config.ParentFeatureViewModel;
        SettingId = config.SettingId;
        Name = config.Name;
        Description = config.Description;
        GroupName = config.GroupName;
        Icon = config.Icon;
        IconPack = config.IconPack;
        InputType = config.InputType;
        IsSelected = config.IsSelected;
        IsCustomState = config.IsCustomState;
        OnText = config.OnText;
        OffText = config.OffText;
        ActionButtonText = config.ActionButtonText;

        // Initialize remaining defaults
        Status = string.Empty;
        ComboBoxOptions = new ObservableCollection<ComboBoxDisplayOption>();
        MaxValue = 100;
        Units = string.Empty;
        IsVisible = true;
        IsEnabled = true;
        ParentIsEnabled = true;

        RunActionCommand = new AsyncRelayCommand(RunActionAsync);
        UnlockCommand = new AsyncRelayCommand(HandleUnlockAsync);

        // Check if this setting is new in the current release
        IsNew = _newBadgeService?.IsSettingNew(
            config.Setting.Display.AddedInVersion, config.SettingId) == true;

        _statusBannerManager = new SettingStatusBannerManager(localizationService);
        _technicalDetailsManager = new TechnicalDetailsManager(
            () => SettingId,
            newSections =>
            {
                TechnicalDetailSections = newSections;
                OnPropertyChanged(nameof(HasTechnicalDetails));
                OnPropertyChanged(nameof(ShowTechnicalDetailsBar));
            },
            logService,
            dispatcherService,
            regeditLauncher,
            eventBus,
            _localizationService,
            _build,
            new TechnicalDetailLabels
            {
                Path = _localizationService.GetString("TechnicalDetails_Path") ?? "Path",
                Value = _localizationService.GetString("TechnicalDetails_Value") ?? "Value",
                Current = _localizationService.GetString("TechnicalDetails_Current") ?? "Current",
                Recommended = _localizationService.GetString("TechnicalDetails_Recommended") ?? "Recommended",
                Default = _localizationService.GetString("TechnicalDetails_DefaultValue") ?? "Default",
                ValueNotExist = _localizationService.GetString("TechnicalDetails_ValueNotExist") ?? "doesn't exist",
                On = _localizationService.GetString("Common_On") ?? "On",
                Off = _localizationService.GetString("Common_Off") ?? "Off",
                SectionRegistry = _localizationService.GetString("TechnicalDetails_Section_Registry") ?? "Registry Changes",
                SectionScheduledTasks = _localizationService.GetString("TechnicalDetails_Section_ScheduledTasks") ?? "Scheduled Tasks",
                SectionPowerSettings = _localizationService.GetString("TechnicalDetails_Section_PowerSettings") ?? "Power Settings",
                SectionScripts = _localizationService.GetString("TechnicalDetails_Section_Scripts") ?? "PowerShell Scripts",
                SectionRegContent = _localizationService.GetString("TechnicalDetails_Section_RegContent") ?? "Registry Content",
                SectionDependencies = _localizationService.GetString("TechnicalDetails_Section_Dependencies") ?? "Depends On",
                SectionOptions = _localizationService.GetString("TechnicalDetails_Section_Options") ?? "Options",
                OrNotSet = _localizationService.GetString("TechnicalDetails_OrNotSet") ?? "or not set",
                DeletesKey = _localizationService.GetString("TechnicalDetails_DeletesKey") ?? "deletes key",
                ScriptOnEnable = _localizationService.GetString("TechnicalDetails_Script_OnEnable") ?? "On Enable",
                ScriptOnDisable = _localizationService.GetString("TechnicalDetails_Script_OnDisable") ?? "On Disable",
                ScriptOnApply = _localizationService.GetString("TechnicalDetails_Script_OnApply") ?? "On Apply",
                RegContentOnEnable = _localizationService.GetString("TechnicalDetails_RegContent_OnEnable") ?? "On Enable",
                RegContentOnDisable = _localizationService.GetString("TechnicalDetails_RegContent_OnDisable") ?? "On Disable",
                DependencyEquals = _localizationService.GetString("TechnicalDetails_Dependency_Equals") ?? "=",
                DependencyNotEquals = _localizationService.GetString("TechnicalDetails_Dependency_NotEquals") ?? "≠",
                PowerCfgSubgroup = _localizationService.GetString("TechnicalDetails_PowerCfg_Subgroup") ?? "Subgroup",
                PowerCfgSetting  = _localizationService.GetString("TechnicalDetails_PowerCfg_Setting") ?? "Setting"
            });
        OpenRegeditCommand = _technicalDetailsManager.OpenRegeditCommand;

        // Initialize badge data availability and compute initial state
        InitializeHasBadgeData();
        ComputeBadgeState();
    }

    /// <summary>
    /// Rebuilds the technical-details panel from the <see cref="Setting"/> model + the VM's resolved
    /// current state (no live registry reads). Called after the factory finishes populating state and after
    /// every system-state / event refresh.
    /// </summary>
    public void RefreshTechnicalDetails()
    {
        var snapshot = new TechnicalDetailsSnapshot(
            InputType,
            IsSelected,
            SelectedValue as int?,
            NumericValue,
            AcValue,
            DcValue,
            AcNumericValue,
            DcNumericValue,
            SupportsSeparateACDC,
            HasBattery,
            new List<ComboBoxDisplayOption>(ComboBoxOptions));
        _technicalDetailsManager.Update(Setting, snapshot);
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
                // An EXTERNAL apply (config import, bulk apply, relationship refresh) just wrote a known
                // state - the toggle is no longer Custom. Guarded with !IsApplying: the service publishes
                // SettingAppliedEvent even on partial FAILURE, and for a self-apply the event bounce is
                // enqueued to the dispatcher before the apply continuation resumes (EventBus.Publish runs
                // inline inside ApplySettingAsync; RunOnUIThread enqueues from the background thread), so
                // it lands here while IsApplying is still true. During a self-apply the VM's own
                // success/failure path owns the flag - an unguarded clear would wipe the overlay a failed
                // Custom-dialog apply deliberately keeps.
                if (!IsApplying)
                    IsCustomState = false;
            }
            else if (InputType == InputType.Selection)
            {
                // AC/DC separate selection: value is Dictionary { "ACValue": acIdx, "DCValue": dcIdx }
                // (what SettingItemViewModel and BulkSettingsActionService send for Separate PowerCfg).
                if (SupportsSeparateACDC && value is System.Collections.Generic.Dictionary<string, object?> selDict)
                {
                    if (selDict.TryGetValue("ACValue", out var ac) && TryReadInt(ac, out var acIdx))
                        AcValue = acIdx;
                    // On non-battery systems PowerCfgApplier skips the DC write — don't pretend
                    // we applied a DC value the system never received. Otherwise a subsequent
                    // system-state refresh would visibly correct the VM and flip the badge.
                    if (HasBattery && selDict.TryGetValue("DCValue", out var dc) && TryReadInt(dc, out var dcIdx))
                        DcValue = dcIdx;
                    // The external AC/DC apply landed on known option indices - no longer Custom
                    // (same self-apply guard as the toggle branch above).
                    if (!IsApplying)
                        IsCustomState = false;
                }
                else if (value != null)
                {
                    SelectedValue = value;
                    // A real option index means the selection is no longer Custom (mirrors the toggle
                    // branch, same self-apply guard). A Custom sentinel or non-index payload leaves it.
                    if (!IsApplying && value is int realIdx && realIdx != ComboBoxConstants.CustomStateIndex)
                        IsCustomState = false;
                }
            }
            else if (InputType == InputType.NumericRange)
            {
                // AC/DC separate numeric: value is Dictionary in display units (Minutes, %, etc.).
                if (SupportsSeparateACDC && value is System.Collections.Generic.Dictionary<string, object?> numDict)
                {
                    if (numDict.TryGetValue("ACValue", out var ac) && TryReadInt(ac, out var acNum))
                        AcNumericValue = acNum;
                    if (HasBattery && numDict.TryGetValue("DCValue", out var dc) && TryReadInt(dc, out var dcNum))
                        DcNumericValue = dcNum;
                }
                else if (value is int intValue)
                {
                    NumericValue = intValue;
                }
            }
        }
        finally
        {
            _isUpdatingFromEvent = false;
            ComputeBadgeState();
            UpdateCustomStateBanner();
            RefreshTechnicalDetails();
        }
    }

    private static bool TryReadInt(object? value, out int result)
    {
        switch (value)
        {
            case int i: result = i; return true;
            case long l: result = (int)l; return true;
            case double d: result = (int)d; return true;
            case float f: result = (int)f; return true;
            case string s when int.TryParse(s, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed): result = parsed; return true;
            default:
                if (value != null)
                {
                    try { result = Convert.ToInt32(value); return true; }
                    catch { }
                }
                result = 0; return false;
        }
    }

    // Updates setting state from a fresh system state read (used during navigation refresh)
    /// <summary>Rebuilds the runtime (non-Builder) power-plan dropdown from the detection
    /// result's DynamicOptions/DynamicSelection, reconstructing the rich PowerPlanComboBoxOption Tag the bespoke
    /// PowerPlanComboBox control reads (status dot / [Active] badge / delete-by-GUID). Shared by SettingViewModelFactory
    /// (initial load) and UpdateStateFromSystemState (refresh) so the dropdown is rebuilt identically from detection on
    /// BOTH paths - the dropdown is detection-driven, not combobox-service-driven.
    /// Returns true when it handled the dropdown (a power-plan Selection with DynamicOptions, not Builder mode); false so
    /// the caller falls through to normal Selection handling. Builder mode keeps the factory's index-valued dropdown
    /// (config-export BuilderEdit serialization), so this returns false there.</summary>
    public bool TryApplyDynamicPowerPlanOptions(SettingStateResult state)
    {
        if (InputType != InputType.Selection
            || !IsPowerPlanSetting
            || IsBuilderMode
            || state.DynamicOptions is not { } dynamicOptions)
            return false;

        ComboBoxOptions.Clear();

        foreach (var opt in dynamicOptions)
        {
            var label = opt.Label.StartsWith("PowerPlan_")
                ? _localizationService.GetString(opt.Label)
                : opt.Label;

            var isActive = state.DynamicSelection != null
                && string.Equals(opt.Value, state.DynamicSelection, StringComparison.OrdinalIgnoreCase);

            // The PowerPlanComboBox control + the delete path read these off the Tag: ExistsOnSystem/IsActive drive the
            // visuals, SystemPlan.Guid is the delete target (null for a not-installed predefined plan so its delete
            // button stays hidden), DisplayName carries the raw loc key (the delete dialog re-localizes it).
            var tag = new PowerPlanComboBoxOption
            {
                DisplayName = opt.Label,
                ExistsOnSystem = opt.ExistsOnSystem,
                IsActive = isActive,
                SystemPlan = opt.ExistsOnSystem
                    ? new Winhance.Core.Features.Optimize.Models.PowerPlan { Guid = opt.Value, Name = label, IsActive = isActive }
                    : null,
            };

            ComboBoxOptions.Add(new ComboBoxDisplayOption(
                label,
                opt.Value,
                opt.ExistsOnSystem ? "Installed on system" : "Not installed",
                tag));
        }

        // The stored selection is the active scheme GUID (default to the first option when the active plan is
        // unreadable, mirroring the factory's load-time fallback).
        SelectedValue = state.DynamicSelection ?? dynamicOptions.FirstOrDefault()?.Value;
        UpdateStatusBanner(SelectedValue);
        return true;
    }

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
                    IsCustomState = state.IsCustomState;
                    break;
                case InputType.Selection:
                    // Power-plan settings rebuild their dropdown from the detection result's DynamicOptions on refresh,
                    // the same way the factory builds it on load. This fixes the latent clobber where the
                    // generic `SelectedValue = state.CurrentValue` below set the wrong value for a power plan (its
                    // CurrentValue is not the active scheme GUID).
                    if (TryApplyDynamicPowerPlanOptions(state))
                        break;

                    // The detection result's flag is the source of truth on refresh (mirrors the factory load).
                    IsCustomState = state.IsCustomState;

                    if (SupportsSeparateACDC && Setting is { States.Count: > 0 } sel
                        && sel.Targets.OfType<PowerCfgTarget>().FirstOrDefault() is { } powerTarget)
                    {
                        if (state.AcValue is int acRaw)
                            AcValue = FindStateIndexForPowerCfgValue(sel, powerTarget.Key, acRaw) ?? ComboBoxConstants.CustomStateIndex;
                        if (state.DcValue is int dcRaw)
                            DcValue = FindStateIndexForPowerCfgValue(sel, powerTarget.Key, dcRaw) ?? ComboBoxConstants.CustomStateIndex;
                    }
                    else if (state.CurrentValue != null)
                    {
                        // Keep the synthetic "Custom" option in sync with the re-detected index: append it when the
                        // reading resolves to Custom and none exists (else the ComboBox binds a -1 with no matching
                        // item and renders BLANK - mirrors the factory's load-time append), and drop it once the
                        // reading is a real option (mirrors HandleValueChangedAsync). The whole method already runs
                        // under _isUpdatingFromEvent, guarding these programmatic control updates.
                        bool isCustomIndex = state.CurrentValue is int ci && ci == ComboBoxConstants.CustomStateIndex;
                        if (isCustomIndex)
                            EnsureCustomOption();
                        SelectedValue = state.CurrentValue;
                        if (!isCustomIndex)
                            RemoveCustomOption();
                    }
                    break;
                case InputType.NumericRange:
                    if (SupportsSeparateACDC)
                    {
                        if (state.AcValue is int acInt)
                            AcNumericValue = ConvertFromSystemUnits(acInt);
                        if (state.DcValue is int dcInt)
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
            UpdateCustomStateBanner();
            RefreshTechnicalDetails();
        }
    }

    // Mirrors SettingViewModelFactory.BuildCatalogSelectionOptions' synthetic "Custom" option: appends one option
    // whose Value is the Custom sentinel when a re-detect resolves outside the known options and none exists yet,
    // so the ComboBox has an item to bind the -1 selection to instead of rendering blank.
    private void EnsureCustomOption()
    {
        if (ComboBoxOptions.Any(o => o.Value is int v && v == ComboBoxConstants.CustomStateIndex))
            return;
        ComboBoxOptions.Add(new ComboBoxDisplayOption(LocalizeCustomOptionLabel(), ComboBoxConstants.CustomStateIndex, null));
    }

    // Mirrors HandleValueChangedAsync: removes the synthetic "Custom" option once a real option index is selected.
    private void RemoveCustomOption()
    {
        var customOption = ComboBoxOptions.FirstOrDefault(o => o.Value is int v && v == ComboBoxConstants.CustomStateIndex);
        if (customOption != null)
            ComboBoxOptions.Remove(customOption);
    }

    // The localized "Custom" option label, mirroring SettingViewModelFactory's resolution:
    // Setting_{id}_Option_Custom, then the generic Common_CustomState, then the literal "Custom".
    private string LocalizeCustomOptionLabel()
    {
        string? Localized(string key)
        {
            var text = _localizationService.GetString(key);
            return (text.Length >= 2 && text[0] == '[' && text[^1] == ']') ? null : text;
        }
        return Localized($"Setting_{SettingId}_Option_Custom") ?? Localized("Common_CustomState") ?? "Custom";
    }

    // Maps a raw powercfg value (the AC or DC reading) to the State index whose Set[powerKey] accepts it.
    // Returns null when no option matches.
    private static int? FindStateIndexForPowerCfgValue(Setting setting, string powerKey, int rawValue)
    {
        var states = setting.States;
        for (int i = 0; i < states.Count; i++)
        {
            if (states[i].Set.TryGetValue(powerKey, out var stateValue)
                && stateValue.Matches(rawValue, present: true))
                return i;
        }
        return null;
    }

    private int ConvertFromSystemUnits(int systemValue)
    {
        var displayUnits = Setting?.Numeric?.Units;
        return UnitConversionHelper.ConvertFromSystemUnits(systemValue, displayUnits);
    }

    // Inverse of ConvertFromSystemUnits, used only by the numeric accessors. The catalog stores per-context
    // numeric targets in DISPLAY units, so reconstructing the system value here lets each call site's
    // ConvertFromSystemUnits re-derive the same display value. Units come from the same model the
    // ContextValue was built with (Setting.Numeric.Units), so the display->system->display round trip is exact.
    private int ConvertToSystemUnits(int displayValue)
    {
        var displayUnits = Setting?.Numeric?.Units;
        return UnitConversionHelper.ConvertToSystemUnits(displayValue, displayUnits);
    }

    #region UI Event Handlers

    public void OnToggleSwitchToggled(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggle)
            HandleToggleAsync(toggle.IsOn).FireAndForget(_logService);
    }

    /// <summary>Click handler for the neutral Custom-state toggle overlay (SettingsCardItem's toggle
    /// template renders it instead of the ToggleSwitch while IsCustomState).</summary>
    public void OnCustomToggleClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        HandleCustomToggleClickAsync().FireAndForget(_logService);
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

    /// <summary>
    /// Sets an invariant-culture NumberFormatter on a NumberBox so that it formats
    /// and parses values using en-US conventions regardless of the Windows system locale.
    /// This prevents locale-sensitive formatting (e.g. Russian "50.000" for 50000)
    /// from causing incorrect values when the user interacts with the control.
    /// </summary>
    public void OnNumberBoxLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is NumberBox nb)
            nb.NumberFormatter = CreateInvariantNumberFormatter();
    }

    private static DecimalFormatter CreateInvariantNumberFormatter()
    {
        var formatter = new DecimalFormatter(new[] { "en-US" }, "US")
        {
            FractionDigits = 0,
            IsGrouped = false
        };
        return formatter;
    }

    #endregion

    #region Apply Logic

    private async Task HandleToggleAsync(bool newValue, bool resetToDefault = false, bool fromCustomState = false)
    {
        if (IsApplying || _isUpdatingFromEvent) return;

        // A Custom-state pick bypasses the equality guard: a Custom toggle sits at IsSelected=false,
        // so picking Disabled (false) would otherwise be swallowed here.
        if (!fromCustomState && newValue == IsSelected) return;

        if (IsBuilderMode)
        {
            // Builder mode: record the desired state only — never apply to the system,
            // never confirm, never show a restart banner.
            IsSelected = newValue;
            if (fromCustomState)
            {
                IsCustomState = false;
                UpdateCustomStateBanner();
            }
            _hasChangedThisSession = true;
            ComputeBadgeState();
            _applicationModeService?.RecordBuilderEdit(new BuilderEdit
            {
                SettingId = SettingId,
                InputType = InputType,
                IsSelected = newValue
            });
            return;
        }

        try
        {
            bool checkboxChecked = false;
            if (!fromCustomState)
            {
                // The Custom-state dialog already confirmed intent - never double-confirm.
                var (confirmed, cb) = await HandleConfirmationIfNeededAsync(newValue);
                if (!confirmed)
                {
                    OnPropertyChanged(nameof(IsSelected));
                    return;
                }
                checkboxChecked = cb;
            }

            IsApplying = true;
            _logService.Log(LogLevel.Info, $"Toggling setting: {SettingId} to {newValue}");

            var result = await _settingApplicationService.ApplySettingAsync(new ApplySettingRequest { SettingId = SettingId, Enable = newValue, ResetToDefault = resetToDefault, CheckboxResult = checkboxChecked });

            if (!result.Success)
            {
                _logService.Log(LogLevel.Warning, $"Setting '{SettingId}' apply failed: {result.ErrorMessage}. Reverting UI state.");
                OnPropertyChanged(nameof(IsSelected));
                return;
            }

            IsSelected = newValue;
            if (fromCustomState)
            {
                // On failure/exception the flag stays true (value untouched, overlay stays).
                IsCustomState = false;
                UpdateCustomStateBanner();
            }
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

    /// <summary>The Custom-state dialog flow: title = the setting's display name, message explains the
    /// unrecognized value, buttons Enabled / Disabled / Cancel with Cancel as the default (safe Enter).
    /// Cancel keeps the unrecognized value and the Custom rendering. A pick applies EXACTLY once via
    /// HandleToggleAsync(fromCustomState: true): no second confirmation, equality guard bypassed. Only
    /// reachable from Custom - afterwards the toggle renders and behaves normally.</summary>
    private async Task HandleCustomToggleClickAsync()
    {
        if (IsApplying || !IsCustomState) return;

        var r = await _dialogService.ShowConfirmationAsync(new ConfirmationRequest
        {
            Title = Name,
            Message = _localizationService.GetString("Common_CustomDialog_Message") ?? string.Empty,
            ConfirmButtonText = _localizationService.GetString("Common_CustomDialog_Enabled") ?? "Enabled",
            SecondaryButtonText = _localizationService.GetString("Common_CustomDialog_Disabled") ?? "Disabled",
            CancelButtonText = _localizationService.GetString("Button_Cancel") ?? "Cancel",
        });

        if (r.Confirmed)
            await HandleToggleAsync(true, fromCustomState: true);
        else if (r.SecondaryChosen)
            await HandleToggleAsync(false, fromCustomState: true);
        // Cancel: keep the unrecognized value and the Custom rendering.
    }

    private async Task HandleValueChangedAsync(object? value, bool resetToDefault = false)
    {
        _logService.LogDebug($"[SettingItemViewModel] HandleValueChangedAsync called: value={value}, IsApplying={IsApplying}, SelectedValue={SelectedValue}");

        if (_isUpdatingFromEvent || value == null)
        {
            _logService.LogDebug($"[SettingItemViewModel] HandleValueChangedAsync early return: _isUpdatingFromEvent={_isUpdatingFromEvent}, value={(value == null ? "null" : "not null")}");
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

        if (IsBuilderMode)
        {
            // Builder mode: record the desired selection only — never apply.
            SelectedValue = value;
            if (value is int builderIntValue)
            {
                NumericValue = builderIntValue;
                if (builderIntValue != ComboBoxConstants.CustomStateIndex)
                {
                    IsCustomState = false;
                    var customOption = ComboBoxOptions.FirstOrDefault(
                        o => o.Value is int v && v == ComboBoxConstants.CustomStateIndex);
                    if (customOption != null)
                        ComboBoxOptions.Remove(customOption);
                }
            }
            _hasChangedThisSession = true;
            ComputeBadgeState();
            UpdateStatusBanner(value);

            // Only Selection settings are serialized from Builder edits today; numeric and
            // AC/DC power edits fall back to the seeded value (see BuilderEdit scope note).
            if (InputType == InputType.Selection && value is int builderSelIndex)
            {
                _applicationModeService?.RecordBuilderEdit(new BuilderEdit
                {
                    SettingId = SettingId,
                    InputType = InputType,
                    SelectedIndex = builderSelIndex == ComboBoxConstants.CustomStateIndex ? null : builderSelIndex,
                    CustomStateValues = builderSelIndex == ComboBoxConstants.CustomStateIndex ? CapturedCustomStateValues : null
                });
            }
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

            var result = await _settingApplicationService.ApplySettingAsync(new ApplySettingRequest { SettingId = SettingId, Enable = true, Value = value, ResetToDefault = resetToDefault, CheckboxResult = checkboxChecked });

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
                    IsCustomState = false;
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

    private async Task HandleACDCSelectionChangedAsync(bool resetToDefault = false)
    {
        if (IsApplying || _isUpdatingFromEvent) return;

        if (IsBuilderMode)
        {
            // Builder mode: AcValue/DcValue are already set by the caller; just record. A pick of known
            // indices also clears a loaded Custom state (mirrors the toggle builder branch).
            IsCustomState = false;
            UpdateCustomStateBanner();
            _hasChangedThisSession = true;
            ComputeBadgeState();
            return;
        }

        try
        {
            IsApplying = true;
            var dict = new Dictionary<string, object?> { ["ACValue"] = AcValue, ["DCValue"] = DcValue };
            _logService.Log(LogLevel.Info, $"Changing AC/DC selection for setting: {SettingId} AC={AcValue}, DC={DcValue}");
            var result = await _settingApplicationService.ApplySettingAsync(new ApplySettingRequest { SettingId = SettingId, Enable = true, Value = dict, ResetToDefault = resetToDefault });

            if (!result.Success)
            {
                _logService.Log(LogLevel.Warning, $"Setting '{SettingId}' AC/DC selection failed: {result.ErrorMessage}. Reverting UI state.");
                OnPropertyChanged(nameof(AcValue));
                OnPropertyChanged(nameof(DcValue));
                return;
            }

            _hasChangedThisSession = true;
            // A successful AC/DC apply landed on known option indices - clear a loaded Custom state and
            // its "Select an option" banner (mirrors the toggle/selection apply paths).
            IsCustomState = false;
            UpdateCustomStateBanner();
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

    private async Task HandleACDCNumericChangedAsync(bool resetToDefault = false)
    {
        if (IsApplying || _isUpdatingFromEvent) return;

        if (IsBuilderMode)
        {
            // Builder mode: AcNumericValue/DcNumericValue are already set by the caller; just record.
            _hasChangedThisSession = true;
            ComputeBadgeState();
            return;
        }

        try
        {
            IsApplying = true;
            var dict = new Dictionary<string, object?> { ["ACValue"] = AcNumericValue, ["DCValue"] = DcNumericValue };
            _logService.Log(LogLevel.Info, $"Changing AC/DC numeric for setting: {SettingId} AC={AcNumericValue}, DC={DcNumericValue}");
            var result = await _settingApplicationService.ApplySettingAsync(new ApplySettingRequest { SettingId = SettingId, Enable = true, Value = dict, ResetToDefault = resetToDefault });

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

    private async Task RunActionAsync()
    {
        if (IsApplying) return;

        if (IsBuilderMode)
        {
            // Builder mode: mark the action for inclusion in the saved config; do not execute.
            IsSelected = true;
            _hasChangedThisSession = true;
            ComputeBadgeState();
            _applicationModeService?.RecordBuilderEdit(new BuilderEdit
            {
                SettingId = SettingId,
                InputType = InputType,
                IsSelected = true
            });
            return;
        }

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
        bool requiresConfirmation =
            Setting?.Apply.RequiresConfirmation ?? false;
        if (!requiresConfirmation)
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

        var r = await _dialogService.ShowConfirmationAsync(new ConfirmationRequest
        {
            Message = message,
            CheckboxText = checkboxText,
            Title = title,
            ConfirmButtonText = continueText,
            CancelButtonText = cancelText,
        });
        return (r.Confirmed, r.CheckboxChecked);
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

        var r = await _dialogService.ShowConfirmationAsync(new ConfirmationRequest
        {
            Message = message,
            CheckboxText = checkboxText,
            Title = title,
            ConfirmButtonText = unlockText,
            CancelButtonText = cancelText,
        });
        bool confirmed = r.Confirmed;
        bool dontShowAgain = r.CheckboxChecked;

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

    public void UpdateStatusBanner(object? value)
    {
        var banner = _statusBannerManager.ComputeBannerForValue(value, OptionWarnings, CrossGroupInfoMessage, ComboBoxOptions.Count, CompatibilityMessage);
        if (banner.HasValue) ApplyBanner(banner.Value);
        UpdateCustomStateBanner();
    }

    /// <summary>
    /// Surfaces the Windows-version compatibility message as a Warning banner. Called by the factory for
    /// non-Selection settings (Selection settings get it through UpdateStatusBanner's compat fallback).
    /// </summary>
    public void ShowCompatibilityBanner()
    {
        if (!string.IsNullOrEmpty(CompatibilityMessage))
            ApplyBanner(new SettingStatusBannerManager.BannerState(CompatibilityMessage, InfoBarSeverity.Warning));
    }

    private void ShowRestartBannerIfNeeded()
    {
        bool requiresRestart =
            Setting?.Apply.RequiresReboot ?? false;
        var banner = _statusBannerManager.GetRestartBanner(requiresRestart, _hasChangedThisSession);
        if (!banner.HasValue) return;

        // Do not overwrite an existing option-warning banner (Error severity) with the
        // generic restart-required message. The option warning is more important because
        // it tells the user *why* the change is potentially dangerous; the restart-required
        // hint is generic and duplicated across many settings. If an Error banner is already
        // present (e.g. from ComputeBannerForValue for a Warning-flagged ComboBox option),
        // keep it visible.
        if (StatusBannerSeverity == InfoBarSeverity.Error && !string.IsNullOrEmpty(StatusBannerMessage))
        {
            return;
        }

        ApplyBanner(banner.Value);
    }

    private void ApplyBanner(SettingStatusBannerManager.BannerState state)
    {
        StatusBannerMessage = state.Message;
        StatusBannerSeverity = state.Severity;
        // Fully qualified: this VM's own string `Icon` property shadows the FluentIcons.Common.Icon enum.
        StatusBannerIconSource = state.IsCustomState
            ? new FluentIcons.WinUI.FluentIconSource
            {
                Icon = FluentIcons.Common.Icon.QuestionCircle,
                IconVariant = FluentIcons.Common.IconVariant.Color,
            }
            : null;
    }

    /// <summary>Shows the Informational Custom-state banner while <see cref="IsCustomState"/> and no
    /// Warning/Error banner is active (compatibility, restart, option-warning and cross-group banners
    /// outrank it), and clears it - only it - once Custom clears. Called from the load/refresh paths,
    /// UpdateStatusBanner, and the Custom dialog flow.</summary>
    internal void UpdateCustomStateBanner()
    {
        bool isToggleLike = InputType == InputType.Toggle || InputType == InputType.CheckBox;
        var custom = _statusBannerManager.GetCustomStateBanner(isToggleLike);
        if (IsCustomState)
        {
            if (!string.IsNullOrEmpty(StatusBannerMessage) && StatusBannerSeverity != InfoBarSeverity.Informational)
                return;
            ApplyBanner(custom);
        }
        else if (!string.IsNullOrEmpty(StatusBannerMessage) && StatusBannerMessage == custom.Message)
        {
            ApplyBanner(SettingStatusBannerManager.BannerState.Clear);
        }
    }

    #endregion

    #region InfoBadge State Computation

    /// <summary>
    /// Computes the badge state by comparing the current UI state against
    /// recommended and default values from the Setting model's state roles and AC/DC accessors.
    /// </summary>
    public void ComputeBadgeState()
    {
        if (!HasBadgeData || Setting == null)
            return;

        bool matchesRecommended = true;
        bool matchesDefault = true;

        bool isToggleLike = InputType == InputType.Toggle || InputType == InputType.CheckBox;
        if (isToggleLike)
        {
            if (IsCustomState)
            {
                // A Custom toggle sits on no known state - it matches nothing (mirrors the selection
                // out-of-range verdict below).
                matchesRecommended = false;
                matchesDefault = false;
            }
            else
            {
                if (ToggleRecommendedState is bool r && r != IsSelected) matchesRecommended = false;
                if (ToggleDefaultState is bool d && d != IsSelected) matchesDefault = false;
            }
        }
        else if (InputType == InputType.Selection && !IsPowerCfgSetting && !IsPowerPlanSetting)
        {
            // Registry selection verdict: recommended/default come from the SELECTED state's Roles
            // (States order 1:1 with options).
            if (SelectedValue is int selIdx && selIdx >= 0 && selIdx < Setting.States.Count)
            {
                matchesRecommended = Setting.States.Any(st => st.HasRole(RoleKind.Recommended, _build))
                    && Setting.States[selIdx].HasRole(RoleKind.Recommended, _build);
                matchesDefault = Setting.States.Any(st => st.HasRole(RoleKind.WindowsDefault, _build))
                    && Setting.States[selIdx].HasRole(RoleKind.WindowsDefault, _build);
            }
            else { matchesRecommended = false; matchesDefault = false; }
        }

        // PowerCfg verdict - all powercfg settings are Separate mode. SupportsSeparateACDC encodes a
        // Separate PowerCfgTarget.
        if (SupportsSeparateACDC)
        {
            // On systems without a battery (desktops), DC values aren't writable by
            // PowerCfgApplier and aren't user-visible in the UI. Treat the setting as
            // AC-only for badge purposes - comparing DC against recommended/default
            // would otherwise produce spurious mismatches after a system-state refresh.
            bool considerDc = HasBattery;

            if (InputType == InputType.Selection)
            {
                // AC/DC selection - compare the live AC/DC option index against the recommended/
                // default index derived from the context-scoped state roles (state order == option
                // order, 1:1; the role tags the option whose PowerCfgValue matched the per-mode value).
                // On battery-less systems DC isn't writable by PowerCfgApplier and the DC control isn't
                // shown - skip DC comparisons or a system-state refresh would visibly flip the badge.
                if (AcSelectionRecommendedIndex is int rai && AcValue != rai)
                    matchesRecommended = false;
                if (considerDc && DcSelectionRecommendedIndex is int rdi && DcValue != rdi)
                    matchesRecommended = false;
                if (AcSelectionDefaultIndex is int dai && AcValue != dai)
                    matchesDefault = false;
                if (considerDc && DcSelectionDefaultIndex is int ddi && DcValue != ddi)
                    matchesDefault = false;
            }
            else if (InputType == InputType.NumericRange)
            {
                // AcNumericValue/DcNumericValue are in display units (e.g. Minutes). The accessors
                // return SYSTEM units, so the ConvertFromSystemUnits at the call site converts them
                // to the same units before comparing.
                if (AcRecommendedValue is int rac && AcNumericValue != ConvertFromSystemUnits(rac))
                    matchesRecommended = false;
                if (considerDc && DcRecommendedValue is int rdc && DcNumericValue != ConvertFromSystemUnits(rdc))
                    matchesRecommended = false;
                if (AcDefaultValue is int dac && AcNumericValue != ConvertFromSystemUnits(dac))
                    matchesDefault = false;
                if (considerDc && DcDefaultValue is int ddc && DcNumericValue != ConvertFromSystemUnits(ddc))
                    matchesDefault = false;
            }
        }

        var row = new List<BadgePillState>(capacity: 8);

        if (Setting.Display.IsSubjectivePreference)
        {
            var (label, tooltip) = ResolvePillStrings(SettingBadgeKind.Preference);
            row.Add(new BadgePillState(SettingBadgeKind.Preference, IsHighlighted: true, label, tooltip));
        }

        // For PowerCfg AC/DC Separate settings with a battery present, emit per-mode pills so
        // the user can see at a glance which mode matches recommended/default and which is
        // custom. On battery-less systems (desktops) DC is hidden and not writable, so we
        // keep the single-pill behaviour that the rest of the catalog uses.
        bool perModeBadges = SupportsSeparateACDC && HasBattery;

        if (perModeBadges)
        {
            AddAcDcRecommendedPills(row);
            AddAcDcDefaultPills(row);
            AddAcDcCustomPills(row);
        }
        else
        {
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

            // Selection: Custom when SelectedValue/AcValue/DcValue falls outside known options.
            // NumericRange: Custom when the current value matches neither Recommended nor Default.
            bool isCustom = InputType switch
            {
                InputType.Selection => !IsKnownSelectionValue(),
                InputType.Toggle or InputType.CheckBox => IsCustomState,
                InputType.NumericRange => (HasAnyRecommendedData() || HasAnyDefaultData())
                    && !matchesRecommended && !matchesDefault,
                _ => false
            };
            var (cLabel, cTooltip) = ResolvePillStrings(SettingBadgeKind.Custom);
            row.Add(new BadgePillState(SettingBadgeKind.Custom, IsHighlighted: isCustom, cLabel, cTooltip));
        }

        BadgeRow = row;
    }

    // Per-mode pills read via the AC/DC accessors (selection: AcSelectionRecommendedIndex
    // etc., off context-scoped state roles; numeric: AcRecommendedValue etc., reconstructed to system units).
    private void AddAcDcRecommendedPills(List<BadgePillState> row)
    {
        bool isNumeric = InputType == InputType.NumericRange;
        if ((isNumeric ? AcRecommendedValue.HasValue : AcSelectionRecommendedIndex.HasValue))
        {
            bool match = isNumeric
                ? AcNumericValue == ConvertFromSystemUnits(AcRecommendedValue!.Value)
                : AcValue == AcSelectionRecommendedIndex!.Value;
            var (label, tooltip) = ResolvePillStrings(SettingBadgeKind.Recommended, SettingBadgeMode.AC);
            row.Add(new BadgePillState(SettingBadgeKind.Recommended, match, label, tooltip, SettingBadgeMode.AC));
        }
        if ((isNumeric ? DcRecommendedValue.HasValue : DcSelectionRecommendedIndex.HasValue))
        {
            bool match = isNumeric
                ? DcNumericValue == ConvertFromSystemUnits(DcRecommendedValue!.Value)
                : DcValue == DcSelectionRecommendedIndex!.Value;
            var (label, tooltip) = ResolvePillStrings(SettingBadgeKind.Recommended, SettingBadgeMode.DC);
            row.Add(new BadgePillState(SettingBadgeKind.Recommended, match, label, tooltip, SettingBadgeMode.DC));
        }
    }

    private void AddAcDcDefaultPills(List<BadgePillState> row)
    {
        bool isNumeric = InputType == InputType.NumericRange;
        if ((isNumeric ? AcDefaultValue.HasValue : AcSelectionDefaultIndex.HasValue))
        {
            bool match = isNumeric
                ? AcNumericValue == ConvertFromSystemUnits(AcDefaultValue!.Value)
                : AcValue == AcSelectionDefaultIndex!.Value;
            var (label, tooltip) = ResolvePillStrings(SettingBadgeKind.Default, SettingBadgeMode.AC);
            row.Add(new BadgePillState(SettingBadgeKind.Default, match, label, tooltip, SettingBadgeMode.AC));
        }
        if ((isNumeric ? DcDefaultValue.HasValue : DcSelectionDefaultIndex.HasValue))
        {
            bool match = isNumeric
                ? DcNumericValue == ConvertFromSystemUnits(DcDefaultValue!.Value)
                : DcValue == DcSelectionDefaultIndex!.Value;
            var (label, tooltip) = ResolvePillStrings(SettingBadgeKind.Default, SettingBadgeMode.DC);
            row.Add(new BadgePillState(SettingBadgeKind.Default, match, label, tooltip, SettingBadgeMode.DC));
        }
    }

    private void AddAcDcCustomPills(List<BadgePillState> row)
    {
        // Custom (per-mode) lights when the current value matches neither Recommended nor Default on that
        // side, AND the setting has comparison data on that side. Selection also treats an out-of-range
        // index as Custom.
        bool isNumeric = InputType == InputType.NumericRange;
        bool acHasData = isNumeric
            ? (AcRecommendedValue.HasValue || AcDefaultValue.HasValue)
            : (AcSelectionRecommendedIndex.HasValue || AcSelectionDefaultIndex.HasValue);
        bool dcHasData = isNumeric
            ? (DcRecommendedValue.HasValue || DcDefaultValue.HasValue)
            : (DcSelectionRecommendedIndex.HasValue || DcSelectionDefaultIndex.HasValue);

        bool acCustom = false, dcCustom = false;

        if (InputType == InputType.Selection)
        {
            // State count == option count (1:1). In production Setting is non-null (the VM is built from the
            // catalog Setting), so this is a no-op there - but this line is NOT data-gated, so null-guard it
            // defensively rather than NRE (CS8602) on a null Setting: no Setting -> zero options -> no
            // out-of-range verdict (the AC/DC accessors below are already null-safe, so the pill logic stays sane).
            int optionCount = Setting?.States.Count ?? 0;
            bool hasOptions = optionCount > 0;
            if (acHasData)
            {
                bool acRec = AcSelectionRecommendedIndex is int rai && AcValue == rai;
                bool acDef = AcSelectionDefaultIndex is int dai && AcValue == dai;
                bool acOutOfRange = hasOptions && (AcValue < 0 || AcValue >= optionCount);
                acCustom = acOutOfRange || (!acRec && !acDef);
            }
            if (dcHasData)
            {
                bool dcRec = DcSelectionRecommendedIndex is int rdi && DcValue == rdi;
                bool dcDef = DcSelectionDefaultIndex is int ddi && DcValue == ddi;
                bool dcOutOfRange = hasOptions && (DcValue < 0 || DcValue >= optionCount);
                dcCustom = dcOutOfRange || (!dcRec && !dcDef);
            }
        }
        else if (InputType == InputType.NumericRange)
        {
            if (acHasData)
            {
                bool acRec = AcRecommendedValue is int rac && AcNumericValue == ConvertFromSystemUnits(rac);
                bool acDef = AcDefaultValue is int dac && AcNumericValue == ConvertFromSystemUnits(dac);
                acCustom = !acRec && !acDef;
            }
            if (dcHasData)
            {
                bool dcRec = DcRecommendedValue is int rdc && DcNumericValue == ConvertFromSystemUnits(rdc);
                bool dcDef = DcDefaultValue is int ddc && DcNumericValue == ConvertFromSystemUnits(ddc);
                dcCustom = !dcRec && !dcDef;
            }
        }

        if (acHasData)
        {
            var (label, tooltip) = ResolvePillStrings(SettingBadgeKind.Custom, SettingBadgeMode.AC);
            row.Add(new BadgePillState(SettingBadgeKind.Custom, acCustom, label, tooltip, SettingBadgeMode.AC));
        }
        if (dcHasData)
        {
            var (label, tooltip) = ResolvePillStrings(SettingBadgeKind.Custom, SettingBadgeMode.DC);
            row.Add(new BadgePillState(SettingBadgeKind.Custom, dcCustom, label, tooltip, SettingBadgeMode.DC));
        }
    }

    private bool HasAnyRecommendedData()
    {
        if (Setting == null) return false;
        bool isToggleLike = InputType == InputType.Toggle || InputType == InputType.CheckBox;
        if (isToggleLike) return ToggleRecommendedState.HasValue;
        if (InputType == InputType.Selection && !IsPowerCfgSetting && !IsPowerPlanSetting)
            return Setting.States.Any(st => st.HasRole(RoleKind.Recommended, _build));
        return AcRecommendedValue.HasValue || AcSelectionRecommendedIndex.HasValue
            || DcRecommendedValue.HasValue || DcSelectionRecommendedIndex.HasValue;
    }

    private bool HasAnyDefaultData()
    {
        if (Setting == null) return false;
        bool isToggleLike = InputType == InputType.Toggle || InputType == InputType.CheckBox;
        if (isToggleLike) return ToggleDefaultState.HasValue;
        if (InputType == InputType.Selection && !IsPowerCfgSetting && !IsPowerPlanSetting)
            return Setting.States.Any(st => st.HasRole(RoleKind.WindowsDefault, _build));
        return AcDefaultValue.HasValue || AcSelectionDefaultIndex.HasValue
            || DcDefaultValue.HasValue || DcSelectionDefaultIndex.HasValue;
    }

    private bool IsKnownSelectionValue()
    {
        if (InputType != InputType.Selection) return true;
        if (Setting == null) return true;
        if (!IsPowerCfgSetting && !IsPowerPlanSetting)
            return SelectedValue is int idxK && idxK >= 0 && idxK < Setting.States.Count;
        int optionCount = Setting.States.Count;
        if (optionCount == 0) return true;
        if (SupportsSeparateACDC)
            return AcValue >= 0 && AcValue < optionCount
                && DcValue >= 0 && DcValue < optionCount;
        return SelectedValue is int idx && idx >= 0 && idx < optionCount;
    }

    private (string Label, string Tooltip) ResolvePillStrings(SettingBadgeKind kind, SettingBadgeMode mode = SettingBadgeMode.None)
    {
        var (baseLabel, tooltip) = kind switch
        {
            SettingBadgeKind.Recommended => (
                _localizationService?.GetString("InfoBadge_Recommended") ?? "Recommended",
                _localizationService?.GetString("InfoBadge_Recommended_Tooltip") ?? "Winhance's recommended value"),
            SettingBadgeKind.Default => (
                _localizationService?.GetString("InfoBadge_Default") ?? "Default",
                _localizationService?.GetString("InfoBadge_Default_Tooltip") ?? "Windows factory value"),
            SettingBadgeKind.Custom => (
                _localizationService?.GetString("InfoBadge_Custom") ?? "Custom",
                _localizationService?.GetString("InfoBadge_Custom_Tooltip") ?? "Custom value (not a known option)"),
            SettingBadgeKind.Preference => (
                _localizationService?.GetString("InfoBadge_Preference") ?? "Preference",
                _localizationService?.GetString("InfoBadge_Preference_Tooltip") ?? "Personal preference"),
            _ => ("", ""),
        };

        // AC/DC are technical terms ("AC" = mains power, "DC" = battery) that are not
        // translated in Windows' Power Options either — leave them as-is across all locales.
        var label = mode switch
        {
            SettingBadgeMode.AC => $"{baseLabel} (AC)",
            SettingBadgeMode.DC => $"{baseLabel} (DC)",
            _ => baseLabel,
        };
        return (label, tooltip);
    }

    /// <summary>
    /// Initializes HasBadgeData from the Setting model: whether the setting has comparable
    /// recommended/default data (toggle/task state roles, selection state roles, or per-context powercfg values).
    /// </summary>
    private void InitializeHasBadgeData()
    {
        if (Setting == null)
        {
            HasBadgeData = false;
            return;
        }

        if (InputType == InputType.Action)
        {
            HasBadgeData = false;
            return;
        }

        bool isToggleLike = InputType == InputType.Toggle || InputType == InputType.CheckBox;
        bool hasToggleData = isToggleLike && (ToggleRecommendedState.HasValue || ToggleDefaultState.HasValue);
        bool hasSelectionData = InputType == InputType.Selection && !IsPowerCfgSetting && !IsPowerPlanSetting
            && Setting.States.Any(st => st.HasRole(RoleKind.Recommended, _build) || st.HasRole(RoleKind.WindowsDefault, _build));
        bool hasPowerCfgData = AcRecommendedValue.HasValue || AcDefaultValue.HasValue
            || AcSelectionRecommendedIndex.HasValue || AcSelectionDefaultIndex.HasValue
            || DcRecommendedValue.HasValue || DcDefaultValue.HasValue
            || DcSelectionRecommendedIndex.HasValue || DcSelectionDefaultIndex.HasValue;
        HasBadgeData = hasToggleData || hasSelectionData || hasPowerCfgData;
    }

    #endregion

    #region Technical Details

    public void ToggleTechnicalDetails() => IsTechnicalDetailsExpanded = !IsTechnicalDetailsExpanded;

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(NewBadgeText));
        OnPropertyChanged(nameof(TechnicalDetailsLabel));
        OnPropertyChanged(nameof(OpenRegeditTooltip));
        OnPropertyChanged(nameof(ClickToUnlockText));
        OnPropertyChanged(nameof(PluggedInText));
        OnPropertyChanged(nameof(OnBatteryText));
        OnPropertyChanged(nameof(AcInputAutomationName));
        OnPropertyChanged(nameof(DcInputAutomationName));
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

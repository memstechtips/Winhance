using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Windows.Globalization.NumberFormatting;

using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Localization;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
using Winhance.Core.Features.Common.TechnicalDetails;
using Winhance.UI.Features.Common.Controls;
using Winhance.UI.Features.Common.Converters;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Common.Utilities;
using Winhance.UI.Features.Common.ViewModels;

namespace Winhance.UI.Features.Optimize.ViewModels;

public partial class SettingItemViewModel : BaseViewModel, ISettingWriteProgress
{
    private readonly ISettingWriteStrategySelector _writeStrategySelector;
    private readonly ILogService _logService;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localizationService;
    private readonly IUserPreferencesService? _userPreferencesService;
    private readonly INewBadgeService? _newBadgeService;
    private readonly IApplicationModeService? _applicationModeService;
    private readonly IFilePickerService? _filePickerService;
    private readonly SettingStatusBannerManager _statusBannerManager;
    private readonly TechnicalDetailsManager _technicalDetailsManager;
    // Live Windows build (from config), for build-aware default/badge resolution of merged Selections
    // (e.g. theme-mode-windows). Set once in the ctor before the initial badge computation.
    private readonly WinBuild _build;
    private volatile bool _isUpdatingFromEvent;
    private bool _hasChangedThisSession;
    private object? _pendingValue;
    // An upper-casing rule turns several typed strings into one normalized value, which must not be recorded twice.
    private string? _lastRecordedText;

    public ISettingsFeatureViewModel? ParentFeatureViewModel { get; set; }

    public Setting? Setting { get; set; }

    public IReadOnlyList<OptionWarning?>? OptionWarnings { get; }

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

    // Drives the overlay, its icon and label, the banner, and whether clicking does anything at all: an
    // Undetermined setting is rendered but inert, because applying over a value we failed to read would write blind.
    [ObservableProperty]
    public partial SettingDetectionOutcome Outcome { get; set; }

    [ObservableProperty]
    public partial bool IsApplying { get; set; }

    [ObservableProperty]
    public partial string Status { get; set; }

    [ObservableProperty]
    public partial string? StatusBannerMessage { get; set; }

    [ObservableProperty]
    public partial InfoBarSeverity StatusBannerSeverity { get; set; }

    // Null tells InfoBar to use the severity's native icon, which Warning/Error banners must keep. Set ONLY by
    // ApplyBanner (the single banner funnel).
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

    [ObservableProperty]
    public partial string TextValue { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasTextError { get; set; }

    public string TextError => Setting?.TextBox?.Rule.Message is { } message
        ? _localizationService.GetStringOrDefault(message.Value, message.Value)
        : string.Empty;

    public string TextPlaceholder => Setting?.TextBox?.Placeholder is { } placeholder
        ? _localizationService.GetStringOrDefault(placeholder.Value, placeholder.Value)
        : string.Empty;

    public PickerKind TextBoxPicker => Setting?.TextBox?.Picker ?? PickerKind.None;

    public bool HasTextBoxPicker => TextBoxPicker != PickerKind.None;

    // en.json carries no generic Browse caption, so every card borrows the two the background children already
    // ship in all 29 languages.
    public string BrowseLabel => Setting?.Display.Tiles == OptionTiles.Colors
        ? _localizationService.GetStringOrDefault(LocKey.Setting.ThemeWallpaperColor.Browse.Value, "View colors")
        : _localizationService.GetStringOrDefault(LocKey.Setting.ThemeWallpaperPicture.Browse.Value, "Browse");

    // Windows' own wording: "Recent images" heads the picture tiles, "Custom colors" the row under the swatches.
    public string ChildHeader => Setting?.Display.Tiles == OptionTiles.Pictures
        ? _localizationService.GetStringOrDefault(LocKey.Setting.ThemeWallpaperPicture.Recent.Value, "Recent images")
        : Name;

    public string BrowseRowHeader => Setting?.Display.Tiles == OptionTiles.Colors
        ? _localizationService.GetStringOrDefault(LocKey.Setting.ThemeWallpaperColor.Custom.Value, "Custom colors")
        : Name;

    public ObservableCollection<ListRowViewModel> Rows { get; } = [];

    [ObservableProperty]
    public partial string ListAddLabel { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ListRemoveLabel { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SavePasswordsLabel { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SavePasswordsNote { get; set; } = string.Empty;

    // The flag lives on the List record, so a tick rewrites the whole record the way a row edit does.
    [ObservableProperty]
    public partial bool SavePasswords { get; set; }

    partial void OnSavePasswordsChanged(bool value) => HandleListAsync().FireAndForget(_logService);

    public bool HasRows => Rows.Count > 0;

    public string OnText { get; set; } = "On";
    public string OffText { get; set; } = "Off";
    public string ActionButtonText { get; set; } = "Apply";

    [ObservableProperty]
    public partial bool IsTechnicalDetailsExpanded { get; set; }

    [ObservableProperty]
    public partial bool IsTechnicalDetailsGloballyVisible { get; set; }

    [ObservableProperty]
    public partial OptionMatrix? TechnicalDetailMatrix { get; set; }

    public bool HasTechnicalDetails => TechnicalDetailMatrix is not null;

    public IRelayCommand<string> OpenRegeditCommand => _technicalDetailsManager.OpenRegeditCommand;

    public bool ShowTechnicalDetailsBar => HasTechnicalDetails && IsTechnicalDetailsGloballyVisible;

    // Bottom corners rounded only while collapsed; when expanded the content panel below carries them.
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
        _localizationService.GetStringOrDefault("View_TechnicalDetails", "Technical Details");

    [RelayCommand]
    public void ToggleTechnicalDetails() => IsTechnicalDetailsExpanded = !IsTechnicalDetailsExpanded;

    [ObservableProperty]
    public partial bool IsVisible { get; set; }

    [ObservableProperty]
    public partial bool IsEnabled { get; set; }

    // Names this setting's cross-group children. The child list is filtered by the catalog scope, so a
    // scope change rebuilds it (SettingsLoadingService.RefreshScopeDerivedStateAsync).
    public string? CrossGroupInfoMessage { get; set; }

    // Windows-version compatibility warning text (set by the loading bridge when the
    // version filter is off). Surfaced as a Warning banner.
    public string? CompatibilityMessage { get; set; }

    [ObservableProperty]
    public partial bool IsNew { get; set; }

    [ObservableProperty]
    public partial bool IsNewBadgeGloballyVisible { get; set; } = true;

    public string NewBadgeText => _localizationService.GetStringOrDefault("Badge_New", "NEW");

    public bool ShowNewBadge => IsNew && IsNewBadgeGloballyVisible;

    partial void OnIsNewChanged(bool value) => OnPropertyChanged(nameof(ShowNewBadge));
    partial void OnIsNewBadgeGloballyVisibleChanged(bool value) => OnPropertyChanged(nameof(ShowNewBadge));

    [ObservableProperty]
    public partial bool IsInfoBadgeGloballyVisible { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<BadgePillState> BadgeRow { get; set; } = Array.Empty<BadgePillState>();

    public bool HasBadgeData { get; set; }

    public bool ShowInfoBadge => IsInfoBadgeGloballyVisible && HasBadgeData;

    partial void OnIsInfoBadgeGloballyVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowInfoBadge));
        OnPropertyChanged(nameof(ShowNumericQuickSetButtons));
        OnPropertyChanged(nameof(ShowTwoStateQuickSetButtons));
        OnPropertyChanged(nameof(ShowSelectionQuickSetButtons));
        OnPropertyChanged(nameof(ShowAcSelectionQuickSetButtons));
        OnPropertyChanged(nameof(ShowDcSelectionQuickSetButtons));
    }

    // Quick-set buttons: every setting card shows "Set to Recommended" / "Set to Default" buttons in front
    // of its control when the ShowInfoBadges preference is on AND the setting has at
    // least one of Recommended/Default defined.
    //
    // Tooltips use the localized "Set to Recommended ({0})" / "Set to Default ({0})"
    // template — {0} is the target value's display form (number, On/Off text, or
    // combobox option label). The string uses a literal "{0}" token (not .NET composite
    // format), so we use string.Replace at runtime.

    // Real numerics are all powercfg-separate AC/DC, so there is no Always-context value and this returns null.
    public int? NumericRecommendedValue =>
        Setting?.Numeric?.Recommended.FirstOrDefault(cv => cv.Context == PowerContext.Always) is { } cv
            ? ConvertToSystemUnits(cv.Value) : null;

    public int? NumericDefaultValue =>
        Setting?.Numeric?.WindowsDefault.FirstOrDefault(cv => cv.Context == PowerContext.Always) is { } cv
            ? ConvertToSystemUnits(cv.Value) : null;

    // SYSTEM units, reconstructed from the per-context Numeric target (display -> system) so the call sites'
    // ConvertFromSystemUnits re-derives the same display value.
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

    // Accessibility names (issue #647 follow-up).
    // The quick-set buttons inside SettingsCardItem inherit no context from their
    // parent SettingsCard, so Narrator was announcing only the action ("Set to
    // Recommended button") without saying which setting it applied to. These helpers
    // compose "<Setting name>: <action>" (and "<Setting name> (Plugged In|On Battery):
    // <action>" for Dual AC/DC variants) for AutomationProperties.Name. Visible
    // ToolTipService.ToolTip strings stay short (action only).
    //
    // x:Bind function syntax re-evaluates when the argument's PropertyChanged fires (language change).

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

    public bool? TwoStateRecommended =>
        Setting is { } s ? TwoState.GetRecommended(s, _build) : null;

    public bool? TwoStateDefault =>
        Setting is { } s ? TwoState.GetDefault(s, _build) : null;

    private string TwoStateText(bool state) => state ? OnText : OffText;

    public string TwoStateRecommendedTooltip =>
        TwoStateRecommended is bool s
            ? FormatValueTooltip("InfoBadge_Numeric_SetToRecommended_Tooltip", TwoStateText(s))
            : string.Empty;

    public string TwoStateDefaultTooltip =>
        TwoStateDefault is bool s
            ? FormatValueTooltip("InfoBadge_Numeric_SetToDefault_Tooltip", TwoStateText(s))
            : string.Empty;

    public bool ShowTwoStateQuickSetButtons
    {
        get
        {
            if (!IsInfoBadgeGloballyVisible) return false;
            if (InputType != InputType.Toggle && InputType != InputType.CheckBox) return false;
            return TwoStateRecommended.HasValue || TwoStateDefault.HasValue;
        }
    }

    public IRelayCommand SetTwoStateToRecommendedCommand => _setTwoStateToRecommendedCommand ??=
        new RelayCommand(() =>
        {
            if (TwoStateRecommended is bool v)
                // fromCustomState while Custom: bypass the newValue==IsSelected guard (a Custom toggle
                // sits at IsSelected=false, so a Disabled target would be silently swallowed - no write,
                // no feedback) and clear the overlay on success. Quick-set is an explicit state pick,
                // so no dialog (same reasoning as the Custom dialog flow's no-double-confirm).
                HandleTwoStateAsync(v, fromCustomState: ShowsStateOverlay).FireAndForget(_logService);
        });
    private RelayCommand? _setTwoStateToRecommendedCommand;

    public IRelayCommand SetTwoStateToDefaultCommand => _setTwoStateToDefaultCommand ??=
        new RelayCommand(() =>
        {
            if (TwoStateDefault is bool v)
                HandleTwoStateAsync(v, resetToDefault: true, fromCustomState: ShowsStateOverlay).FireAndForget(_logService);
        });
    private RelayCommand? _setTwoStateToDefaultCommand;

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

    public bool HasRecommendedQuickSetTarget => HasQuickSetTarget(recommended: true);

    public bool HasDefaultQuickSetTarget => HasQuickSetTarget(recommended: false);

    private bool HasQuickSetTarget(bool recommended) => InputType switch
    {
        InputType.Toggle or InputType.CheckBox =>
            (recommended ? TwoStateRecommended : TwoStateDefault).HasValue,
        InputType.Selection when IsPowerCfgSetting =>
            (recommended ? AcSelectionRecommendedIndex : AcSelectionDefaultIndex).HasValue
            || (SupportsSeparateACDC && (recommended ? DcSelectionRecommendedIndex : DcSelectionDefaultIndex).HasValue),
        InputType.Selection =>
            (recommended ? SelectionRecommendedIndex : SelectionDefaultIndex).HasValue,
        InputType.NumericRange when SupportsSeparateACDC =>
            (recommended ? AcRecommendedValue : AcDefaultValue).HasValue
            || (recommended ? DcRecommendedValue : DcDefaultValue).HasValue,
        InputType.NumericRange =>
            (recommended ? NumericRecommendedValue : NumericDefaultValue).HasValue,
        _ => false
    };

    // Every path runs through the guarded apply pipeline, so in Builder mode this records an edit and never touches the system.
    public bool TrySetToRecommended() => TryExecuteQuickSet(recommended: true);

    public bool TrySetToDefault() => TryExecuteQuickSet(recommended: false);

    private bool TryExecuteQuickSet(bool recommended)
    {
        if (!HasQuickSetTarget(recommended)) return false;

        switch (InputType)
        {
            case InputType.Toggle:
            case InputType.CheckBox:
                (recommended ? SetTwoStateToRecommendedCommand : SetTwoStateToDefaultCommand).Execute(null);
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

    [ObservableProperty]
    public partial bool IsLocked { get; set; }

    public bool RequiresAdvancedUnlock =>
        Setting?.Availability.RequiresAdvancedUnlock ?? false;
    public string ClickToUnlockText => _localizationService.GetStringOrDefault("Common_ClickToUnlock", "Click to unlock");
    public IAsyncRelayCommand UnlockCommand { get; }

    // Review-mode state.
    // All of it lives behind one nullable reference, so leaving review is a single assignment to
    // null and nothing has to be reset field by field. That is the point of the shape: the previous
    // one was nine separate observable properties cleared by a nine-line ClearReviewState(), which
    // put every future review field one forgotten line away from surviving into the next mode — a
    // leak with no symptom where it was introduced. A field added to SettingReviewState cannot
    // outlive the review, because nothing clears fields at all.
    private SettingReviewState? _reviewState;

    // Setting it true opens a fresh review overlay; setting it false drops the overlay and everything in it.
    public bool IsInReviewMode
    {
        get => _reviewState is not null;
        set
        {
            if (value == IsInReviewMode) return;

            _reviewState = value ? new SettingReviewState() : null;

            OnPropertyChanged();
            NotifyReviewProjectionsChanged();
            OnPropertyChanged(nameof(EffectiveIsEnabled));

            // Entering review forces every expander open so children carrying review diffs are
            // visible. A parent collapsed before import would otherwise hide its children behind a
            // disabled card and leave Apply Config gated. The chevron still collapses a subtree after.
            if (value)
                IsExpanderExpanded = true;
        }
    }

    public bool HasReviewDiff
    {
        get => _reviewState?.HasDiff ?? false;
        set => SetReviewValue(static s => s.HasDiff, static (s, v) => s.HasDiff = v, value);
    }

    public string? ReviewDiffMessage
    {
        get => _reviewState?.DiffMessage;
        set => SetReviewValue(static s => s.DiffMessage, static (s, v) => s.DiffMessage = v, value);
    }

    public bool IsReviewApproved
    {
        get => _reviewState?.IsApproved ?? false;
        set
        {
            if (!SetReviewValue(static s => s.IsApproved, static (s, v) => s.IsApproved = v, value))
                return;

            if (value && IsReviewRejected)
                IsReviewRejected = false;

            OnPropertyChanged(nameof(IsReviewDecisionMade));
            ReviewApprovalChanged?.Invoke(this, value);
        }
    }

    public bool IsReviewRejected
    {
        get => _reviewState?.IsRejected ?? false;
        set
        {
            if (!SetReviewValue(static s => s.IsRejected, static (s, v) => s.IsRejected = v, value))
                return;

            if (value && IsReviewApproved)
                IsReviewApproved = false;

            OnPropertyChanged(nameof(IsReviewDecisionMade));
            if (value)
                ReviewApprovalChanged?.Invoke(this, false);
        }
    }

    public bool IsReviewDecisionMade => IsReviewApproved || IsReviewRejected;

    // Review action properties (for action settings like wallpaper that appear alongside a diff)
    public bool HasReviewAction
    {
        get => _reviewState?.HasAction ?? false;
        set => SetReviewValue(static s => s.HasAction, static (s, v) => s.HasAction = v, value);
    }

    public string? ReviewActionMessage
    {
        get => _reviewState?.ActionMessage;
        set => SetReviewValue(static s => s.ActionMessage, static (s, v) => s.ActionMessage = v, value);
    }

    public bool IsReviewActionApproved
    {
        get => _reviewState?.IsActionApproved ?? false;
        set
        {
            if (!SetReviewValue(static s => s.IsActionApproved, static (s, v) => s.IsActionApproved = v, value))
                return;

            if (value && IsReviewActionRejected)
                IsReviewActionRejected = false;

            ReviewActionApprovalChanged?.Invoke(this, value);
        }
    }

    public bool IsReviewActionRejected
    {
        get => _reviewState?.IsActionRejected ?? false;
        set
        {
            if (!SetReviewValue(static s => s.IsActionRejected, static (s, v) => s.IsActionRejected = v, value))
                return;

            if (value && IsReviewActionApproved)
                IsReviewActionApproved = false;

            if (value)
                ReviewActionApprovalChanged?.Invoke(this, false);
        }
    }

    public string ReviewActionGroupName => $"{SettingId}_action";

    public event EventHandler<bool>? ReviewActionApprovalChanged;

    public event EventHandler<bool>? ReviewApprovalChanged;

    // A write while no overlay exists is dropped: review values belong to the review, and letting one land outside
    // it is the contamination this shape prevents - the diff applier sets IsInReviewMode before anything else for that reason.
    private bool SetReviewValue<T>(
        Func<SettingReviewState, T> read,
        Action<SettingReviewState, T> write,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (_reviewState is not { } state) return false;
        if (EqualityComparer<T>.Default.Equals(read(state), value)) return false;

        write(state, value);
        OnPropertyChanged(propertyName);
        return true;
    }

    // Needed when the overlay itself is created or dropped: the individual values did not change, the object they
    // read from did. A missing line here leaves a card looking stale (visible, harmless), where a missing reset
    // leaked state into the next mode invisibly. SettingItemViewModelReviewStateTests derives the expected set by reflection.
    private void NotifyReviewProjectionsChanged()
    {
        OnPropertyChanged(nameof(HasReviewDiff));
        OnPropertyChanged(nameof(ReviewDiffMessage));
        OnPropertyChanged(nameof(IsReviewApproved));
        OnPropertyChanged(nameof(IsReviewRejected));
        OnPropertyChanged(nameof(IsReviewDecisionMade));
        OnPropertyChanged(nameof(HasReviewAction));
        OnPropertyChanged(nameof(ReviewActionMessage));
        OnPropertyChanged(nameof(IsReviewActionApproved));
        OnPropertyChanged(nameof(IsReviewActionRejected));
    }

    // Handlers go first: dropping the overlay raises PropertyChanged for the projections, and a stale subscriber
    // must not act on those.
    public void ClearReviewState()
    {
        ReviewApprovalChanged = null;
        ReviewActionApprovalChanged = null;

        IsInReviewMode = false;
    }

    partial void OnIsEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(EffectiveIsEnabled));
    }

    // False only when Setting.EnabledWhen names a setting currently outside the declared states; true for every
    // card that declares no gate - including every card merely NESTED under another. One bool because that is all
    // the view binds to; the decision is made in BaseSettingsFeatureViewModel, the only place that can see the other card.
    [ObservableProperty]
    public partial bool ParentIsEnabled { get; set; }

    partial void OnParentIsEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(EffectiveIsEnabled));
    }

    // Kept apart from IsVisible because the search path stamps that one true on every card on every keystroke,
    // which would erase a declared gate.
    [ObservableProperty]
    public partial bool ParentIsVisible { get; set; }

    partial void OnParentIsVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(EffectiveIsVisible));
    }

    partial void OnIsVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(EffectiveIsVisible));
    }

    public bool EffectiveIsVisible => IsVisible && ParentIsVisible;

    // The state LABEL this card sits on, or null when there is none to name (no catalog Setting, no States, or a
    // Selection on the -1 Custom sentinel). A declared gate compares against this - by LABEL, never index. A toggle
    // answers from IsSelected (its SelectedValue is only written at load and would go stale); a selection from
    // SelectedValue, whose int IS the state index, so a detect-only state still names itself. Deliberately NOT
    // bindable: its only reader pulls it imperatively straight after writing the state it derives from.
    public string? CurrentStateLabel
    {
        get
        {
            if (Setting is not { States.Count: > 0 } catalogSetting)
                return null;

            return InputType switch
            {
                InputType.Toggle or InputType.CheckBox =>
                    catalogSetting.States.FirstOrDefault(st => st.Label == TwoState.Label(catalogSetting.Control, IsSelected))?.Label.Value,
                InputType.Selection when SelectedValue is int index
                    && index >= 0 && index < catalogSetting.States.Count =>
                    catalogSetting.States[index].Label.Value,
                _ => null,
            };
        }
    }

    public bool EffectiveIsEnabled => IsEnabled && ParentIsEnabled && !IsInReviewMode && IsIncluded;

    // Builder mode records desired state into the UI without applying to the system. Named for the
    // capability rather than the mode so the write path asks what it is allowed to do, not who it is.
    private bool AuthorsIntent => _applicationModeService.Capabilities().AuthorsIntent;

    private bool SettingRequiresConfirmation => Setting?.Apply.RequiresConfirmation ?? false;

    // While a mode is authoring, the authored value - not what the machine says - is what the card must show,
    // because it is what Save will write. Call from every path that writes live state into this ViewModel (a
    // filter toggle or language change rebuilds every card from the machine). No-op outside an authoring mode or
    // for a setting never authored.
    public void ApplyAuthoredOverlay()
    {
        if (!AuthorsIntent)
        {
            // A card can outlive the session that excluded it.
            SetIncludedFromSession(true);
            return;
        }

        // Before the edit lookup, which returns early for a setting that was excluded without ever being edited.
        SetIncludedFromSession(_applicationModeService?.IsIncluded(SettingId) ?? true);

        if (_applicationModeService?.GetBuilderEdit(SettingId) is not { } edit) return;

        // These are programmatic writes standing in for the user's earlier ones - they must not
        // re-enter the input handlers and record themselves a second time. Saved and restored
        // rather than cleared, because the live-state writers call this from inside their own
        // suppressed section.
        bool wasUpdatingFromEvent = _isUpdatingFromEvent;
        _isUpdatingFromEvent = true;
        try
        {
            ApplyAuthoredValues(edit);
        }
        finally
        {
            _isUpdatingFromEvent = wasUpdatingFromEvent;
        }
    }

    private void ApplyAuthoredValues(SettingChoice edit)
    {
        switch (edit.Value)
        {
            case ChoiceValue.Toggle t:
                // Outcome deliberately untouched: the two-state path only resolves an outcome for a
                // Custom-state pick, and that is not part of the record to restore from.
                IsSelected = t.On;
                break;

            case ChoiceValue.CheckBox c:
                IsSelected = c.Checked;
                break;

            case ChoiceValue.Text t:
                TextValue = t.Value;
                _lastRecordedText = t.Value;
                HasTextError = false;
                break;

            case ChoiceValue.List l:
                SavePasswords = l.SavePasswords;
                RebuildRows(l);
                break;

            case ChoiceValue.Keyed k:
                // The provider lists only three Windows pictures and a few recents, so a restored picture is usually
                // missing from it; the tile is captioned by file name rather than as "not available".
                EnsureKeyedOption(k.Key, ShowsOptionTiles ? Path.GetFileName(k.Key) : null);
                SelectedValue = k.Key;
                Outcome = SettingDetectionOutcome.Resolved;
                UpdateStatusBanner(k.Key);
                break;

            case ChoiceValue.AcDcOption a:
                AcValue = a.AcIndex;
                DcValue = a.DcIndex;
                Outcome = SettingDetectionOutcome.Resolved;
                break;

            case ChoiceValue.CustomValues c:
                // Authored at the Custom index: the raw values are the payload, and the card
                // keeps its Custom rendering - the write path does not resolve this case either.
                CapturedCustomStateValues = new Dictionary<string, object>(c.Values);
                SelectedValue = ComboBoxConstants.CustomStateIndex;
                NumericValue = ComboBoxConstants.CustomStateIndex;
                break;

            case ChoiceValue.Option o:
                SelectedValue = o.Index;
                NumericValue = o.Index;
                Outcome = SettingDetectionOutcome.Resolved;
                UpdateStatusBanner(o.Index);
                break;

            case ChoiceValue.AcDcNumber n:
                // Converted on the way out because the record holds SYSTEM units and the sliders
                // read DISPLAY units - the same asymmetry the recording side converts across.
                AcNumericValue = ConvertFromSystemUnits(n.Ac);
                DcNumericValue = ConvertFromSystemUnits(n.Dc);
                break;

            case ChoiceValue.Number n:
                int display = ConvertFromSystemUnits(n.Value);
                NumericValue = display;
                SelectedValue = display;
                Outcome = SettingDetectionOutcome.Resolved;
                UpdateStatusBanner(display);
                break;
        }
    }

    // Resolved per write, not held in a field: the mode changes while this ViewModel stays alive.
    private Task<SettingWriteResult> WriteAsync(SettingWriteRequest request) =>
        _writeStrategySelector.ForCurrentMode().WriteAsync(
            request with { SystemRequest = request.SystemRequest with { BeforeState = _lastKnownState } },
            this);

    // What this card last showed. The apply service builds the change-history receipt's "before" half from it
    // rather than re-reading the setting: the read cost nothing for a registry setting but collided with the
    // powercfg commit of the apply before it, which is what made a single-setting refresh take 500ms.
    private SettingStateResult? _lastKnownState;

    public void RecordKnownState(SettingStateResult state)
    {
        if (state.Success)
            _lastKnownState = state;
    }

    // Every input handler funnels through here on a write that stuck: the discard prompt has to fire for authored
    // work even when it produced no serializable ChoiceValue, and a new input type gets it for free.
    private void MarkChangedThisSession()
    {
        _hasChangedThisSession = true;
        if (AuthorsIntent)
        {
            _applicationModeService?.MarkBuilderDirty();
        }
    }

    // Captured at seed time so Builder-mode serialization can emit the custom value without re-reading the system.
    public Dictionary<string, object>? CapturedCustomStateValues { get; set; }
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
        IsLastChild && !ShowsOptionTiles ? new Microsoft.UI.Xaml.CornerRadius(0, 0, 4, 4) : new Microsoft.UI.Xaml.CornerRadius(0);

    public Microsoft.UI.Xaml.CornerRadius BrowseRowCornerRadius =>
        IsLastChild ? new Microsoft.UI.Xaml.CornerRadius(0, 0, 4, 4) : new Microsoft.UI.Xaml.CornerRadius(0);

    partial void OnIsLastChildChanged(bool value)
    {
        OnPropertyChanged(nameof(ChildCornerRadius));
        OnPropertyChanged(nameof(BrowseRowCornerRadius));
    }

    [ObservableProperty]
    public partial bool HasVisibleChildren { get; set; } = true;

    public Microsoft.UI.Xaml.CornerRadius ParentCornerRadius => HasVisibleChildren
        ? new Microsoft.UI.Xaml.CornerRadius(4, 4, 0, 0)
        : new Microsoft.UI.Xaml.CornerRadius(4);

    partial void OnHasVisibleChildrenChanged(bool value) => OnPropertyChanged(nameof(ParentCornerRadius));

    public void ToggleExpander() => IsExpanderExpanded = !IsExpanderExpanded;

    [ObservableProperty]
    public partial bool IsIncluded { get; set; } = true;

    private bool _isSyncingInclude;

    partial void OnIsIncludedChanged(bool value)
    {
        OnPropertyChanged(nameof(EffectiveIsEnabled));
        OnPropertyChanged(nameof(IncludeState));
        OnPropertyChanged(nameof(IncludeToolTip));

        if (_isSyncingInclude) return;

        _applicationModeService?.SetIncluded(SettingId, value);
    }

    // An answer file cannot leave a setting out: an absent choice renders the file's default.
    public bool ShowIncludeCheckbox => AuthorsIntent && Setting?.IsAnswerFileOnly != true;

    // The answer file carries the passwords whatever the box says; the tick rides on the List record to a
    // later config save.
    public bool ShowPasswordBanner =>
        Setting?.List?.Fields.Any(f => f.Kind == FieldKind.Password) == true && AuthorsIntent;

    public string IncludeToolTip => IsIncluded
        ? _localizationService.GetStringOrDefault("Setting_Include_Tooltip_In", "Included in the file")
        : _localizationService.GetStringOrDefault("Setting_Include_Tooltip_Out", "Left out of the file. Tick it to put it back.");

    // The include box template's width: a 20px checkbox, 8px, a 16px glyph, then 12px to the header icon.
    private const double IncludeBoxGutter = 56;

    public Microsoft.UI.Xaml.Thickness CardPadding => ShowIncludeCheckbox
        ? new Microsoft.UI.Xaml.Thickness(16 + IncludeBoxGutter, 12, 16, 12)
        : new Microsoft.UI.Xaml.Thickness(16, 12, 16, 12);

    public Microsoft.UI.Xaml.Thickness ChildCardPadding => IsCompactRow
        ? RowPadding
        : ShowIncludeCheckbox
            ? new Microsoft.UI.Xaml.Thickness(54 + IncludeBoxGutter, 12, 52, 12)
            : new Microsoft.UI.Xaml.Thickness(54, 12, 52, 12);

    // Starts where the header text does: past the card padding, the 24px icon column and its 16px margin.
    public Microsoft.UI.Xaml.Thickness RowPadding => new(CardPadding.Left + 40, 8, 52, 8);

    public bool IsCompactRow =>
        EffectiveUiParentId is { } parentId
        && SettingCatalog.Find(parentId)?.Display.CompactChildren == true;

    // 52 is the toolkit's expander-item height, 68 its card minimum, from a Style setter no per-card resource can reach.
    public double ChildMinHeight => IsCompactRow ? 52 : 68;

    public Microsoft.UI.Xaml.Controls.IconElement? ChildHeaderIcon =>
        IsCompactRow ? null : IconConverter.Build(this, null);

    public CommunityToolkit.WinUI.Controls.ContentAlignment ChildContentAlignment =>
        Setting?.Display.Tiles is { } tiles && tiles != OptionTiles.None
            ? CommunityToolkit.WinUI.Controls.ContentAlignment.Vertical
            : CommunityToolkit.WinUI.Controls.ContentAlignment.Right;

    // Right is the toolkit default; a vertical card must stretch or its tiles wrap inside their own measured width.
    public Microsoft.UI.Xaml.HorizontalAlignment ChildContentHorizontalAlignment =>
        ChildContentAlignment == CommunityToolkit.WinUI.Controls.ContentAlignment.Vertical
            ? Microsoft.UI.Xaml.HorizontalAlignment.Stretch
            : Microsoft.UI.Xaml.HorizontalAlignment.Right;

    public string IncludeAutomationName => _localizationService.GetStringOrDefault(
        "Setting_Include_AutomationName", $"Include {Name} in the file", Name);

    // The box keeps IsThreeState false, so null only ever comes from the getter.
    public bool? IncludeState
    {
        get
        {
            if (Children is not { Count: > 0 } children) return IsIncluded;

            bool allIn = IsIncluded;
            bool noneIn = !IsIncluded;
            foreach (var child in children)
            {
                allIn &= child.IsIncluded;
                noneIn &= !child.IsIncluded;
            }

            return allIn ? true : noneIn ? false : null;
        }
        set
        {
            if (value is not bool included) return;

            IsIncluded = included;
            if (Children is { } children)
            {
                foreach (var child in children)
                {
                    child.IsIncluded = included;
                }
            }

            // The only raise when the click changed nothing - without it the box keeps what was clicked.
            OnPropertyChanged();
        }
    }

    // RebuildSettingIndexes replaces Children wholesale, so this hooks the assignment and not CollectionChanged.
    private ObservableCollection<SettingItemViewModel>? _observedChildren;

    partial void OnChildrenChanged(ObservableCollection<SettingItemViewModel>? value)
    {
        UnsubscribeFromChildren();

        _observedChildren = value;
        if (value is null) return;

        foreach (var child in value)
        {
            child.PropertyChanged += OnChildPropertyChanged;
        }

        OnPropertyChanged(nameof(IncludeState));
    }

    private void UnsubscribeFromChildren()
    {
        if (_observedChildren is not { } children) return;

        foreach (var child in children)
        {
            child.PropertyChanged -= OnChildPropertyChanged;
        }

        _observedChildren = null;
    }

    private void OnChildPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null or nameof(IsIncluded))
        {
            OnPropertyChanged(nameof(IncludeState));
        }
    }

    // Entering Builder rebuilds no card - BuilderSeededEvent fires only for a seed that is not this machine - so
    // the card has to hear the transition itself.
    private void OnApplicationModeChanged(object? sender, EventArgs e)
    {
        if (!AuthorsIntent)
        {
            SetIncludedFromSession(true);
        }

        OnPropertyChanged(nameof(ShowIncludeCheckbox));
        OnPropertyChanged(nameof(CardPadding));
        OnPropertyChanged(nameof(ChildCardPadding));
        OnPropertyChanged(nameof(RowPadding));
        OnPropertyChanged(nameof(IncludeState));
        OnPropertyChanged(nameof(IncludeToolTip));
        OnPropertyChanged(nameof(ShowPasswordBanner));
    }

    internal void SetIncludedFromSession(bool included)
    {
        if (IsIncluded == included) return;

        bool wasSyncing = _isSyncingInclude;
        _isSyncingInclude = true;
        try
        {
            IsIncluded = included;
        }
        finally
        {
            _isSyncingInclude = wasSyncing;
        }
    }

    public bool IsKeyedSelection => Setting?.Control == ControlKind.KeyedSelection;

    // Only power plans offer an option this machine does not have (a predefined plan not yet installed).
    public bool ShowsOptionStatus => IsKeyedSelection && Setting?.Options?.Source == OptionSource.PowerPlans;

    // Apart from SelectedValue so an authored pick in Builder does not move the Active badge off the running scheme.
    public string? LiveKeyedSelection { get; private set; }

    public string KeyedActiveBadgeText => _localizationService.GetString("PowerPlan_Active_Badge");

    public string KeyedDeleteTooltipText => _localizationService.GetString("PowerPlan_Delete_Tooltip");

    public string KeyedInstalledTooltipText => _localizationService.GetString("PowerPlan_Status_Exists");

    public string KeyedNotInstalledTooltipText => _localizationService.GetString("PowerPlan_Status_NotExists");

    // A powercfg setting carries exactly one PowerCfgTarget whose Mode is PowerModeSupport. Non-powercfg
    // settings have no PowerCfgTarget -> false.
    public bool SupportsSeparateACDC =>
        Setting?.Targets.OfType<PowerCfgTarget>().FirstOrDefault()?.Mode == PowerModeSupport.Separate;

    private bool IsPowerCfgSetting =>
        Setting?.Targets.OfType<PowerCfgTarget>().Any() == true;

    // Detection-outcome presentation.
    //
    // The overlay does not REPLACE the ToggleSwitch, it covers it. The switch is always measured, so the
    // toggle column is one width on every row (that is what keeps the Quick-Set buttons aligned); when the
    // setting is unresolved the switch is simply made invisible and inert underneath the overlay.
    //
    // Everything below is driven from Outcome through ONE map, because the same icon and text appear in
    // three places (the overlay knob, the selection adornment, the banner) and three hard-coded copies
    // would eventually disagree. All of it is consumed with x:Bind function-call syntax in
    // SettingsCardItem.xaml, which re-evaluates whenever Outcome raises PropertyChanged - the same pattern
    // the A11yName(...) bindings already use.

    // Every non-Resolved outcome shows it, so a bad state is always visible and the toggle column keeps one
    // footprint; only whether it RESPONDS differs (IsActionable).
    public bool ShowsStateOverlay => Outcome != SettingDetectionOutcome.Resolved;

    // False for Undetermined: detection failed, so offering Enabled/Disabled would write blind over data we could
    // not read; the overlay is drawn but inert.
    public bool IsActionable => Outcome is SettingDetectionOutcome.Custom or SettingDetectionOutcome.Malformed;

    // Colour carries the severity and matches the banner severity: blue question (a choice to make) / yellow
    // exclamation (a recoverable fault) / red cross (we could not read it).
    public FluentIcons.Common.Icon OverlayIconFor(SettingDetectionOutcome outcome) => outcome switch
    {
        SettingDetectionOutcome.Malformed => FluentIcons.Common.Icon.ErrorCircle,
        SettingDetectionOutcome.Undetermined => FluentIcons.Common.Icon.DismissCircle,
        _ => FluentIcons.Common.Icon.QuestionCircle,
    };

    // Each matches its icon ("?" / "!" / "x") so label and knob never disagree; localizable so a language can
    // substitute a better glyph.
    public string OverlayShortLabelFor(SettingDetectionOutcome outcome) => outcome switch
    {
        SettingDetectionOutcome.Malformed =>
            _localizationService.GetStringOrDefault("Common_MalformedState_ShortLabel", "!"),
        SettingDetectionOutcome.Undetermined =>
            _localizationService.GetStringOrDefault("Common_UndeterminedState_ShortLabel", "x"),
        _ => _localizationService.GetStringOrDefault("Common_CustomState_ShortLabel", "?"),
    };

    public string OverlayStateTextFor(SettingDetectionOutcome outcome) => outcome switch
    {
        SettingDetectionOutcome.Malformed =>
            Localized("Common_MalformedState") ?? "Wrong format",
        SettingDetectionOutcome.Undetermined =>
            Localized("Common_UndeterminedState") ?? "Couldn't read",
        // One wording for every setting, and not "User Defined": that asserts a cause we cannot know - a
        // debloat tool, an OEM image or a policy could equally have set the value. The banner carries the
        // reassuring tone ("you can leave it as it is"); this label only has to be accurate. "Custom" itself
        // is reserved for states the catalog genuinely offers as a CHOICE (system tray icons, visual effects,
        // ads), so a detected value we cannot place says "Not recognized" instead - one word, one meaning.
        _ => Localized("Common_CustomState") ?? "Not recognized",
    };

    // A detect-only state has no ComboBox item, so this tells the card which real state name to draw where the
    // item would have been; the outcome stays Resolved - no banner, no icon.
    public SettingState? DetectOnlySelectedState =>
        Setting is { } catalogSetting
        && SelectedValue is int stateIndex
        && stateIndex >= 0 && stateIndex < catalogSetting.States.Count
        && catalogSetting.States[stateIndex].IsDetectOnly
            ? catalogSetting.States[stateIndex]
            : null;

    // Resolved through the SAME Setting_{id}_Option_{i} key the dropdown would have used.
    public string DetectOnlyStateText
    {
        get
        {
            if (Setting is not { } catalogSetting
                || SelectedValue is not int stateIndex
                || DetectOnlySelectedState is not { } state)
            {
                return string.Empty;
            }

            return Localized(state.Label.Value) ?? state.Label.Value;
        }
    }

    // Only the single-value mode can: the AC/DC modes carry powercfg option indices, and no powercfg selection
    // authors a detect-only state.
    private bool IsDetectOnlyForMode(SettingInputMode mode) =>
        mode is not (SettingInputMode.Ac or SettingInputMode.Dc) && DetectOnlySelectedState is not null;

    private string? Localized(string key) =>
        _localizationService.TryGetString(key, out var text) ? text : null;

    // The SAME string that outcome's banner shows, so the two can never drift.
    public string OverlayTooltipFor(SettingDetectionOutcome outcome, bool isTwoState = true)
    {
        string prefix = outcome switch
        {
            SettingDetectionOutcome.Malformed => "Common_MalformedBanner_",
            SettingDetectionOutcome.Undetermined => "Common_UndeterminedBanner_",
            _ => "Common_CustomBanner_",
        };
        return _localizationService.GetStringOrDefault(prefix + (isTwoState ? "Toggle" : "Selection"), string.Empty);
    }

    // Per-MODE resolution. A Separate-mode powercfg setting edits two values (AC and DC) that can be
    // unrecognized independently: the sleep-after setting can sit on a catalog option while plugged in
    // and on some arbitrary number on battery. Outcome is a setting-level verdict from the AC read, so
    // asking it alone would have left a DC-only problem invisible.
    //
    // Rule: a failure to READ is setting-wide (we could not talk to powercfg at all), but a value we do
    // not recognize is per-mode. Everything the shared input controls need is resolved here rather than
    // in XAML, so the ten templates cannot drift apart.

    // Undetermined wins setting-wide; otherwise a mode whose resolved index is the Custom sentinel is unrecognized in its own right.
    public SettingDetectionOutcome OutcomeForMode(SettingInputMode mode)
    {
        if (Outcome == SettingDetectionOutcome.Undetermined)
            return SettingDetectionOutcome.Undetermined;

        return mode switch
        {
            SettingInputMode.Ac => AcValue == ComboBoxConstants.CustomStateIndex
                ? SettingDetectionOutcome.Custom
                : SettingDetectionOutcome.Resolved,
            SettingInputMode.Dc => DcValue == ComboBoxConstants.CustomStateIndex
                ? SettingDetectionOutcome.Custom
                : SettingDetectionOutcome.Resolved,
            _ => Outcome,
        };
    }

    // A DETECT-ONLY current state borrows the overlay for a second job. The overlay exists because a
    // control with nothing selected renders an empty content area; that is exactly the situation here,
    // for the opposite reason - not "we could not place this value" but "this value IS a known state that
    // is not on the list". So it draws the state's own NAME, with no outcome icon and no tooltip: nothing
    // is wrong, and a fault marker would say otherwise. No synthetic option is added to the dropdown -
    // it would be pickable.

    public Microsoft.UI.Xaml.Visibility OverlayVisibilityForMode(SettingInputMode mode) =>
        IsDetectOnlyForMode(mode)
            ? Microsoft.UI.Xaml.Visibility.Visible
            : OverlayVisibilityFor(OutcomeForMode(mode));

    public FluentIcons.Common.Icon OverlayIconForMode(SettingInputMode mode) =>
        OverlayIconFor(OutcomeForMode(mode));

    // False for a detect-only state: the icons are a severity scale and this is not a fault.
    public bool OverlayShowsIconForMode(SettingInputMode mode) => !IsDetectOnlyForMode(mode);

    public string OverlayTextForMode(SettingInputMode mode) =>
        IsDetectOnlyForMode(mode)
            ? DetectOnlyStateText
            : OverlayStateTextFor(OutcomeForMode(mode));

    public string OverlayTooltipForMode(SettingInputMode mode, bool isTwoState) =>
        IsDetectOnlyForMode(mode)
            ? string.Empty
            : OverlayTooltipFor(OutcomeForMode(mode), isTwoState);

    // A DETECT-ONLY state reports -1 too: its state index is real, but the option list has no ITEM there (skipped,
    // never renumbered), so binding the raw index would point past the end; the overlay names the state.
    public int ComboIndexForMode(SettingInputMode mode) => mode switch
    {
        SettingInputMode.Ac => AcValue,
        SettingInputMode.Dc => DcValue,
        _ => IsKeyedSelection ? KeyedOptionPosition(SelectedValue as string)
            : DetectOnlySelectedState is not null ? ComboBoxConstants.CustomStateIndex
            : SelectedValue is int index ? index : ComboBoxConstants.CustomStateIndex,
    };

    // A key the dropdown does not hold answers -1, which the ComboBox reads as nothing selected.
    private int KeyedOptionPosition(string? key)
    {
        for (int i = 0; i < ComboBoxOptions.Count; i++)
        {
            if (ComboBoxOptions[i].Value is string value && string.Equals(value, key, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return ComboBoxConstants.CustomStateIndex;
    }

    public double NumericValueForMode(SettingInputMode mode) => mode switch
    {
        SettingInputMode.Ac => AcNumericValue,
        SettingInputMode.Dc => DcNumericValue,
        _ => NumericValue,
    };

    public bool ShowSelectionQuickSetForMode(SettingInputMode mode) => mode switch
    {
        SettingInputMode.Ac => ShowAcSelectionQuickSetButtons,
        SettingInputMode.Dc => ShowDcSelectionQuickSetButtons,
        _ => ShowSelectionQuickSetButtons,
    };

    public IRelayCommand SelectionRecommendedCommandForMode(SettingInputMode mode) => mode switch
    {
        SettingInputMode.Ac => SetAcSelectionToRecommendedCommand,
        SettingInputMode.Dc => SetDcSelectionToRecommendedCommand,
        _ => SetSelectionToRecommendedCommand,
    };

    public IRelayCommand SelectionDefaultCommandForMode(SettingInputMode mode) => mode switch
    {
        SettingInputMode.Ac => SetAcSelectionToDefaultCommand,
        SettingInputMode.Dc => SetDcSelectionToDefaultCommand,
        _ => SetSelectionToDefaultCommand,
    };

    public string? SelectionRecommendedTooltipForMode(SettingInputMode mode) => mode switch
    {
        SettingInputMode.Ac => AcSelectionRecommendedTooltip,
        SettingInputMode.Dc => DcSelectionRecommendedTooltip,
        _ => SelectionRecommendedTooltip,
    };

    public string? SelectionDefaultTooltipForMode(SettingInputMode mode) => mode switch
    {
        SettingInputMode.Ac => AcSelectionDefaultTooltip,
        SettingInputMode.Dc => DcSelectionDefaultTooltip,
        _ => SelectionDefaultTooltip,
    };

    public IRelayCommand NumericRecommendedCommandForMode(SettingInputMode mode) => mode switch
    {
        SettingInputMode.Ac => SetAcNumericToRecommendedCommand,
        SettingInputMode.Dc => SetDcNumericToRecommendedCommand,
        _ => SetNumericToRecommendedCommand,
    };

    public IRelayCommand NumericDefaultCommandForMode(SettingInputMode mode) => mode switch
    {
        SettingInputMode.Ac => SetAcNumericToDefaultCommand,
        SettingInputMode.Dc => SetDcNumericToDefaultCommand,
        _ => SetNumericToDefaultCommand,
    };

    public string? NumericRecommendedTooltipForMode(SettingInputMode mode) => mode switch
    {
        SettingInputMode.Ac => RecommendedAcValueTooltip,
        SettingInputMode.Dc => RecommendedDcValueTooltip,
        _ => RecommendedValueTooltip,
    };

    public string? NumericDefaultTooltipForMode(SettingInputMode mode) => mode switch
    {
        SettingInputMode.Ac => DefaultAcValueTooltip,
        SettingInputMode.Dc => DefaultDcValueTooltip,
        _ => DefaultValueTooltip,
    };

    // Qualified with the power context so a screen reader can tell the plugged-in button from the on-battery one.
    public string A11yNameForMode(SettingInputMode mode, string? action) => mode switch
    {
        SettingInputMode.Ac => A11yAcName(action),
        SettingInputMode.Dc => A11yDcName(action),
        _ => A11yName(action),
    };

    public double PowerColumnWidth(bool hasBattery) => hasBattery ? 120d : double.NaN;

    public string InputAutomationNameForMode(SettingInputMode mode) => mode switch
    {
        SettingInputMode.Ac => AcInputAutomationName,
        SettingInputMode.Dc => DcInputAutomationName,
        _ => Name,
    };

    // Never Collapsed - it must keep occupying its space, or the column reflows.
    public double TwoStateOpacityFor(SettingDetectionOutcome outcome) =>
        outcome == SettingDetectionOutcome.Resolved ? 1d : 0d;

    // False while the overlay covers it, so the overlay is the only thing reachable.
    public bool TwoStateInteractiveFor(SettingDetectionOutcome outcome) =>
        outcome == SettingDetectionOutcome.Resolved;

    // Keeps the invisible ToggleSwitch out of the automation tree while covered, so Narrator announces only the overlay.
    public AccessibilityView TwoStateAccessibilityViewFor(SettingDetectionOutcome outcome) =>
        outcome == SettingDetectionOutcome.Resolved ? AccessibilityView.Content : AccessibilityView.Raw;

    // Fully qualified: this file imports Microsoft.UI.Xaml.Controls but not Microsoft.UI.Xaml.
    public Microsoft.UI.Xaml.Visibility OverlayVisibilityFor(SettingDetectionOutcome outcome) =>
        outcome == SettingDetectionOutcome.Resolved
            ? Microsoft.UI.Xaml.Visibility.Collapsed
            : Microsoft.UI.Xaml.Visibility.Visible;

    // Opacity, not Visibility, so the element stays measured and its host cannot collapse mid-apply; the outcome
    // is untouched, so the feature's banner stays until the apply lands.
    public double OverlayOpacity => IsApplying ? 0d : 1d;

    public string CustomStateText => OverlayStateTextFor(Outcome);


    public string PluggedInText =>
        _localizationService.GetStringOrDefault("PowerStatus_PluggedIn", "Plugged In");
    public string OnBatteryText =>
        _localizationService.GetStringOrDefault("PowerStatus_OnBattery", "On Battery");

    public IAsyncRelayCommand RunActionCommand { get; }

    public SettingItemViewModel(
        SettingItemViewModelConfig config,
        ISettingWriteStrategySelector writeStrategySelector,
        ILogService logService,
        IDispatcherService dispatcherService,
        IDialogService dialogService,
        ILocalizationService localizationService,
        IUserPreferencesService? userPreferencesService = null,
        IRegeditLauncher? regeditLauncher = null,
        INewBadgeService? newBadgeService = null,
        IApplicationModeService? applicationModeService = null,
        IFilePickerService? filePickerService = null)
    {
        _writeStrategySelector = writeStrategySelector;
        _logService = logService;
        _dialogService = dialogService;
        _localizationService = localizationService;
        _userPreferencesService = userPreferencesService;
        _newBadgeService = newBadgeService;
        _applicationModeService = applicationModeService;
        _filePickerService = filePickerService;

        _localizationService.LanguageChanged += OnLanguageChanged;
        if (_applicationModeService is { } modeService)
        {
            modeService.ModeChanged += OnApplicationModeChanged;
        }

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
        Outcome = config.Outcome;
        OnText = config.OnText;
        OffText = config.OffText;
        ActionButtonText = config.ActionButtonText;

        Status = string.Empty;
        ComboBoxOptions = new ObservableCollection<ComboBoxDisplayOption>();
        MaxValue = 100;
        Units = string.Empty;
        IsVisible = true;
        IsEnabled = true;
        ParentIsEnabled = true;
        ParentIsVisible = true;
        Rows.CollectionChanged += (_, _) => OnRowsChanged();

        RunActionCommand = new AsyncRelayCommand(RunActionAsync);
        UnlockCommand = new AsyncRelayCommand(HandleUnlockAsync);

        IsNew = _newBadgeService?.IsSettingNew(
            config.Setting.Display.AddedInVersion, config.SettingId) == true;

        _statusBannerManager = new SettingStatusBannerManager(localizationService);
        // Labels are resolved by key inside the builder, so there is no label map here to fall out of
        // sync with what the panel actually asks for.
        _technicalDetailsManager = new TechnicalDetailsManager(
            () => SettingId,
            newSections =>
            {
                TechnicalDetailMatrix = newSections;
                OnPropertyChanged(nameof(HasTechnicalDetails));
                OnPropertyChanged(nameof(ShowTechnicalDetailsBar));
            },
            logService,
            dispatcherService,
            regeditLauncher,
            _localizationService,
            _build);

        InitializeHasBadgeData();
        ComputeBadgeState();
    }

    public void RefreshTechnicalDetails()
    {
        var snapshot = new SettingStateSnapshot
        {
            InputType = InputType,
            IsSelected = IsSelected,
            SelectedIndex = SelectedValue as int?,
            SelectedKey = SelectedValue as string,
            NumericValue = NumericValue,
            AcValue = AcValue,
            DcValue = DcValue,
            AcNumericValue = AcNumericValue,
            DcNumericValue = DcNumericValue,
            SupportsSeparateACDC = SupportsSeparateACDC,
            HasBattery = HasBattery,
            Options = new List<ComboBoxDisplayOption>(ComboBoxOptions),
            Outcome = Outcome,
            // Only populated when detection resolved to Custom, which is exactly when the panel
            // reports the live readings instead of marking an option as current.
            Readings = CapturedCustomStateValues,
        };
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
                    Outcome = SettingDetectionOutcome.Resolved;
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
                        Outcome = SettingDetectionOutcome.Resolved;
                }
                else if (value != null)
                {
                    SelectedValue = value;
                    // A real option index means the selection is no longer Custom (mirrors the toggle
                    // branch, same self-apply guard). A Custom sentinel or non-index payload leaves it.
                    if (!IsApplying && value is int realIdx && realIdx != ComboBoxConstants.CustomStateIndex)
                        Outcome = SettingDetectionOutcome.Resolved;
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
            // An authoring mode applies nothing, so a SettingAppliedEvent should never reach a card
            // holding authored values. Re-applying anyway costs nothing and means the invariant
            // "every live-state writer re-applies the overlay" has no exception to remember.
            ApplyAuthoredOverlay();

            _isUpdatingFromEvent = false;
            ComputeBadgeState();
            UpdateDetectionOutcomeBanner();
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

    public bool TryApplyKeyedOptions(SettingStateResult state)
    {
        if (InputType != InputType.Selection || !IsKeyedSelection || state.DynamicOptions is not { } options)
            return false;

        ComboBoxOptions.Clear();
        // The Tag carries the option for SettingComboBox (status dot, delete button) and the technical details (status).
        foreach (var option in options)
            ComboBoxOptions.Add(new ComboBoxDisplayOption(option.Label, option.Value, tag: option));

        LiveKeyedSelection = state.DynamicSelection is { Length: > 0 } live ? live : null;

        if (LiveKeyedSelection is { } key)
        {
            EnsureKeyedOption(key);
            SelectedValue = key;
        }

        UpdateStatusBanner(SelectedValue);

        // Clearing a ComboBox's ItemsSource resets SelectedIndex to -1, and SettingComboBox re-pushes the index only
        // from PropertyChanged, which a rebuild onto the SAME key raises for nothing else.
        OnPropertyChanged(nameof(ComboBoxOptions));
        RebuildTiles();
        return true;
    }

    // Added rather than dropped: dropping it would show the machine's own value while Save still wrote the authored key.
    private void EnsureKeyedOption(string key, string? label = null)
    {
        if (KeyedOptionPosition(key) >= 0)
            return;

        var display = label ?? _localizationService.GetStringOrDefault(
            "Setting_KeyedOption_Unavailable", "{0} (not available on this PC)", key);
        ComboBoxOptions.Add(new ComboBoxDisplayOption(display, key));
        RebuildTiles();
    }

    public void UpdateStateFromSystemState(SettingStateResult state)
    {
        if (!state.Success) return;
        RecordKnownState(state);
        _isUpdatingFromEvent = true;
        try
        {
            switch (InputType)
            {
                case InputType.Toggle:
                case InputType.CheckBox:
                    IsSelected = state.IsEnabled;
                    Outcome = state.Outcome;
                    break;
                case InputType.Selection:
                    if (TryApplyKeyedOptions(state))
                        break;

                    // The detection result's flag is the source of truth on refresh (mirrors the factory load).
                    Outcome = state.Outcome;

                    if (SupportsSeparateACDC && Setting is { States.Count: > 0 } sel
                        && sel.Targets.OfType<PowerCfgTarget>().FirstOrDefault() is { } powerTarget)
                    {
                        if (state.AcValue is int acRaw)
                            AcValue = FindStateIndexForPowerCfgValue(sel, powerTarget.Key, acRaw) ?? ComboBoxConstants.CustomStateIndex;
                        if (state.DcValue is int dcRaw)
                            DcValue = FindStateIndexForPowerCfgValue(sel, powerTarget.Key, dcRaw) ?? ComboBoxConstants.CustomStateIndex;
                    }
                    // Not a keyed card: its CurrentValue is the provider's index placeholder, not a key.
                    else if (state.CurrentValue != null && !IsKeyedSelection)
                    {
                        // The option list holds only real options, so an unresolved re-detect simply leaves the
                        // ComboBox with nothing selected and the card's outcome overlay renders over it. The whole
                        // method already runs under _isUpdatingFromEvent, guarding programmatic control updates.
                        SelectedValue = state.CurrentValue;
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
                case InputType.TextBox:
                    if (state.CurrentValue is string text)
                        TextValue = text;
                    break;
                case InputType.List:
                    // The provider reads no machine for a list, so anything but a rebuilt record leaves the rows alone.
                    if (state.CurrentValue is ChoiceValue.List rebuilt)
                        RebuildRows(rebuilt);
                    break;
            }
        }
        finally
        {
            // Before the badge/banner/details rebuild, so all three describe what the card will
            // actually show. A refresh that lands during authoring has just overwritten the user's
            // values with the machine's; this puts theirs back.
            ApplyAuthoredOverlay();

            _isUpdatingFromEvent = false;
            ComputeBadgeState();
            UpdateDetectionOutcomeBanner();
            RefreshTechnicalDetails();
        }
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

    public void OnToggleSwitchToggled(object sender)
    {
        if (sender is ToggleSwitch toggle)
            HandleTwoStateAsync(toggle.IsOn).FireAndForget(_logService);
    }

    public void OnCheckBoxChanged(bool isChecked) => HandleTwoStateAsync(isChecked).FireAndForget(_logService);

    public void OnTextBoxChanged(string raw) => HandleTextAsync(raw).FireAndForget(_logService);

    public void AddRow()
    {
        if (NewListRow(null) is not { } row)
            return;

        Rows.Add(row);
        HandleListAsync().FireAndForget(_logService);
    }

    public void OnCustomToggleClicked()
    {
        HandleCustomToggleClickAsync().FireAndForget(_logService);
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
    public void OnComboBoxDropDownClosed(object sender)
    {
        if (sender is ComboBox comboBox && comboBox.SelectedValue is { } value)
            HandleValueChangedAsync(value).FireAndForget(_logService);
    }

    public void ApplySelectionValue(object value)
    {
        _logService.LogDebug($"ApplySelectionValue called with value={value}, SettingId={SettingId}");
        HandleValueChangedAsync(value).FireAndForget(_logService);
    }

    public void OnNumberBoxValueChanged(NumberBoxValueChangedEventArgs e)
    {
        if (!double.IsNaN(e.NewValue))
            HandleValueChangedAsync((int)e.NewValue).FireAndForget(_logService);
    }

    public void OnACComboBoxDropDownClosed(object sender)
    {
        if (sender is ComboBox cb && cb.SelectedIndex >= 0)
        {
            AcValue = cb.SelectedIndex;
            HandleACDCSelectionChangedAsync().FireAndForget(_logService);
        }
    }

    public void OnDCComboBoxDropDownClosed(object sender)
    {
        if (sender is ComboBox cb && cb.SelectedIndex >= 0)
        {
            DcValue = cb.SelectedIndex;
            HandleACDCSelectionChangedAsync().FireAndForget(_logService);
        }
    }

    public void OnACNumberBoxValueChanged(NumberBoxValueChangedEventArgs e)
    {
        if (!double.IsNaN(e.NewValue))
            AcNumericInputChanged((int)e.NewValue);
    }

    public void OnDCNumberBoxValueChanged(NumberBoxValueChangedEventArgs e)
    {
        if (!double.IsNaN(e.NewValue))
            DcNumericInputChanged((int)e.NewValue);
    }

    // Split from the event handler above because NumberBoxValueChangedEventArgs cannot be constructed in a test.
    // The box re-raises ValueChanged every time its OneWay binding re-pushes the number it already holds, which
    // is what happens when the DC input appears after the hardware filter reveals other-hardware settings. The
    // value did not change, so there is nothing to write - and in Builder mode a write here recorded a setting
    // the user never touched. HandleValueChangedAsync makes the same check for the single-value input.
    public void AcNumericInputChanged(int value)
    {
        if (value == AcNumericValue)
            return;

        AcNumericValue = value;
        HandleACDCNumericChangedAsync().FireAndForget(_logService);
    }

    public void DcNumericInputChanged(int value)
    {
        if (value == DcNumericValue)
            return;

        DcNumericValue = value;
        HandleACDCNumericChangedAsync().FireAndForget(_logService);
    }

    // Invariant-culture NumberFormatter so the box parses and formats en-US regardless of system locale - Russian
    // "50.000" for 50000 otherwise produces wrong values.
    public void OnNumberBoxLoaded(object sender)
    {
        if (sender is NumberBox nb)
            nb.NumberFormatter = CreateInvariantNumberFormatter();
    }

    private static readonly string[] InvariantFormatterLanguages = ["en-US"];

    private static DecimalFormatter CreateInvariantNumberFormatter()
    {
        var formatter = new DecimalFormatter(InvariantFormatterLanguages, "US")
        {
            FractionDigits = 0,
            IsGrouped = false
        };
        return formatter;
    }

    private async Task HandleTwoStateAsync(bool newValue, bool resetToDefault = false, bool fromCustomState = false)
    {
        if (IsApplying || _isUpdatingFromEvent) return;

        // A Custom-state pick bypasses the equality guard: a Custom toggle sits at IsSelected=false,
        // so picking Disabled (false) would otherwise be swallowed here.
        if (!fromCustomState && newValue == IsSelected) return;

        var result = await WriteAsync(new SettingWriteRequest
        {
            Description = TwoState.Label(Setting!.Control, newValue).Value,
            SystemRequest = new ApplySettingRequest
            {
                SettingId = SettingId,
                Enable = newValue,
                ResetToDefault = resetToDefault,
            },
            AuthoredEdit = new SettingChoice(SettingId, ChoiceValue.TwoState(Setting!.Control, newValue)),
            // The Custom-state dialog already confirmed intent - never double-confirm.
            RequiresConfirmation = SettingRequiresConfirmation && !fromCustomState,
        });

        if (result.Outcome == SettingWriteOutcome.Rejected)
        {
            OnPropertyChanged(nameof(IsSelected));
            return;
        }

        IsSelected = newValue;
        if (fromCustomState)
        {
            // Only on a write that stuck: a rejected one leaves the value untouched and the
            // Custom overlay in place.
            Outcome = SettingDetectionOutcome.Resolved;
            UpdateDetectionOutcomeBanner();
        }
        MarkChangedThisSession();
        ComputeBadgeState();

        if (result.Outcome == SettingWriteOutcome.Applied)
            ShowRestartBannerIfNeeded();
    }

    // Cancel is the default (safe Enter) and keeps the current value. A pick applies EXACTLY once via
    // HandleTwoStateAsync(fromCustomState: true): no second confirmation, equality guard bypassed. The Malformed
    // message promises the storage format will be repaired, which the apply delivers for free: every write passes
    // the catalog's declared RegistryValueKind.
    private async Task HandleCustomToggleClickAsync()
    {
        // IsActionable is the safety gate: Undetermined never opens the dialog, so we can never
        // apply a state over a value we failed to read.
        if (IsApplying || !IsActionable) return;

        string messageKey = Outcome == SettingDetectionOutcome.Malformed
            ? "Common_MalformedDialog_Message"
            : "Common_CustomDialog_Message";

        var r = await _dialogService.ShowConfirmationAsync(new ConfirmationRequest
        {
            Title = Name,
            Message = _localizationService.GetStringOrDefault(messageKey, string.Empty),
            ConfirmButtonText = _localizationService.GetStringOrDefault("Common_Enabled", "Enabled"),
            SecondaryButtonText = _localizationService.GetStringOrDefault("Common_Disabled", "Disabled"),
            CancelButtonText = _localizationService.GetStringOrDefault("Button_Cancel", "Cancel"),
        });

        if (r.Confirmed)
            await HandleTwoStateAsync(true, fromCustomState: true);
        else if (r.SecondaryChosen)
            await HandleTwoStateAsync(false, fromCustomState: true);
        // Cancel: keep the unrecognized value and the Custom rendering.
    }

    private async Task HandleTextAsync(string raw, bool browsed = false)
    {
        if (_isUpdatingFromEvent || Setting?.TextBox?.Rule is not { } rule)
            return;

        // The box raises TextChanged for the value this card just pushed into it.
        if (raw == TextValue && !browsed)
            return;

        TextValue = raw;
        HasTextError = raw.Length > 0 && !rule.Matches(raw);
        if (HasTextError)
            return;

        var normalized = rule.Normalize(raw);
        if (normalized == _lastRecordedText && !browsed)
        {
            _logService.LogDebug($"Text for {SettingId} is unchanged, nothing to write");
            return;
        }

        var result = await WriteAsync(new SettingWriteRequest
        {
            Description = $"text ({normalized.Length} chars)",
            SystemRequest = new ApplySettingRequest
            {
                SettingId = SettingId,
                Enable = true,
                Value = normalized,
            },
            AuthoredEdit = new SettingChoice(SettingId, new ChoiceValue.Text(normalized)),
        });

        if (result.Outcome == SettingWriteOutcome.Rejected)
            return;

        _lastRecordedText = normalized;
        MarkChangedThisSession();
        ComputeBadgeState();
    }

    internal void SeedText(string value)
    {
        TextValue = value;
        _lastRecordedText = value;
    }

    // Not a locale key: no caption exists for a picker filter. IFilePickerService reads the array as a
    // name/pattern PAIR, not as a list of patterns.
    private const string PictureFilterName = "Pictures";
    private const string PictureFilterPattern = "*.jpg;*.jpeg;*.png;*.bmp";

    // Choosing the path the box already shows still applies it: that is how an album is started again.
    public void BrowseForText()
    {
        if (_filePickerService is not { } picker)
            return;

        var chosen = TextBoxPicker switch
        {
            PickerKind.Folder => picker.PickFolder(Name),
            PickerKind.File => picker.PickFile([PictureFilterName, PictureFilterPattern], Name),
            _ => null,
        };

        _logService.LogDebug($"Browse for {SettingId} chose {(chosen is { Length: > 0 } ? chosen : "nothing")}");
        if (chosen is { Length: > 0 })
            HandleTextAsync(chosen, browsed: true).FireAndForget(_logService);
    }

    internal void SeedList(ChoiceValue.List value) => RebuildRows(value);

    private void OnRowsChanged()
    {
        OnPropertyChanged(nameof(HasRows));
        foreach (var row in Rows)
            row.CanRemove = Rows.Count > 1;
    }

    private ListRowViewModel? NewListRow(ChoiceValue.ListRow? values)
    {
        if (Setting?.List is not { } list)
        {
            _logService.Log(LogLevel.Warning, $"List row for {SettingId} has no catalog fields; the row is dropped.");
            return null;
        }

        return new ListRowViewModel(list.Fields, LocalizedField, ListRemoveLabel, values, OnListRowChanged, RemoveRow);
    }

    private string LocalizedField(LocKey key) => _localizationService.GetStringOrDefault(key.Value, key.Value);

    private void OnListRowChanged(ListRowViewModel row)
    {
        foreach (var field in row.Fields)
        {
            if (!field.OneRowOnly || !field.Checked)
                continue;

            foreach (var other in Rows.Where(r => r != row))
                other.FieldFor(field.Key)?.ClearTick();
        }

        HandleListAsync().FireAndForget(_logService);
    }

    private void RemoveRow(ListRowViewModel row)
    {
        Rows.Remove(row);
        HandleListAsync().FireAndForget(_logService);
    }

    private void RebuildRows(ChoiceValue.List value)
    {
        Rows.Clear();
        foreach (var values in value.Rows)
        {
            if (NewListRow(values) is { } row)
                Rows.Add(row);
        }
    }

    // No read-back after the write: rebuilding the rows would take the caret out of the box being typed in.
    private async Task HandleListAsync()
    {
        if (_isUpdatingFromEvent)
            return;

        var filled = Rows.Where(row => row.IsFilled).Select(row => row.ToRow()).ToList();

        var result = await WriteAsync(new SettingWriteRequest
        {
            Description = $"list {filled.Count} rows",
            SystemRequest = new ApplySettingRequest
            {
                SettingId = SettingId,
                Enable = true,
            },
            AuthoredEdit = new SettingChoice(SettingId, new ChoiceValue.List(filled, SavePasswords)),
        });

        if (result.Outcome == SettingWriteOutcome.Rejected)
            return;

        MarkChangedThisSession();
        ComputeBadgeState();
    }

    public bool ShowsOptionTiles =>
        IsKeyedSelection && Setting?.Display.Tiles is { } tiles && tiles != OptionTiles.None;

    public ObservableCollection<OptionTileViewModel> Tiles { get; } = [];

    private void RebuildTiles()
    {
        Tiles.Clear();
        if (!ShowsOptionTiles)
            return;

        foreach (var option in ComboBoxOptions)
        {
            if (option.Value is string value)
                Tiles.Add(new OptionTileViewModel(value, TileLabel(value, option.DisplayText), IsChosenTile(value), ChooseTile));
        }

        OnPropertyChanged(nameof(Tiles));
    }

    private string TileLabel(string value, string enumerated) =>
        Setting is { } setting
        && setting.States.FirstOrDefault(state =>
            string.Equals(KeyedOptions.KeyOf(setting, state), value, StringComparison.OrdinalIgnoreCase)) is { } named
            ? _localizationService.GetStringOrDefault(named.Label.Value, enumerated)
            : enumerated;

    private bool IsChosenTile(string value) =>
        SelectedValue is string key && string.Equals(key, value, StringComparison.OrdinalIgnoreCase);

    private void ChooseTile(OptionTileViewModel tile) =>
        HandleValueChangedAsync(tile.Value).FireAndForget(_logService);

    partial void OnSelectedValueChanged(object? value)
    {
        foreach (var tile in Tiles)
            tile.IsSelected = value is string key
                && string.Equals(tile.Value, key, StringComparison.OrdinalIgnoreCase);
    }

    public void BrowseForOption()
    {
        if (Setting?.Display.Tiles != OptionTiles.Pictures || _filePickerService is not { } picker)
            return;

        if (picker.PickFile([PictureFilterName, PictureFilterPattern], Name) is not { Length: > 0 } chosen)
            return;

        var chosenFormat = _localizationService.GetStringOrDefault(
            LocKey.Setting.ThemeWallpaperPicture.Chosen.Value, "{0}");
        EnsureKeyedOption(chosen, string.Format(chosenFormat, Path.GetFileName(chosen)));
        HandleValueChangedAsync(chosen).FireAndForget(_logService);
    }

    public void PickColor(Windows.UI.Color color)
    {
        if (Setting?.Display.Tiles != OptionTiles.Colors)
            return;

        var hex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        EnsureKeyedOption(hex, hex);
        HandleValueChangedAsync(hex).FireAndForget(_logService);
    }

    private async Task HandleValueChangedAsync(object? value, bool resetToDefault = false, string? description = null)
    {
        _logService.LogDebug($"HandleValueChangedAsync called: value={value}, IsApplying={IsApplying}, SelectedValue={SelectedValue}");

        if (_isUpdatingFromEvent || value == null)
        {
            _logService.LogDebug($"HandleValueChangedAsync early return: _isUpdatingFromEvent={_isUpdatingFromEvent}, value={(value == null ? "null" : "not null")}");
            return;
        }

        if (IsApplying)
        {
            _logService.LogDebug($"HandleValueChangedAsync: queuing pending value {value} for {SettingId}");
            _pendingValue = value;
            return;
        }

        if (Equals(value, SelectedValue))
        {
            _logService.LogDebug($"HandleValueChangedAsync: value equals SelectedValue, skipping");
            return;
        }

        try
        {
            var result = await WriteAsync(new SettingWriteRequest
            {
                Description = description ?? $"value {value}",
                SystemRequest = new ApplySettingRequest
                {
                    SettingId = SettingId,
                    Enable = true,
                    Value = value,
                    ResetToDefault = resetToDefault,
                },
                AuthoredEdit = AuthoredEditForValue(value),
                RequiresConfirmation = SettingRequiresConfirmation,
            });

            if (result.Outcome == SettingWriteOutcome.Rejected)
            {
                OnPropertyChanged(nameof(SelectedValue));
                OnPropertyChanged(nameof(NumericValue));
                return;
            }

            SelectedValue = value;

            if (value is int intValue)
            {
                NumericValue = intValue;

                if (intValue != ComboBoxConstants.CustomStateIndex)
                {
                    Outcome = SettingDetectionOutcome.Resolved;
                }
            }

            MarkChangedThisSession();
            ComputeBadgeState();
            UpdateStatusBanner(value);

            if (result.Outcome == SettingWriteOutcome.Applied)
                ShowRestartBannerIfNeeded();
        }
        finally
        {
            await ProcessPendingValueAsync();
        }
    }

    // Null when this input carries a value shape the config format cannot represent - which the authoring strategy
    // reports rather than swallowing.
    private SettingChoice? AuthoredEditForValue(object? value) => (InputType, value) switch
    {
        (InputType.Selection, string key) when IsKeyedSelection =>
            new SettingChoice(SettingId, new ChoiceValue.Keyed(key, KeyedOptionLabel(key))),

        (InputType.Selection, int index) when index == ComboBoxConstants.CustomStateIndex =>
            CapturedCustomStateValues is { } custom ? new SettingChoice(SettingId, new ChoiceValue.CustomValues(custom)) : null,

        (InputType.Selection, int index) => new SettingChoice(SettingId, new ChoiceValue.Option(index)),

        (InputType.TextBox, string text) => new SettingChoice(SettingId, new ChoiceValue.Text(text)),

        // The slider carries DISPLAY units and a ChoiceValue holds SYSTEM units.
        (InputType.NumericRange, int numeric) => new SettingChoice(SettingId, new ChoiceValue.Number(ConvertToSystemUnits(numeric))),

        _ => null,
    };

    private string KeyedOptionLabel(string key)
    {
        int position = KeyedOptionPosition(key);
        return position >= 0 ? ComboBoxOptions[position].DisplayText : key;
    }

    private async Task ProcessPendingValueAsync()
    {
        var pending = _pendingValue;
        _pendingValue = null;

        if (pending != null && !Equals(pending, SelectedValue))
        {
            _logService.LogDebug($"Processing pending value {pending} for {SettingId}");
            await HandleValueChangedAsync(pending);
        }
    }

    private async Task HandleACDCSelectionChangedAsync(bool resetToDefault = false)
    {
        if (IsApplying || _isUpdatingFromEvent) return;

        // AcValue/DcValue are already set by the caller; the write only has to carry them.
        var result = await WriteAsync(new SettingWriteRequest
        {
            Description = $"AC/DC selection AC={AcValue}, DC={DcValue}",
            SystemRequest = new ApplySettingRequest
            {
                SettingId = SettingId,
                Enable = true,
                Value = new Dictionary<string, object?> { ["ACValue"] = AcValue, ["DCValue"] = DcValue },
                ResetToDefault = resetToDefault,
            },
            AuthoredEdit = new SettingChoice(SettingId, new ChoiceValue.AcDcOption(AcValue, DcValue)),
            // The AC/DC inputs have never prompted, whatever the setting declares. No power setting
            // declares RequiresConfirmation today, so this states the intent rather than suppressing
            // a prompt anyone would see.
            RequiresConfirmation = false,
        });

        if (result.Outcome == SettingWriteOutcome.Rejected)
        {
            OnPropertyChanged(nameof(AcValue));
            OnPropertyChanged(nameof(DcValue));
            return;
        }

        // The write landed on known option indices - clear a loaded Custom state and its
        // "Select an option" banner (mirrors the toggle/selection paths).
        Outcome = SettingDetectionOutcome.Resolved;
        UpdateDetectionOutcomeBanner();
        MarkChangedThisSession();
        ComputeBadgeState();

        if (result.Outcome == SettingWriteOutcome.Applied)
            ShowRestartBannerIfNeeded();
    }

    private async Task HandleACDCNumericChangedAsync(bool resetToDefault = false)
    {
        if (IsApplying || _isUpdatingFromEvent) return;

        // AcNumericValue/DcNumericValue are already set by the caller; the write only carries them.
        var result = await WriteAsync(new SettingWriteRequest
        {
            Description = $"AC/DC numeric AC={AcNumericValue}, DC={DcNumericValue}",
            SystemRequest = new ApplySettingRequest
            {
                SettingId = SettingId,
                Enable = true,
                Value = new Dictionary<string, object?> { ["ACValue"] = AcNumericValue, ["DCValue"] = DcNumericValue },
                ResetToDefault = resetToDefault,
            },
            // Converted because the sliders carry DISPLAY units and every consumer of a ChoiceValue
            // - the config file included - speaks SYSTEM units.
            AuthoredEdit = new SettingChoice(SettingId, new ChoiceValue.AcDcNumber(ConvertToSystemUnits(AcNumericValue), ConvertToSystemUnits(DcNumericValue))),
            // As with the AC/DC dropdowns: these inputs have never prompted.
            RequiresConfirmation = false,
        });

        if (result.Outcome == SettingWriteOutcome.Rejected)
        {
            OnPropertyChanged(nameof(AcNumericValue));
            OnPropertyChanged(nameof(DcNumericValue));
            return;
        }

        MarkChangedThisSession();
        ComputeBadgeState();

        if (result.Outcome == SettingWriteOutcome.Applied)
            ShowRestartBannerIfNeeded();
    }

    private async Task RunActionAsync()
    {
        if (IsApplying) return;

        var result = await WriteAsync(new SettingWriteRequest
        {
            Description = "action",
            SystemRequest = new ApplySettingRequest { SettingId = SettingId, Enable = true },
            AuthoredEdit = new SettingChoice(SettingId, new ChoiceValue.Toggle(true)),
            RequiresConfirmation = SettingRequiresConfirmation,
            CheckboxAlsoAppliesRecommended = true,
        });

        if (result.Outcome == SettingWriteOutcome.Recorded)
        {
            // Authoring an action means marking it for inclusion in the saved config, and the card
            // shows that as selected. Running one for real is a one-shot with no lasting state to
            // show, which is why this is keyed on what the write did rather than on the mode.
            IsSelected = true;
            MarkChangedThisSession();
            ComputeBadgeState();
            return;
        }

        if (result.Outcome == SettingWriteOutcome.Applied
            && result.ConfirmationCheckboxChecked
            && ParentFeatureViewModel != null)
        {
            // The checkbox applied the whole feature's recommended settings, so every sibling this
            // card sits beside is now showing a stale value.
            _logService.Log(LogLevel.Info, $"Refreshing parent ViewModel after applying recommended settings for {SettingId}");
            await ParentFeatureViewModel.RefreshSettingsAsync();
        }
    }

    private async Task HandleUnlockAsync()
    {
        if (!IsLocked) return;

        var message = _localizationService.GetString("Dialog_AdvancedPowerWarning_Message");
        var checkboxText = _localizationService.GetString("Dialog_AdvancedPowerWarning_DontShowAgain");
        var title = _localizationService.GetString("Dialog_AdvancedPowerWarning_Title");
        var unlockText = _localizationService.GetStringOrDefault("Button_Unlock", "Unlock");
        var cancelText = _localizationService.GetStringOrDefault("Button_Cancel", "Cancel");

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

    public void UpdateStatusBanner(object? value)
    {
        var banner = _statusBannerManager.ComputeBannerForValue(value, OptionWarnings, CrossGroupInfoMessage, ComboBoxOptions.Count, CompatibilityMessage);
        if (banner.HasValue) ApplyBanner(banner.Value);
        UpdateDetectionOutcomeBanner();
    }

    // Selection settings get it through UpdateStatusBanner's compat fallback.
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
        // A detection-outcome banner gets that outcome's colour icon - the SAME icon the toggle overlay knob
        // and the selection adornment show, from the one map, so the three can never disagree. Every other
        // banner passes null, which InfoBar reads as "use the severity's native icon".
        StatusBannerIconSource = state.DetectionOutcome is { } bannerOutcome
            ? new FluentIcons.WinUI.FluentIconSource
            {
                Icon = OverlayIconFor(bannerOutcome),
                IconVariant = FluentIcons.Common.IconVariant.Color,
            }
            : null;
    }

    // Clears only its own banner once the setting resolves. Priority: Undetermined (Error) > existing
    // compatibility/restart Warnings > Malformed (Warning) > Custom (Informational). Undetermined outranks
    // everything because we could not read the setting at all; the existing Warnings outrank Malformed because
    // they describe the action the user just took, whereas a malformed value is a pre-existing condition.
    internal void UpdateDetectionOutcomeBanner()
    {
        bool isTwoState = InputType == InputType.Toggle || InputType == InputType.CheckBox;
        var outcomeBanner = _statusBannerManager.GetDetectionOutcomeBanner(Outcome, isTwoState);

        if (Outcome != SettingDetectionOutcome.Resolved)
        {
            bool hasBanner = !string.IsNullOrEmpty(StatusBannerMessage);
            if (Outcome == SettingDetectionOutcome.Undetermined)
            {
                ApplyBanner(outcomeBanner); // outranks every other banner
                return;
            }

            // Malformed and Custom both yield to an existing Warning/Error banner.
            if (hasBanner && StatusBannerSeverity != InfoBarSeverity.Informational)
                return;
            ApplyBanner(outcomeBanner);
        }
        else if (!string.IsNullOrEmpty(StatusBannerMessage) && IsDetectionOutcomeBannerMessage(StatusBannerMessage))
        {
            ApplyBanner(SettingStatusBannerManager.BannerState.Clear);
        }
    }

    // Checked against every outcome's text because the outcome has already changed to Resolved by the time we
    // clear - comparing against the current one would miss a banner raised under a different one.
    private bool IsDetectionOutcomeBannerMessage(string message)
    {
        bool isTwoState = InputType == InputType.Toggle || InputType == InputType.CheckBox;
        foreach (var candidate in new[]
                 {
                     SettingDetectionOutcome.Custom,
                     SettingDetectionOutcome.Malformed,
                     SettingDetectionOutcome.Undetermined,
                 })
        {
            if (message == _statusBannerManager.GetDetectionOutcomeBanner(candidate, isTwoState).Message)
                return true;
        }
        return false;
    }

    public void ComputeBadgeState()
    {
        if (!HasBadgeData || Setting == null)
            return;

        bool matchesRecommended = true;
        bool matchesDefault = true;

        bool isTwoState = InputType == InputType.Toggle || InputType == InputType.CheckBox;
        if (isTwoState)
        {
            if (Outcome != SettingDetectionOutcome.Resolved)
            {
                // An unresolved toggle sits on no known state - it matches nothing (mirrors the selection
                // out-of-range verdict below).
                matchesRecommended = false;
                matchesDefault = false;
            }
            else
            {
                if (TwoStateRecommended is bool r && r != IsSelected) matchesRecommended = false;
                if (TwoStateDefault is bool d && d != IsSelected) matchesDefault = false;
            }
        }
        else if (InputType == InputType.Selection && !IsPowerCfgSetting)
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

            // NO Custom pill, deliberately. It would say two things at once - "detection couldn't place this" and
            // "this value is neither Recommended nor Default" - and both are said better elsewhere: the first by the
            // control's own icon plus its banner, which name WHICH kind of problem instead of flattening all three to
            // the word "Custom"; the second by Recommended and Default sitting dim together, which already says
            // "at neither". A Custom pill would also collide with "Custom" the detection outcome, and every control
            // type and mechanism - registry, powercfg, scheduled task - behaves identically with no special case
            // for the numeric up-down, where any value is legitimately the user's own.
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

    private bool HasAnyRecommendedData()
    {
        if (Setting == null) return false;
        bool isTwoState = InputType == InputType.Toggle || InputType == InputType.CheckBox;
        if (isTwoState) return TwoStateRecommended.HasValue;
        if (InputType == InputType.Selection && !IsPowerCfgSetting)
            return Setting.States.Any(st => st.HasRole(RoleKind.Recommended, _build));
        return AcRecommendedValue.HasValue || AcSelectionRecommendedIndex.HasValue
            || DcRecommendedValue.HasValue || DcSelectionRecommendedIndex.HasValue;
    }

    private bool HasAnyDefaultData()
    {
        if (Setting == null) return false;
        bool isTwoState = InputType == InputType.Toggle || InputType == InputType.CheckBox;
        if (isTwoState) return TwoStateDefault.HasValue;
        if (InputType == InputType.Selection && !IsPowerCfgSetting)
            return Setting.States.Any(st => st.HasRole(RoleKind.WindowsDefault, _build));
        return AcDefaultValue.HasValue || AcSelectionDefaultIndex.HasValue
            || DcDefaultValue.HasValue || DcSelectionDefaultIndex.HasValue;
    }

    private (string Label, string Tooltip) ResolvePillStrings(SettingBadgeKind kind, SettingBadgeMode mode = SettingBadgeMode.None)
    {
        var (baseLabel, tooltip) = kind switch
        {
            SettingBadgeKind.Recommended => (
                _localizationService.GetStringOrDefault("InfoBadge_Recommended", "Recommended"),
                _localizationService.GetStringOrDefault("InfoBadge_Recommended_Tooltip", "Winhance's recommended value")),
            SettingBadgeKind.Default => (
                _localizationService.GetStringOrDefault("InfoBadge_Default", "Default"),
                _localizationService.GetStringOrDefault("InfoBadge_Default_Tooltip", "Windows factory value")),
            SettingBadgeKind.Preference => (
                _localizationService.GetStringOrDefault("InfoBadge_Preference", "Preference"),
                _localizationService.GetStringOrDefault("InfoBadge_Preference_Tooltip", "Personal preference")),
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

        bool isTwoState = InputType == InputType.Toggle || InputType == InputType.CheckBox;
        bool hasTwoStateData = isTwoState && (TwoStateRecommended.HasValue || TwoStateDefault.HasValue);
        bool hasSelectionData = InputType == InputType.Selection && !IsPowerCfgSetting
            && Setting.States.Any(st => st.HasRole(RoleKind.Recommended, _build) || st.HasRole(RoleKind.WindowsDefault, _build));
        bool hasPowerCfgData = AcRecommendedValue.HasValue || AcDefaultValue.HasValue
            || AcSelectionRecommendedIndex.HasValue || AcSelectionDefaultIndex.HasValue
            || DcRecommendedValue.HasValue || DcDefaultValue.HasValue
            || DcSelectionRecommendedIndex.HasValue || DcSelectionDefaultIndex.HasValue;
        HasBadgeData = hasTwoStateData || hasSelectionData || hasPowerCfgData;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        // The panel bakes its localized strings in at build time, so it has to be rebuilt on a
        // language switch rather than just raising property-changed.
        RefreshTechnicalDetails();
        OnPropertyChanged(nameof(NewBadgeText));
        OnPropertyChanged(nameof(TechnicalDetailsLabel));
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
        OnPropertyChanged(nameof(TwoStateRecommendedTooltip));
        OnPropertyChanged(nameof(TwoStateDefaultTooltip));
        OnPropertyChanged(nameof(SelectionRecommendedTooltip));
        OnPropertyChanged(nameof(SelectionDefaultTooltip));
        OnPropertyChanged(nameof(AcSelectionRecommendedTooltip));
        OnPropertyChanged(nameof(AcSelectionDefaultTooltip));
        OnPropertyChanged(nameof(DcSelectionRecommendedTooltip));
        OnPropertyChanged(nameof(DcSelectionDefaultTooltip));
        OnPropertyChanged(nameof(IncludeAutomationName));
        OnPropertyChanged(nameof(IncludeToolTip));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _localizationService.LanguageChanged -= OnLanguageChanged;
            if (_applicationModeService is { } modeService)
            {
                modeService.ModeChanged -= OnApplicationModeChanged;
            }

            UnsubscribeFromChildren();
        }
        base.Dispose(disposing);
    }
}

using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentIcons.Common;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Enums;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Services;

namespace Winhance.UI.Features.SoftwareApps.ViewModels;

public partial class AppItemViewModel : ObservableObject, IDisposable
{
    private static readonly string[] SizeUnits = ["B", "KB", "MB", "GB", "TB"];

    private static readonly string[] LocalizedProperties =
    [
        nameof(InstalledStatusText), nameof(ReinstallableStatusText), nameof(DescriptionTooltip), nameof(StatusColumnText),
        nameof(InstabilityWarningLabel), nameof(InstabilityWarningTooltip), nameof(InstalledStatusTooltip),
        nameof(CategoryDisplayName), nameof(UpdateAvailableLabel), nameof(UpdateAvailableTooltip), nameof(NoPackageTooltip),
        nameof(ItemTypeDescription), nameof(OpenWebsiteLabel),
    ];

    private static readonly string[] IconProperties =
        [nameof(IconSource), nameof(ResolvedIconPath), nameof(HasIcon), nameof(NeedsFallbackIcon)];

    private readonly ItemDefinition _definition;
    private readonly ILocalizationService _localizationService;
    private readonly IDispatcherService _dispatcherService;
    private readonly IThemeService _themeService;
    private bool _disposed;

    public AppItemViewModel(
        ItemDefinition definition,
        ILocalizationService localizationService,
        IDispatcherService dispatcherService,
        IThemeService themeService)
    {
        LiveInstanceTracker.Shared.Register(this);
        _definition = definition;
        _localizationService = localizationService;
        _dispatcherService = dispatcherService;
        _themeService = themeService;
        IsInstalled = definition.IsInstalled;

        _localizationService.LanguageChanged += OnLanguageChanged;
        _themeService.ThemeChanged += OnThemeChanged;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _localizationService.LanguageChanged -= OnLanguageChanged;
            _themeService.ThemeChanged -= OnThemeChanged;
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        foreach (var name in LocalizedProperties)
            OnPropertyChanged(name);
    }

    private void OnThemeChanged(object? sender, WinhanceTheme theme) => NotifyIconChanged();

    public ItemDefinition Definition => _definition;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    partial void OnIsSelectedChanged(bool value)
    {
        if (value && !IsSelectable)
            IsSelected = false;
    }

    public string Name => Definition.Name;

    public string Description => Definition.Description;
    public string GroupName => Definition.GroupName ?? string.Empty;
    public string Id => Definition.Id;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InstalledStatusText), nameof(InstalledStatusTooltip), nameof(HasUpdate), nameof(IsInstalledUpToDate),
        nameof(StatusColumnText), nameof(StatusIcon), nameof(StatusTooltip))]
    public partial bool IsInstalled { get; set; }

    partial void OnIsInstalledChanged(bool value) => Definition.IsInstalled = value;

    public bool CanBeReinstalled => Definition.CanBeReinstalled;

    public bool IsOtherInstalled => GroupName == OtherInstalledItem.GroupName;

    // What the installed-apps scan found for this app; null until it runs, and for apps it did not find.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUpdate), nameof(IsInstalledUpToDate), nameof(StatusColumnText), nameof(Version), nameof(VersionSortKey), nameof(AvailableVersion),
        nameof(InstalledOn), nameof(InstalledOnText), nameof(SizeBytes), nameof(SizeText), nameof(UpdateAvailableTooltip),
        nameof(StatusIcon), nameof(StatusTooltip))]
    public partial InstalledApp? Installed { get; set; }

    partial void OnInstalledChanged(InstalledApp? value) => UpdateIsSelectable();

    // Builder writes a file for another PC, which can only install an app a package manager offers.
    [ObservableProperty]
    public partial bool AuthorsIntent { get; set; }

    partial void OnAuthorsIntentChanged(bool value) => UpdateIsSelectable();

    // Every mode change sets AuthorsIntent on all ~400 rows; only an unlisted app without a package can lock, so the
    // rest keep this value and raise nothing that would re-evaluate their bindings.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoPackageTooltip))]
    public partial bool IsSelectable { get; private set; } = true;

    partial void OnIsSelectableChanged(bool value)
    {
        if (!value)
            IsSelected = false;
    }

    private void UpdateIsSelectable() => IsSelectable = !(AuthorsIntent && IsOtherInstalled && PackageId is null);

    public string? PackageId => IsOtherInstalled && Installed is { } app ? OtherInstalledItem.PackageId(app) : null;

    public string? NoPackageTooltip => IsSelectable ? null : _localizationService.GetString("ExternalApps_NoPackageSource_Tooltip",
        _localizationService.GetString("Setting_autounattend-own-installers_Name"), _localizationService.GetString("Nav_Autounattend"));

    public bool HasUpdate => IsInstalled && Installed?.HasUpdate == true;

    public bool IsInstalledUpToDate => IsInstalled && !HasUpdate;

    public string StatusColumnText => HasUpdate ? UpdateAvailableLabel : InstalledStatusText;

    public Icon StatusIcon => HasUpdate ? Icon.Clock : IsInstalled ? Icon.CheckmarkCircle : Icon.DismissCircle;

    public string StatusTooltip => HasUpdate ? UpdateAvailableTooltip : InstalledStatusTooltip;

    public string Version => Installed?.Version ?? string.Empty;

    public System.Version? VersionSortKey => System.Version.TryParse(Version, out var version) ? version : null;

    public string AvailableVersion => Installed?.AvailableVersion ?? string.Empty;

    public DateTime? InstalledOn => Installed?.InstalledOn;

    public string InstalledOnText => InstalledOn?.ToString("d", CultureInfo.CurrentCulture) ?? string.Empty;

    public long? SizeBytes => Installed?.SizeBytes;

    public string SizeText => SizeBytes is { } bytes ? FormatSize(bytes) : string.Empty;

    public string UpdateAvailableLabel => _localizationService.GetString("Card_Pill_UpdateAvailable");

    public string UpdateAvailableTooltip => _localizationService.GetString("Card_Pill_UpdateAvailable_Tooltip", Version, AvailableVersion);

    // Three significant figures, the way File Explorer shows sizes.
    internal static string FormatSize(long bytes)
    {
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < SizeUnits.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        var format = unit == 0 || value >= 100 ? "0" : value >= 10 ? "0.#" : "0.##";
        return $"{value.ToString(format, CultureInfo.CurrentCulture)} {SizeUnits[unit]}";
    }

    public string InstalledStatusText => _localizationService.GetString(
        IsInstalled ? "Status_Installed" : "Status_NotInstalled");

    public string ReinstallableStatusText => _localizationService.GetString(
        CanBeReinstalled ? "Status_CanReinstall" : "Status_CannotReinstall");

    private BitmapImage? _iconSource;
    private string? _iconSourcePath;
    private string? _resolvedIconBase;
    private string? _resolvedIconPath;

    // Cached by path value so a change to IconPath produces a fresh BitmapImage on next read; IconPath is mutated
    // by IAppIconResolver AFTER binding, so mutators call NotifyIconChanged(). Separate from IconSource because path
    // SELECTION is pure logic worth testing on its own - constructing a BitmapImage needs a WinRT/XAML application context.
    public string? ResolvedIconPath
    {
        get
        {
            var basePath = Definition.IconPath;
            if (string.IsNullOrEmpty(basePath))
                return null;

            // Every container that realizes the row reads this, so the variant files are probed once per theme
            // and icon change, not on each read.
            if (_resolvedIconPath is null || _resolvedIconBase != basePath)
            {
                _resolvedIconPath = ResolveThemeAwarePath(basePath);
                _resolvedIconBase = basePath;
            }

            return _resolvedIconPath;
        }
    }

    public BitmapImage? IconSource
    {
        get
        {
            var resolvedPath = ResolvedIconPath;
            if (resolvedPath is null)
            {
                _iconSource = null;
                _iconSourcePath = null;
                return null;
            }

            if (_iconSource is not null && _iconSourcePath == resolvedPath)
                return _iconSource;

            // The card icon's size in logical pixels, so the decode follows the display scale and the
            // smaller compact and table icons scale down from it.
            var bmp = new BitmapImage { DecodePixelWidth = 36, DecodePixelType = DecodePixelType.Logical };
            bmp.UriSource = new Uri(resolvedPath);
            _iconSource = bmp;
            _iconSourcePath = resolvedPath;
            return _iconSource;
        }
    }

    // WinUI keeps the icon file's bytes in its image cache for as long as a BitmapImage points at the file, and a
    // dropped BitmapImage only lets go when the GC collects its wrapper. Clearing UriSource frees them now; the next
    // read of IconSource builds a fresh one.
    public void ReleaseIcon()
    {
        if (_iconSource is null)
            return;

        _iconSource.UriSource = null;
        _iconSource = null;
        _iconSourcePath = null;
    }

    // Mirrors AppIconResolver.LightVariantPath / DarkVariantPath (in a
    // different assembly) — kept inline as one-liners to avoid crossing the
    // assembly boundary for static string derivation. If the naming
    // convention ever changes, update both sites.
    private string ResolveThemeAwarePath(string basePath)
    {
        var stem = Path.ChangeExtension(basePath, null);

        // Light theme: prefer .light.png if synthesized. Dark theme: prefer
        // .dark.png if synthesized (only mono-dark sources have one, e.g.
        // Xbox Game Bar; mono-light sources fall through to the primary
        // which already renders correctly in dark mode).
        if (_themeService.GetEffectiveTheme() == ElementTheme.Light)
        {
            var lightPath = stem + ".light.png";
            if (File.Exists(lightPath)) return lightPath;
        }
        else
        {
            var darkPath = stem + ".dark.png";
            if (File.Exists(darkPath)) return darkPath;
        }

        return basePath;
    }

    public bool HasIcon => !string.IsNullOrEmpty(Definition.IconPath);

    public bool NeedsFallbackIcon => !HasIcon;

    public Icon FallbackIcon =>
        !string.IsNullOrEmpty(Definition.CapabilityName) ? Icon.PuzzlePiece
        : !string.IsNullOrEmpty(Definition.OptionalFeatureName) ? Icon.StarSettings
        : Icon.Apps;

    // Call after mutating Definition.IconPath (e.g. after ResolveBatchAsync) so the bound Image and fallback icon refresh.
    public void NotifyIconChanged()
    {
        _dispatcherService.RunOnUIThread(() =>
        {
            _resolvedIconPath = null;
            foreach (var name in IconProperties)
                OnPropertyChanged(name);
        });
    }

    public string ItemTypeDescription => Definition.Kind switch
    {
        WindowsAppKind.AppxPackage => _localizationService.GetString("SoftwareApps_Type_AppxPackage"),
        WindowsAppKind.Capability => _localizationService.GetString("SoftwareApps_Type_LegacyCapability"),
        WindowsAppKind.OptionalFeature => _localizationService.GetString("SoftwareApps_Type_OptionalFeature"),
        WindowsAppKind.DesktopApp => _localizationService.GetString("SoftwareApps_Type_DesktopApp"),
        _ => string.Empty,
    };

    public string? WebsiteUrl => Definition.WebsiteUrl;

    public Uri? WebsiteUri => Uri.TryCreate(WebsiteUrl, UriKind.Absolute, out var uri) ? uri : null;

    public bool HasWebsite => WebsiteUri is not null;

    public string OpenWebsiteLabel => _localizationService.GetString("Tooltip_OpenWebsite");

    public bool HasDescription => !string.IsNullOrEmpty(Definition.Description);

    public bool HasInstabilityWarning => Definition.HasInstabilityWarning;

    public string InstabilityWarningLabel => _localizationService.GetString("Card_Pill_Warning");

    public string InstabilityWarningTooltip =>
        _localizationService.GetString(Definition.MachineWarning?.TooltipKey ?? "Card_Pill_InstabilityWarning_Tooltip");

    // Null (not empty) when there is nothing to show, so no empty tooltip popup appears. A removed app that
    // Windows cannot put back says so on the last line.
    public string? DescriptionTooltip => (HasDescription, CanBeReinstalled) switch
    {
        (true, true) => Description,
        (true, false) => Description + Environment.NewLine + ReinstallableStatusText,
        (false, false) => ReinstallableStatusText,
        _ => null,
    };

    // Windows capabilities and optional features have no update source, so they cannot claim "up to date".
    public string InstalledStatusTooltip => _localizationService.GetString(
        !IsInstalled ? "Card_Pill_NotInstalled_Tooltip"
        : !string.IsNullOrEmpty(Definition.CapabilityName) || !string.IsNullOrEmpty(Definition.OptionalFeatureName)
            ? "Card_Pill_Installed_Tooltip"
            : "Card_Pill_InstalledUpToDate_Tooltip");

    // Mirrors ExternalAppsViewModel.RebuildCategories: GroupName -> ExternalApps_Category_* key (spaces and & ( )
    // stripped), raw GroupName as fallback.
    public string CategoryDisplayName
    {
        get
        {
            if (string.IsNullOrEmpty(GroupName))
                return string.Empty;
            var locKey = "ExternalApps_Category_" + GroupName
                .Replace(" ", "").Replace("&", "").Replace(",", "").Replace("(", "").Replace(")", "");
            return _localizationService.GetStringOrDefault(locKey, GroupName);
        }
    }
}

using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.SoftwareApps.Models;

namespace Winhance.UI.Features.SoftwareApps.ViewModels;

/// <summary>
/// ViewModel for an individual app item in the software apps list.
/// </summary>
public partial class AppItemViewModel : ObservableObject, ISelectable, IDisposable
{
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
        _definition = definition;
        _localizationService = localizationService;
        _dispatcherService = dispatcherService;
        _themeService = themeService;

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
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(InstalledStatusText));
        OnPropertyChanged(nameof(ReinstallableStatusText));
        OnPropertyChanged(nameof(InstabilityWarningLabel));
        OnPropertyChanged(nameof(InstabilityWarningTooltip));
        OnPropertyChanged(nameof(InstalledStatusTooltip));
        OnPropertyChanged(nameof(ReinstallableStatusTooltip));
        OnPropertyChanged(nameof(CategoryDisplayName));
    }

    private void OnThemeChanged(object? sender, WinhanceTheme theme)
    {
        _dispatcherService.RunOnUIThread(() => OnPropertyChanged(nameof(IconSource)));
    }

    public ItemDefinition Definition => _definition;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public string Name => Definition.Name;

    public string Description => Definition.Description;
    public string GroupName => Definition.GroupName ?? string.Empty;
    public string Id => Definition.Id;

    public bool IsInstalled
    {
        get => Definition.IsInstalled;
        set
        {
            if (Definition.IsInstalled != value)
            {
                Definition.IsInstalled = value;
                _dispatcherService.RunOnUIThread(() =>
                {
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(InstalledStatusText));
                    OnPropertyChanged(nameof(InstalledStatusTooltip));
                });
            }
        }
    }

    public bool CanBeReinstalled => Definition.CanBeReinstalled;

    public string InstalledStatusText => _localizationService.GetString(
        IsInstalled ? "Status_Installed" : "Status_NotInstalled");

    public string ReinstallableStatusText => _localizationService.GetString(
        CanBeReinstalled ? "Status_CanReinstall" : "Status_CannotReinstall");

    private BitmapImage? _iconSource;
    private string? _iconSourcePath;

    /// <summary>
    /// Lazily-constructed BitmapImage from Definition.IconPath. Cached by path
    /// value so a change to IconPath produces a fresh BitmapImage on next read.
    /// Returns null when no icon has been resolved.
    ///
    /// IconPath is mutated by IAppIconResolver *after* this ViewModel is bound
    /// to the UI, so callers that change Definition.IconPath must invoke
    /// NotifyIconChanged() to refresh the bound Image and FontIcon.
    /// </summary>
    public BitmapImage? IconSource
    {
        get
        {
            var basePath = Definition.IconPath;
            if (string.IsNullOrEmpty(basePath))
            {
                _iconSource = null;
                _iconSourcePath = null;
                return null;
            }

            var resolvedPath = ResolveThemeAwarePath(basePath);

            if (_iconSource is not null && _iconSourcePath == resolvedPath)
                return _iconSource;

            var bmp = new BitmapImage { DecodePixelWidth = 64 };
            bmp.UriSource = new Uri(resolvedPath);
            _iconSource = bmp;
            _iconSourcePath = resolvedPath;
            return _iconSource;
        }
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

    /// <summary>True when a real app icon is available; false → render fallback glyph.</summary>
    public bool HasIcon => !string.IsNullOrEmpty(Definition.IconPath);

    /// <summary>
    /// True when the row should render the AppX-style fallback icon (no real
    /// icon resolved, and the entry is an AppX package or doesn't fit the
    /// Capability/Optional Feature buckets). Used as visibility for an
    /// app-shaped icon element in XAML.
    /// </summary>
    public bool IsAppXFallback =>
        !HasIcon &&
        string.IsNullOrEmpty(Definition.CapabilityName) &&
        string.IsNullOrEmpty(Definition.OptionalFeatureName);

    /// <summary>
    /// True when the row should render the Capability fallback (no real icon,
    /// definition has a CapabilityName).
    /// </summary>
    public bool IsCapabilityFallback =>
        !HasIcon && !string.IsNullOrEmpty(Definition.CapabilityName);

    /// <summary>
    /// True when the row should render the Optional Feature fallback
    /// (no real icon, definition has an OptionalFeatureName).
    /// </summary>
    public bool IsOptionalFeatureFallback =>
        !HasIcon && !string.IsNullOrEmpty(Definition.OptionalFeatureName);

    /// <summary>
    /// Raises PropertyChanged for IconSource and HasIcon. Call this after mutating
    /// Definition.IconPath (e.g. after IAppIconResolver.ResolveBatchAsync returns)
    /// so the bound Image/FontIcon refresh.
    /// </summary>
    public void NotifyIconChanged()
    {
        _dispatcherService.RunOnUIThread(() =>
        {
            OnPropertyChanged(nameof(IconSource));
            OnPropertyChanged(nameof(HasIcon));
            OnPropertyChanged(nameof(IsAppXFallback));
            OnPropertyChanged(nameof(IsCapabilityFallback));
            OnPropertyChanged(nameof(IsOptionalFeatureFallback));
        });
    }

    public string ItemTypeDescription
    {
        get
        {
            if (!string.IsNullOrEmpty(Definition.CapabilityName))
                return "Legacy Capability";

            if (!string.IsNullOrEmpty(Definition.OptionalFeatureName))
                return "Optional Feature";

            if (Definition.AppxPackageName?.Length > 0)
                return "AppX Package";

            return string.Empty;
        }
    }

    public string? WebsiteUrl => Definition.WebsiteUrl;

    /// <summary>True when the app has a non-empty Description; drives Card-view visibility.</summary>
    public bool HasDescription => !string.IsNullOrEmpty(Definition.Description);

    /// <summary>True when the item is marked as carrying an uninstall risk; drives the Card-view "Warning" pill.</summary>
    public bool HasInstabilityWarning => Definition.HasInstabilityWarning;

    /// <summary>Localized label for the Card-view "Warning" pill.</summary>
    public string InstabilityWarningLabel => _localizationService.GetString("Card_Pill_Warning");

    /// <summary>Localized generic instability message shown as the Warning pill's tooltip.</summary>
    public string InstabilityWarningTooltip => _localizationService.GetString("Card_Pill_InstabilityWarning_Tooltip");

    /// <summary>True when the item cannot be reinstalled; drives the "Cannot reinstall" chip in Card view.</summary>
    public bool ShowNonReinstallableChip => !Definition.CanBeReinstalled;

    /// <summary>
    /// Description surfaced as a hover tooltip on the Compact-view icon/name. Null (not empty)
    /// when there is no description, so no empty tooltip popup appears.
    /// </summary>
    public string? DescriptionTooltip => HasDescription ? Description : null;

    /// <summary>
    /// Localized, state-aware tooltip for the install-status badge/icon (installed checkmark or
    /// not-installed cross). Shown on every install-status indicator across Card, Table and
    /// Compact views. Re-raised by the <see cref="IsInstalled"/> setter.
    /// </summary>
    public string InstalledStatusTooltip => _localizationService.GetString(
        IsInstalled ? "Card_Pill_Installed_Tooltip" : "Card_Pill_NotInstalled_Tooltip");

    /// <summary>
    /// Localized, state-aware tooltip for the reinstallability badge/icon. When the item can be
    /// reinstalled it explains that removal is reversible; when it can't, it warns that removal is
    /// permanent. Shown on the reinstallable icon, the "Permanent" flag/badge, across all views.
    /// </summary>
    public string ReinstallableStatusTooltip => _localizationService.GetString(
        CanBeReinstalled ? "Card_Pill_Reinstallable_Tooltip" : "Card_Pill_NonReinstallable_Tooltip");

    /// <summary>
    /// Localized category/group display name for External Apps, used by the Group column in the
    /// table view. Mirrors the lookup in <c>ExternalAppsViewModel.RebuildCategories</c>: the raw
    /// <see cref="GroupName"/> maps to an <c>ExternalApps_Category_*</c> key (spaces and the
    /// characters &amp; , ( ) stripped). Falls back to the raw GroupName when no key resolves.
    /// </summary>
    public string CategoryDisplayName
    {
        get
        {
            if (string.IsNullOrEmpty(GroupName))
                return string.Empty;
            var locKey = "ExternalApps_Category_" + GroupName
                .Replace(" ", "").Replace("&", "").Replace(",", "").Replace("(", "").Replace(")", "");
            var displayName = _localizationService.GetString(locKey);
            return string.IsNullOrEmpty(displayName) ? GroupName : displayName;
        }
    }
}

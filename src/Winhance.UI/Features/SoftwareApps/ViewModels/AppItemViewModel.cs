using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.SoftwareApps.Models;

namespace Winhance.UI.Features.SoftwareApps.ViewModels;

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
        GC.SuppressFinalize(this);
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

    // Cached by path value so a change to IconPath produces a fresh BitmapImage on next read; IconPath is mutated
    // by IAppIconResolver AFTER binding, so mutators call NotifyIconChanged(). Separate from IconSource because path
    // SELECTION is pure logic worth testing on its own - constructing a BitmapImage needs a WinRT/XAML application context.
    public string? ResolvedIconPath
    {
        get
        {
            var basePath = Definition.IconPath;
            return string.IsNullOrEmpty(basePath) ? null : ResolveThemeAwarePath(basePath);
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

    public bool HasIcon => !string.IsNullOrEmpty(Definition.IconPath);

    public bool IsAppXFallback =>
        !HasIcon &&
        string.IsNullOrEmpty(Definition.CapabilityName) &&
        string.IsNullOrEmpty(Definition.OptionalFeatureName);

    public bool IsCapabilityFallback =>
        !HasIcon && !string.IsNullOrEmpty(Definition.CapabilityName);

    public bool IsOptionalFeatureFallback =>
        !HasIcon && !string.IsNullOrEmpty(Definition.OptionalFeatureName);

    // Call after mutating Definition.IconPath (e.g. after ResolveBatchAsync) so the bound Image/FontIcon refresh.
    public void NotifyIconChanged()
    {
        _dispatcherService.RunOnUIThread(() =>
        {
            OnPropertyChanged(nameof(IconSource));
            OnPropertyChanged(nameof(ResolvedIconPath));
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

    public bool HasDescription => !string.IsNullOrEmpty(Definition.Description);

    public bool HasInstabilityWarning => Definition.HasInstabilityWarning;

    public string InstabilityWarningLabel => _localizationService.GetString("Card_Pill_Warning");

    public string InstabilityWarningTooltip => _localizationService.GetString("Card_Pill_InstabilityWarning_Tooltip");

    public bool ShowNonReinstallableChip => !Definition.CanBeReinstalled;

    // Null (not empty) when there is no description, so no empty tooltip popup appears.
    public string? DescriptionTooltip => HasDescription ? Description : null;

    public string InstalledStatusTooltip => _localizationService.GetString(
        IsInstalled ? "Card_Pill_Installed_Tooltip" : "Card_Pill_NotInstalled_Tooltip");

    public string ReinstallableStatusTooltip => _localizationService.GetString(
        CanBeReinstalled ? "Card_Pill_Reinstallable_Tooltip" : "Card_Pill_NonReinstallable_Tooltip");

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
            var displayName = _localizationService.GetString(locKey);
            return string.IsNullOrEmpty(displayName) ? GroupName : displayName;
        }
    }
}

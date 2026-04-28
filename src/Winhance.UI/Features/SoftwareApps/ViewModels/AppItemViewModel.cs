using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media.Imaging;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.SoftwareApps.Constants;
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
    private bool _disposed;

    public AppItemViewModel(
        ItemDefinition definition,
        ILocalizationService localizationService,
        IDispatcherService dispatcherService)
    {
        _definition = definition;
        _localizationService = localizationService;
        _dispatcherService = dispatcherService;

        _localizationService.LanguageChanged += OnLanguageChanged;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _localizationService.LanguageChanged -= OnLanguageChanged;
            _disposed = true;
        }
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(InstalledStatusText));
        OnPropertyChanged(nameof(ReinstallableStatusText));
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
            var currentPath = Definition.IconPath;
            if (string.IsNullOrEmpty(currentPath))
            {
                _iconSource = null;
                _iconSourcePath = null;
                return null;
            }
            if (_iconSource is not null && _iconSourcePath == currentPath)
                return _iconSource;

            var bmp = new BitmapImage { DecodePixelWidth = 80 };
            bmp.UriSource = new Uri(currentPath);
            _iconSource = bmp;
            _iconSourcePath = currentPath;
            return _iconSource;
        }
    }

    /// <summary>True when a real app icon is available; false → render fallback glyph.</summary>
    public bool HasIcon => !string.IsNullOrEmpty(Definition.IconPath);

    /// <summary>
    /// Segoe Fluent Icons codepoint shown in a FontIcon when HasIcon is false.
    /// Categorised by the kind of definition (AppX vs Capability vs Optional Feature).
    /// </summary>
    public string FallbackGlyph => Definition switch
    {
        { CapabilityName: not null and not "" } => FallbackGlyphs.Capability,
        { OptionalFeatureName: not null and not "" } => FallbackGlyphs.OptionalFeature,
        _ => FallbackGlyphs.Package,
    };

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
}

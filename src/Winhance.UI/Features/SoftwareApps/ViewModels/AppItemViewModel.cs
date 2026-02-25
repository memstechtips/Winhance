using CommunityToolkit.Mvvm.ComponentModel;
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

    public string ItemTypeDescription
    {
        get
        {
            if (!string.IsNullOrEmpty(Definition.CapabilityName))
                return "Legacy Capability";

            if (!string.IsNullOrEmpty(Definition.OptionalFeatureName))
                return "Optional Feature";

            if (!string.IsNullOrEmpty(Definition.AppxPackageName))
                return "AppX Package";

            return string.Empty;
        }
    }

    public string? WebsiteUrl => Definition.WebsiteUrl;
}

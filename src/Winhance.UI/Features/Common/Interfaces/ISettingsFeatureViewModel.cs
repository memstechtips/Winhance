using System.Collections.ObjectModel;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Common.Interfaces;

/// <summary>
/// Interface for feature ViewModels that display settings.
/// </summary>
public interface ISettingsFeatureViewModel : IDisposable
{
    /// <summary>
    /// Module identifier for this feature.
    /// </summary>
    string ModuleId { get; }

    /// <summary>
    /// Display name for this feature.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Collection of settings in this feature.
    /// </summary>
    ObservableCollection<SettingItemViewModel> Settings { get; }

    /// <summary>
    /// Indicates whether this feature has any visible settings (after search filtering).
    /// </summary>
    bool HasVisibleSettings { get; }

    /// <summary>
    /// Indicates whether this feature section is expanded.
    /// </summary>
    bool IsExpanded { get; set; }

    /// <summary>
    /// Indicates whether settings are currently loading.
    /// </summary>
    bool IsLoading { get; }

    /// <summary>
    /// Number of settings in this feature.
    /// </summary>
    int SettingsCount { get; }

    /// <summary>
    /// Loads all settings for this feature.
    /// </summary>
    Task LoadSettingsAsync();

    /// <summary>
    /// Refreshes all settings, reloading their current values.
    /// </summary>
    Task RefreshSettingsAsync();

    /// <summary>
    /// Applies a search filter to the settings.
    /// </summary>
    void ApplySearchFilter(string searchText);

    /// <summary>
    /// Handles a domain-specific setting context change.
    /// </summary>
    Task<bool> HandleDomainContextSettingAsync(SettingDefinition setting, object? value, bool additionalContext = false);
}

/// <summary>
/// Event args for when a feature's visibility changes due to search.
/// </summary>
public class FeatureVisibilityChangedEventArgs : EventArgs
{
    public string FeatureId { get; }
    public bool IsVisible { get; }
    public string SearchText { get; }

    public FeatureVisibilityChangedEventArgs(string featureId, bool isVisible, string searchText)
    {
        FeatureId = featureId;
        IsVisible = isVisible;
        SearchText = searchText;
    }
}

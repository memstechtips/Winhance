using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Winhance.WPF.Features.Common.Models;

namespace Winhance.WPF.Features.Common.Interfaces
{
    /// <summary>
    /// Represents a ViewModel for a specific feature in the WPF layer.
    /// This interface contains UI-specific concerns and properties.
    /// </summary>
    public interface IFeatureViewModel
    {
        /// <summary>
        /// Gets the unique identifier for this feature module.
        /// </summary>
        string ModuleId { get; }

        /// <summary>
        /// Gets the display name for this feature module.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Gets the collection of UI settings managed by this feature.
        /// </summary>
        ObservableCollection<SettingUIItem> Settings { get; }

        /// <summary>
        /// Gets a value indicating whether this feature has any visible settings.
        /// </summary>
        bool HasVisibleSettings { get; }

        /// <summary>
        /// Gets or sets the search text for filtering settings.
        /// </summary>
        string SearchText { get; set; }

        /// <summary>
        /// Gets a value indicating whether this feature is currently loading.
        /// </summary>
        bool IsLoading { get; }

        /// <summary>
        /// Gets the total number of settings in this feature.
        /// </summary>
        int SettingsCount { get; }

        /// <summary>
        /// Gets the category this feature belongs to.
        /// </summary>
        string Category { get; }

        /// <summary>
        /// Gets an optional description of what this feature does.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Gets the sort order for this feature in the UI.
        /// </summary>
        int SortOrder { get; }

        /// <summary>
        /// Gets the command to load settings for this feature.
        /// </summary>
        ICommand LoadSettingsCommand { get; }

        /// <summary>
        /// Loads the settings for this feature asynchronously.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task LoadSettingsAsync();

        /// <summary>
        /// Refreshes the settings for this feature asynchronously.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RefreshSettingsAsync();

        /// <summary>
        /// Clears all settings and resets the feature state.
        /// </summary>
        void ClearSettings();
    }
}

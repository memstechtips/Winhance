using System.ComponentModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.ViewModels;

namespace Winhance.WPF.Features.SoftwareApps.ViewModels
{
    /// <summary>
    /// Base view model with common functionality for app view modes
    /// </summary>
    public partial class AppViewModeManager : ObservableObject
    {
        /// <summary>
        /// Flag to indicate whether the view model is in table view mode
        /// </summary>
        [ObservableProperty]
        private bool _isTableViewMode = false;

        /// <summary>
        /// Property to convert view mode to visibility for grid view
        /// </summary>
        public Visibility GridViewVisibility => IsTableViewMode ? Visibility.Collapsed : Visibility.Visible;

        /// <summary>
        /// Property to convert view mode to visibility for table view
        /// </summary>
        public Visibility TableViewVisibility => IsTableViewMode ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// Delegate for view mode changed event
        /// </summary>
        /// <param name="isTableViewMode">True if table view mode, false if list view mode</param>
        public delegate void ViewModeChangedEventHandler(bool isTableViewMode);
        
        /// <summary>
        /// Event raised when view mode changes
        /// </summary>
        public event ViewModeChangedEventHandler ViewModeChanged;

        /// <summary>
        /// Constructor for the view mode manager
        /// </summary>
        public AppViewModeManager()
        {
            // Register for property changed notifications to update visibility when view mode changes
            PropertyChanged += OnPropertyChanged;
        }

        /// <summary>
        /// Handles property changes in the view mode manager
        /// </summary>
        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IsTableViewMode))
            {
                OnPropertyChanged(nameof(GridViewVisibility));
                OnPropertyChanged(nameof(TableViewVisibility));

                // Raise event to notify listeners of view mode change
                ViewModeChanged?.Invoke(IsTableViewMode);
            }
        }
    }
}

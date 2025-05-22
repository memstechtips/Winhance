using System;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Service for navigating between views in the application.
    /// </summary>
    public interface INavigationService
    {
        /// <summary>
        /// Navigates to a view.
        /// </summary>
        /// <param name="viewName">The name of the view to navigate to.</param>
        /// <returns>True if navigation was successful; otherwise, false.</returns>
        bool NavigateTo(string viewName);

        /// <summary>
        /// Navigates to a view with parameters.
        /// </summary>
        /// <param name="viewName">The name of the view to navigate to.</param>
        /// <param name="parameter">The navigation parameter.</param>
        /// <returns>True if navigation was successful; otherwise, false.</returns>
        bool NavigateTo(string viewName, object parameter);

        /// <summary>
        /// Navigates back to the previous view.
        /// </summary>
        /// <returns>True if navigation was successful; otherwise, false.</returns>
        bool NavigateBack();

        /// <summary>
        /// Gets the current view name.
        /// </summary>
        string CurrentView { get; }

        /// <summary>
        /// Event raised when navigation occurs.
        /// </summary>
        event EventHandler<NavigationEventArgs>? Navigated;

        /// <summary>
        /// Event raised before navigation occurs.
        /// </summary>
        event EventHandler<NavigationEventArgs>? Navigating;

        /// <summary>
        /// Event raised when navigation fails.
        /// </summary>
        event EventHandler<NavigationEventArgs>? NavigationFailed;
    }

    /// <summary>
    /// Event arguments for navigation events.
    /// </summary>
    public class NavigationEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the source view name.
        /// </summary>
        public string SourceView { get; }

        /// <summary>
        /// Gets the target view name.
        /// </summary>
        public string TargetView { get; }

        /// <summary>
        /// Gets the navigation route (same as TargetView).
        /// </summary>
        public string Route => TargetView;

        /// <summary>
        /// Gets the view model type associated with the navigation.
        /// </summary>
        public Type? ViewModelType => Parameter?.GetType();

        /// <summary>
        /// Gets the navigation parameter.
        /// </summary>
        public object? Parameter { get; }

        /// <summary>
        /// Gets a value indicating whether the navigation can be canceled.
        /// </summary>
        public bool CanCancel { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the navigation should be canceled.
        /// </summary>
        public bool Cancel { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="NavigationEventArgs"/> class.
        /// </summary>
        /// <param name="sourceView">The source view name.</param>
        /// <param name="targetView">The target view name.</param>
        /// <param name="parameter">The navigation parameter.</param>
        /// <param name="canCancel">Whether the navigation can be canceled.</param>
        public NavigationEventArgs(
            string sourceView,
            string targetView,
            object? parameter = null,
            bool canCancel = false
        )
        {
            SourceView = sourceView;
            TargetView = targetView;
            Parameter = parameter;
            CanCancel = canCancel;
        }
    }
}

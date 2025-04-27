using System.Windows;

namespace Winhance.WPF.Features.Common.Interfaces
{
    /// <summary>
    /// Interface for locating view models in the application.
    /// </summary>
    public interface IViewModelLocator
    {
        /// <summary>
        /// Finds a view model of the specified type in the application.
        /// </summary>
        /// <typeparam name="T">The type of view model to find.</typeparam>
        /// <returns>The view model if found, otherwise null.</returns>
        T? FindViewModel<T>() where T : class;

        /// <summary>
        /// Finds a view model of the specified type in the specified window.
        /// </summary>
        /// <typeparam name="T">The type of view model to find.</typeparam>
        /// <param name="window">The window to search in.</param>
        /// <returns>The view model if found, otherwise null.</returns>
        T? FindViewModelInWindow<T>(Window window) where T : class;

        /// <summary>
        /// Finds a view model of the specified type in the visual tree starting from the specified element.
        /// </summary>
        /// <typeparam name="T">The type of view model to find.</typeparam>
        /// <param name="element">The starting element.</param>
        /// <returns>The view model if found, otherwise null.</returns>
        T? FindViewModelInVisualTree<T>(DependencyObject element) where T : class;

        /// <summary>
        /// Finds a view model by its name.
        /// </summary>
        /// <param name="viewModelName">The name of the view model to find.</param>
        /// <returns>The view model if found, otherwise null.</returns>
        object? FindViewModelByName(string viewModelName);

        /// <summary>
        /// Gets a property from a view model.
        /// </summary>
        /// <typeparam name="T">The type of the property to get.</typeparam>
        /// <param name="viewModel">The view model to get the property from.</param>
        /// <param name="propertyName">The name of the property to get.</param>
        /// <returns>The property value if found, otherwise null.</returns>
        T? GetPropertyFromViewModel<T>(object viewModel, string propertyName) where T : class;
    }
}

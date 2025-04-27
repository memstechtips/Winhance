using System;
using System.Linq;
using System.Windows;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Interfaces;

namespace Winhance.WPF.Features.Common.Services
{
    /// <summary>
    /// Service for locating view models in the application.
    /// </summary>
    public class ViewModelLocator : IViewModelLocator
    {
        private readonly ILogService _logService;

        /// <summary>
        /// Initializes a new instance of the <see cref="ViewModelLocator"/> class.
        /// </summary>
        /// <param name="logService">The log service.</param>
        public ViewModelLocator(ILogService logService)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        /// <summary>
        /// Finds a view model of the specified type in the application.
        /// </summary>
        /// <typeparam name="T">The type of view model to find.</typeparam>
        /// <returns>The view model if found, otherwise null.</returns>
        public T? FindViewModel<T>() where T : class
        {
            try
            {
                var app = Application.Current;
                if (app == null) return null;

                // Try to find the view model in the main window's DataContext hierarchy
                var mainWindow = app.MainWindow;
                if (mainWindow != null)
                {
                    var mainViewModel = FindViewModelInWindow<T>(mainWindow);
                    if (mainViewModel != null)
                    {
                        _logService.Log(LogLevel.Info, $"Found {typeof(T).Name} in main window's DataContext");
                        return mainViewModel;
                    }
                }

                // If we can't find it in the main window, try to find it in any open window
                foreach (Window window in app.Windows)
                {
                    var viewModel = FindViewModelInWindow<T>(window);
                    if (viewModel != null)
                    {
                        _logService.Log(LogLevel.Info, $"Found {typeof(T).Name} in window: {window.Title}");
                        return viewModel;
                    }
                }

                _logService.Log(LogLevel.Warning, $"Could not find {typeof(T).Name} in any window");
                return null;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error finding view model: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Finds a view model of the specified type in the specified window.
        /// </summary>
        /// <typeparam name="T">The type of view model to find.</typeparam>
        /// <param name="window">The window to search in.</param>
        /// <returns>The view model if found, otherwise null.</returns>
        public T? FindViewModelInWindow<T>(Window window) where T : class
        {
            try
            {
                // Check if the window's DataContext is or contains the view model
                if (window.DataContext is T vm)
                {
                    return vm;
                }

                // Check if the window's DataContext has a property that is the view model
                if (window.DataContext != null)
                {
                    var type = window.DataContext.GetType();
                    var properties = type.GetProperties();

                    foreach (var property in properties)
                    {
                        if (property.PropertyType == typeof(T))
                        {
                            return property.GetValue(window.DataContext) as T;
                        }
                    }
                }

                // If not found in the DataContext, search the visual tree
                return FindViewModelInVisualTree<T>(window);
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error finding view model in window: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Finds a view model of the specified type in the visual tree starting from the specified element.
        /// </summary>
        /// <typeparam name="T">The type of view model to find.</typeparam>
        /// <param name="element">The starting element.</param>
        /// <returns>The view model if found, otherwise null.</returns>
        public T? FindViewModelInVisualTree<T>(DependencyObject element) where T : class
        {
            try
            {
                // Check if the element's DataContext is the view model
                if (element is FrameworkElement fe && fe.DataContext is T vm)
                {
                    return vm;
                }

                // Recursively check children
                int childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(element);
                for (int i = 0; i < childCount; i++)
                {
                    var child = System.Windows.Media.VisualTreeHelper.GetChild(element, i);
                    var result = FindViewModelInVisualTree<T>(child);
                    if (result != null)
                    {
                        return result;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error finding view model in visual tree: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Finds a view model by its name.
        /// </summary>
        /// <param name="viewModelName">The name of the view model to find.</param>
        /// <returns>The view model if found, otherwise null.</returns>
        public object? FindViewModelByName(string viewModelName)
        {
            try
            {
                var app = Application.Current;
                if (app == null) return null;

                // Try to find the view model in any window's DataContext
                foreach (Window window in app.Windows)
                {
                    if (window.DataContext != null)
                    {
                        var type = window.DataContext.GetType();
                        if (type.Name == viewModelName || type.Name == $"{viewModelName}ViewModel")
                        {
                            return window.DataContext;
                        }

                        // Check if the DataContext has a property that is the view model
                        var properties = type.GetProperties();
                        foreach (var property in properties)
                        {
                            if (property.Name == viewModelName || property.PropertyType.Name == viewModelName ||
                                property.Name == $"{viewModelName}ViewModel" || property.PropertyType.Name == $"{viewModelName}ViewModel")
                            {
                                return property.GetValue(window.DataContext);
                            }
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error finding view model by name: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets a property from a view model.
        /// </summary>
        /// <typeparam name="T">The type of the property to get.</typeparam>
        /// <param name="viewModel">The view model to get the property from.</param>
        /// <param name="propertyName">The name of the property to get.</param>
        /// <returns>The property value if found, otherwise null.</returns>
        public T? GetPropertyFromViewModel<T>(object viewModel, string propertyName) where T : class
        {
            try
            {
                var type = viewModel.GetType();
                var property = type.GetProperty(propertyName);
                if (property != null)
                {
                    return property.GetValue(viewModel) as T;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error getting property from view model: {ex.Message}");
                return null;
            }
        }
    }
}

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.WPF.Features.Common.Services.Configuration
{
    /// <summary>
    /// Service for refreshing view models after configuration changes.
    /// </summary>
    public class ViewModelRefresher : IViewModelRefresher
    {
        private readonly ILogService _logService;

        /// <summary>
        /// Initializes a new instance of the <see cref="ViewModelRefresher"/> class.
        /// </summary>
        /// <param name="logService">The log service.</param>
        public ViewModelRefresher(ILogService logService)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        /// <summary>
        /// Refreshes a view model after configuration changes.
        /// </summary>
        /// <param name="viewModel">The view model to refresh.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task RefreshViewModelAsync(object viewModel)
        {
            try
            {
                // Special handling for OptimizeViewModel
                if (viewModel is Winhance.WPF.Features.Optimize.ViewModels.OptimizeViewModel optimizeViewModel)
                {
                    _logService.Log(LogLevel.Debug, "Refreshing OptimizeViewModel and its child view models");
                    
                    // Refresh child view models first
                    var childViewModels = new object[]
                    {
                        optimizeViewModel.GamingandPerformanceOptimizationsViewModel,
                        optimizeViewModel.PrivacyOptimizationsViewModel,
                        optimizeViewModel.UpdateOptimizationsViewModel,
                        optimizeViewModel.PowerSettingsViewModel,
                        optimizeViewModel.WindowsSecuritySettingsViewModel,
                        optimizeViewModel.ExplorerOptimizationsViewModel,
                        optimizeViewModel.NotificationOptimizationsViewModel,
                        optimizeViewModel.SoundOptimizationsViewModel
                    };
                    
                    foreach (var childViewModel in childViewModels)
                    {
                        if (childViewModel != null)
                        {
                            // Try to refresh each child view model
                            await RefreshChildViewModelAsync(childViewModel);
                        }
                    }
                    
                    // Then reload the main view model's items to reflect changes in child view models
                    await optimizeViewModel.LoadItemsAsync();
                    
                    // Notify UI of changes using a safer approach
                    try
                    {
                        // Try to find a method that takes a string parameter
                        var methods = optimizeViewModel.GetType().GetMethods(
                            System.Reflection.BindingFlags.Public |
                            System.Reflection.BindingFlags.NonPublic |
                            System.Reflection.BindingFlags.Instance)
                            .Where(m => (m.Name == "RaisePropertyChanged" || m.Name == "OnPropertyChanged") &&
                                       m.GetParameters().Length == 1 &&
                                       m.GetParameters()[0].ParameterType == typeof(string))
                            .ToList();
                        
                        if (methods.Any())
                        {
                            var method = methods.First();
                            // Refresh key properties
                            // Execute property change notifications on the UI thread
                            ExecuteOnUIThread(() => {
                                method.Invoke(optimizeViewModel, new object[] { "Items" });
                                method.Invoke(optimizeViewModel, new object[] { "IsInitialized" });
                                method.Invoke(optimizeViewModel, new object[] { "IsLoading" });
                                _logService.Log(LogLevel.Debug, $"Refreshed OptimizeViewModel properties using {method.Name} on UI thread");
                            });
                        }
                        else
                        {
                            // If no suitable method is found, try using RefreshCommand
                            var refreshCommand = optimizeViewModel.GetType().GetProperty("RefreshCommand")?.GetValue(optimizeViewModel) as System.Windows.Input.ICommand;
                            if (refreshCommand != null && refreshCommand.CanExecute(null))
                            {
                                ExecuteOnUIThread(() => {
                                    refreshCommand.Execute(null);
                                    _logService.Log(LogLevel.Debug, "Refreshed OptimizeViewModel using RefreshCommand on UI thread");
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logService.Log(LogLevel.Debug, $"Error refreshing OptimizeViewModel: {ex.Message}");
                    }
                    
                    return;
                }
                
                // Standard refresh logic for other view models
                // Try multiple refresh methods in order of preference
                
                // 1. First try RefreshCommand if available
                var refreshCommandProperty = viewModel.GetType().GetProperty("RefreshCommand");
                if (refreshCommandProperty != null)
                {
                    var refreshCommand = refreshCommandProperty.GetValue(viewModel) as System.Windows.Input.ICommand;
                    if (refreshCommand != null && refreshCommand.CanExecute(null))
                    {
                        ExecuteOnUIThread(() => {
                            refreshCommand.Execute(null);
                            _logService.Log(LogLevel.Debug, $"Refreshed {viewModel.GetType().Name} using RefreshCommand on UI thread");
                        });
                        return;
                    }
                }
                
                // 2. Try RaisePropertyChanged for the Items property if the view model implements INotifyPropertyChanged
                if (viewModel is System.ComponentModel.INotifyPropertyChanged)
                {
                    try
                    {
                        // Use a safer approach to find property changed methods
                        var methods = viewModel.GetType().GetMethods(
                            System.Reflection.BindingFlags.Public |
                            System.Reflection.BindingFlags.NonPublic |
                            System.Reflection.BindingFlags.Instance)
                            .Where(m => (m.Name == "RaisePropertyChanged" || m.Name == "OnPropertyChanged") &&
                                       m.GetParameters().Length == 1 &&
                                       m.GetParameters()[0].ParameterType == typeof(string))
                            .ToList();
                        
                        if (methods.Any())
                        {
                            var method = methods.First();
                            ExecuteOnUIThread(() => {
                                method.Invoke(viewModel, new object[] { "Items" });
                                _logService.Log(LogLevel.Debug, $"Refreshed {viewModel.GetType().Name} using {method.Name} on UI thread");
                            });
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logService.Log(LogLevel.Debug, $"Error finding property changed method: {ex.Message}");
                        // Continue with other refresh methods
                    }
                }
                
                // 3. Try LoadItemsAsync method
                var loadItemsMethod = viewModel.GetType().GetMethod("LoadItemsAsync");
                if (loadItemsMethod != null)
                {
                    await (Task)loadItemsMethod.Invoke(viewModel, null);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error refreshing UI: {ex.Message}");
            }
        }

        /// <summary>
        /// Refreshes a child view model after configuration changes.
        /// </summary>
        /// <param name="childViewModel">The child view model to refresh.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task RefreshChildViewModelAsync(object childViewModel)
        {
            try
            {
                // Try to find and call CheckSettingStatusesAsync method
                var checkStatusMethod = childViewModel.GetType().GetMethod("CheckSettingStatusesAsync");
                if (checkStatusMethod != null)
                {
                    await (Task)checkStatusMethod.Invoke(childViewModel, null);
                    _logService.Log(LogLevel.Debug, $"Called CheckSettingStatusesAsync on {childViewModel.GetType().Name}");
                }
                
                // Try to notify property changes using a safer approach
                if (childViewModel is System.ComponentModel.INotifyPropertyChanged)
                {
                    try
                    {
                        // Use a safer approach to find the right PropertyChanged event
                        var propertyChangedEvent = childViewModel.GetType().GetEvent("PropertyChanged");
                        if (propertyChangedEvent != null)
                        {
                            // Try to find a method that raises the PropertyChanged event
                            // Look for a method that takes a string parameter first
                            var methods = childViewModel.GetType().GetMethods(
                                System.Reflection.BindingFlags.Public |
                                System.Reflection.BindingFlags.NonPublic |
                                System.Reflection.BindingFlags.Instance)
                                .Where(m => (m.Name == "RaisePropertyChanged" || m.Name == "OnPropertyChanged") &&
                                           m.GetParameters().Length == 1 &&
                                           m.GetParameters()[0].ParameterType == typeof(string))
                                .ToList();
                            
                            if (methods.Any())
                            {
                                var method = methods.First();
                                // Refresh Settings property and IsSelected property on UI thread
                                ExecuteOnUIThread(() => {
                                    method.Invoke(childViewModel, new object[] { "Settings" });
                                    method.Invoke(childViewModel, new object[] { "IsSelected" });
                                    _logService.Log(LogLevel.Debug, $"Refreshed properties on {childViewModel.GetType().Name} using {method.Name} on UI thread");
                                });
                            }
                            else
                            {
                                // If no method with string parameter is found, try to find a method that takes PropertyChangedEventArgs
                                var refreshCommand = childViewModel.GetType().GetProperty("RefreshCommand")?.GetValue(childViewModel) as System.Windows.Input.ICommand;
                                if (refreshCommand != null && refreshCommand.CanExecute(null))
                                {
                                    ExecuteOnUIThread(() => {
                                        refreshCommand.Execute(null);
                                        _logService.Log(LogLevel.Debug, $"Refreshed {childViewModel.GetType().Name} using RefreshCommand on UI thread");
                                    });
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logService.Log(LogLevel.Debug, $"Error refreshing child view model properties: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Debug, $"Error refreshing child view model: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Executes an action on the UI thread.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        private void ExecuteOnUIThread(Action action)
        {
            try
            {
                // Check if we have access to a dispatcher
                if (Application.Current != null && Application.Current.Dispatcher != null)
                {
                    // Check if we're already on the UI thread
                    if (Application.Current.Dispatcher.CheckAccess())
                    {
                        // We're on the UI thread, execute directly
                        action();
                    }
                    else
                    {
                        // We're not on the UI thread, invoke on the UI thread
                        Application.Current.Dispatcher.Invoke(action, DispatcherPriority.Background);
                    }
                }
                else
                {
                    // No dispatcher available, execute directly
                    action();
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error executing action on UI thread: {ex.Message}");
                _logService.Log(LogLevel.Debug, $"Exception details: {ex}");
                
                // Try to execute the action directly as a fallback
                try
                {
                    action();
                }
                catch (Exception innerEx)
                {
                    _logService.Log(LogLevel.Error, $"Error executing action directly: {innerEx.Message}");
                }
            }
        }
    }
}
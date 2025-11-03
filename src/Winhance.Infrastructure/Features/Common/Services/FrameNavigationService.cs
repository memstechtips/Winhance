using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.Infrastructure.Features.Common.Services
{
    public class FrameNavigationService
        : Winhance.Core.Features.Common.Interfaces.INavigationService
    {
        private readonly Stack<Type> _backStack = new();
        private readonly Stack<(Type ViewModelType, object Parameter)> _forwardStack = new();
        private readonly List<string> _navigationHistory = new();
        private readonly Dictionary<string, (Type ViewType, Type ViewModelType)> _viewMappings =
            new();
        private readonly IServiceProvider _serviceProvider;
        private readonly IParameterSerializer _parameterSerializer;
        private readonly ILogService _logService;
        private object _currentParameter;
        private string _currentRoute;
        private const int MaxHistorySize = 50;
        private ICommand _navigateCommand;

        public ICommand NavigateCommand =>
            _navigateCommand ??= new RelayCommand<string>(route => NavigateTo(route));
        public FrameNavigationService(
            IServiceProvider serviceProvider,
            IParameterSerializer parameterSerializer,
            ILogService logService)
        {
            _serviceProvider = serviceProvider;
            _parameterSerializer = parameterSerializer ?? throw new ArgumentNullException(nameof(parameterSerializer));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        public bool CanGoBack => _backStack.Count > 1;
        public IReadOnlyList<string> NavigationHistory => _navigationHistory.AsReadOnly();
        public string CurrentView => _currentRoute;
        
        public event EventHandler<Winhance.Core.Features.Common.Interfaces.NavigationEventArgs> Navigated;
        public event EventHandler<Winhance.Core.Features.Common.Interfaces.NavigationEventArgs> Navigating;
        public event EventHandler<Winhance.Core.Features.Common.Interfaces.NavigationEventArgs> NavigationFailed;

        public void Initialize() { }

        public void RegisterViewMapping(string route, Type viewType, Type viewModelType)
        {
            if (string.IsNullOrWhiteSpace(route))
                throw new ArgumentException("Route cannot be empty", nameof(route));

            _viewMappings[route] = (viewType, viewModelType);
        }

        public bool CanNavigateTo(string route) => _viewMappings.ContainsKey(route);

        public bool NavigateTo(string viewName)
        {
            try
            {
                Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    try
                    {
                        await PreloadAndNavigateToAsync(viewName);
                    }
                    catch (Exception ex)
                    {
                        var args = new Winhance.Core.Features.Common.Interfaces.NavigationEventArgs(
                            CurrentView,
                            viewName,
                            null,
                            false
                        );
                        NavigationFailed?.Invoke(this, args);
                    }
                });
                return true;
            }
            catch (Exception ex)
            {
                var args = new Winhance.Core.Features.Common.Interfaces.NavigationEventArgs(
                    CurrentView,
                    viewName,
                    null,
                    false
                );
                NavigationFailed?.Invoke(this, args);
                return false;
            }
        }

        public async Task<bool> PreloadAndNavigateToAsync(string route)
        {
            _logService?.Log(LogLevel.Info, $"[FrameNavigationService] PreloadAndNavigateToAsync called for: {route}");

            if (_currentRoute == route)
            {
                _logService?.Log(LogLevel.Info, $"[FrameNavigationService] Already on route: {route}, skipping navigation");
                return true; // Return true because we're already where we need to be
            }

            if (!_viewMappings.TryGetValue(route, out var mapping))
            {
                _logService?.Log(LogLevel.Info, $"[FrameNavigationService] No view mapping found for route: {route}");
                return false;
            }

            var navigatingArgs = new Winhance.Core.Features.Common.Interfaces.NavigationEventArgs(
                _currentRoute,
                route,
                null,
                false
            );
            Navigating?.Invoke(this, navigatingArgs);

            try
            {
                _logService?.Log(LogLevel.Info, $"[FrameNavigationService] Creating ViewModel of type: {mapping.ViewModelType.Name}");
                var viewModel = _serviceProvider.GetRequiredService(mapping.ViewModelType);
                _logService?.Log(LogLevel.Info, $"[FrameNavigationService] ViewModel created successfully: {viewModel.GetType().Name}");
                
                if (viewModel is IInitializableViewModel initializable)
                {
                    initializable.Initialize();
                }

                if (viewModel is IPreloadableViewModel preloadable)
                {
                    _logService?.Log(LogLevel.Info, $"[FrameNavigationService] Starting PreloadFeaturesAsync for: {viewModel.GetType().Name}");
                    await preloadable.PreloadFeaturesAsync();
                    _logService?.Log(LogLevel.Info, $"[FrameNavigationService] PreloadFeaturesAsync completed for: {viewModel.GetType().Name}");
                }
                else
                {
                    _logService?.Log(LogLevel.Info, $"[FrameNavigationService] ViewModel does not implement IPreloadableViewModel: {viewModel.GetType().Name}");
                }
                
                if (viewModel is IFeatureViewModel vm)
                {
                    _logService?.Log(LogLevel.Info, $"[FrameNavigationService] Calling OnNavigatedTo for: {viewModel.GetType().Name}");
                    vm.OnNavigatedTo(null);
                }

                _logService?.Log(LogLevel.Info, $"[FrameNavigationService] Starting NavigateInternalAsync with ViewModel");
                await NavigateInternalAsync(mapping.ViewType, mapping.ViewModelType, null, viewModel, CancellationToken.None);
                _logService?.Log(LogLevel.Info, $"[FrameNavigationService] NavigateInternalAsync completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logService?.Log(LogLevel.Error, $"[FrameNavigationService] PreloadAndNavigateToAsync failed: {ex.Message}");
                return false;
            }
        }

        public bool NavigateTo(string viewName, object parameter)
        {
            try
            {
                Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    try
                    {
                        await PreloadAndNavigateToAsync(viewName);
                    }
                    catch (Exception ex)
                    {
                        var args = new Winhance.Core.Features.Common.Interfaces.NavigationEventArgs(
                            CurrentView,
                            viewName,
                            parameter,
                            false
                        );
                        NavigationFailed?.Invoke(this, args);
                    }
                });
                return true;
            }
            catch (Exception ex)
            {
                var args = new Winhance.Core.Features.Common.Interfaces.NavigationEventArgs(
                    CurrentView,
                    viewName,
                    parameter,
                    false
                );
                NavigationFailed?.Invoke(this, args);
                return false;
            }
        }

        public async Task NavigateToAsync(string viewName)
        {
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                await PreloadAndNavigateToAsync(viewName);
            }, System.Windows.Threading.DispatcherPriority.Loaded);

            await Application.Current.Dispatcher.InvokeAsync(() => { },
                System.Windows.Threading.DispatcherPriority.Loaded);
        }

        public async Task NavigateToAsync(string viewName, object parameter)
        {
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                await PreloadAndNavigateToAsync(viewName);
            }, System.Windows.Threading.DispatcherPriority.Loaded);

            await Application.Current.Dispatcher.InvokeAsync(() => { },
                System.Windows.Threading.DispatcherPriority.Loaded);
        }

        public bool NavigateBack()
        {
            try
            {
                GoBackAsync().GetAwaiter().GetResult();
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }



        private async Task NavigateInternalAsync(
            Type viewType,
            Type viewModelType,
            object parameter,
            object preloadedViewModel = null,
            CancellationToken cancellationToken = default
        )
        {
            _logService?.Log(LogLevel.Info, $"[NavigateInternalAsync] Starting navigation to {viewModelType.Name}, preloadedViewModel: {preloadedViewModel != null}");

            cancellationToken.ThrowIfCancellationRequested();

            if (_currentViewModel != null)
            {
                _logService?.Log(LogLevel.Info, $"[NavigateInternalAsync] Disposing current ViewModel: {_currentViewModel.GetType().Name}");
                try 
                {
                    if (_currentViewModel is IFeatureViewModel currentVm)
                    {
                        currentVm.OnNavigatedFrom();
                    }
                    
                    if (_currentViewModel is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    _logService?.Log(LogLevel.Warning, $"[NavigateInternalAsync] Error disposing current ViewModel: {ex.Message}");
                }
            }

            // Find the route for this view/viewmodel for event args
            string route = null;
            foreach (var mapping in _viewMappings)
            {
                if (mapping.Value.ViewType == viewType)
                {
                    route = mapping.Key;
                    break;
                }
            }

            var sourceView = _currentRoute;
            var targetView = route;

            var args = new Winhance.Core.Features.Common.Interfaces.NavigationEventArgs(
                sourceView,
                targetView,
                parameter,
                true
            );
            Navigating?.Invoke(this, args);

            if (args.Cancel)
            {
                return;
            }

            _currentParameter = parameter;

            object viewModel;

            if (preloadedViewModel != null)
            {
                _logService?.Log(LogLevel.Info, $"[NavigateInternalAsync] Using preloaded ViewModel: {preloadedViewModel.GetType().Name}");
                viewModel = preloadedViewModel;
            }
            else
            {
                _logService?.Log(LogLevel.Info, $"[NavigateInternalAsync] Creating new ViewModel of type: {viewModelType.Name}");
                try
                {
                    viewModel = _serviceProvider.GetRequiredService(viewModelType);
                    if (viewModel == null)
                    {
                        throw new InvalidOperationException(
                            $"Failed to create view model of type {viewModelType.FullName}. The service provider returned null."
                        );
                    }
                    _logService?.Log(LogLevel.Info, $"[NavigateInternalAsync] Successfully created ViewModel: {viewModel.GetType().Name}");
                }
                catch (Exception ex)
                {
                    _logService?.Log(LogLevel.Error, $"[NavigateInternalAsync] Error creating ViewModel: {ex.Message}");
                    throw new InvalidOperationException($"Error creating view model: {ex.Message}", ex);
                }
            }

            // Update the current route and view model
            _currentRoute = route;
            _currentViewModel = viewModel;
            _logService?.Log(LogLevel.Info, $"[NavigateInternalAsync] Updated current route to: {route}, ViewModel: {viewModel.GetType().Name}");

            // Update the navigation stacks
            while (_backStack.Count >= MaxHistorySize)
            {
                var tempStack = new Stack<Type>(_backStack.Skip(1).Reverse());
                _backStack.Clear();
                foreach (var item in tempStack)
                {
                    _backStack.Push(item);
                }
            }
            _backStack.Push(viewModelType);

            // Call OnNavigatedTo on the view model if it implements IFeatureViewModel
            if (viewModel is IFeatureViewModel vm)
            {
                vm.OnNavigatedTo(parameter);
            }

            // Update navigation history
            if (!string.IsNullOrEmpty(_currentRoute))
            {
                _navigationHistory.Add(_currentRoute);
            }

            // Raise the Navigated event which will update the UI
            _logService?.Log(LogLevel.Info, $"[NavigateInternalAsync] Raising Navigated event. SourceView: {sourceView}, TargetView: {targetView}");
            var navigatedArgs = new Winhance.Core.Features.Common.Interfaces.NavigationEventArgs(
                sourceView,
                targetView,
                viewModel,
                false
            );
            Navigated?.Invoke(this, navigatedArgs);
            _logService?.Log(LogLevel.Info, $"[NavigateInternalAsync] Navigated event raised for: {targetView}");
            _logService?.Log(LogLevel.Info, $"[NavigateInternalAsync] Navigation completed successfully to: {targetView}");
        }

        public async Task GoBackAsync()
        {
            if (!CanGoBack)
                return;

            var currentType = _backStack.Pop();
            _forwardStack.Push((currentType, _currentParameter));
            var previousViewModelType = _backStack.Peek();
            
            var route = GetRouteForViewModelType(previousViewModelType);
            await PreloadAndNavigateToAsync(route);
        }

        public async Task GoForwardAsync()
        {
            if (_forwardStack.Count == 0)
                return;

            var (nextType, nextParameter) = _forwardStack.Pop();
            _backStack.Push(nextType);
            
            var route = GetRouteForViewModelType(nextType);
            await PreloadAndNavigateToAsync(route);
        }

        public async Task ClearHistoryAsync()
        {
            _backStack.Clear();
            _forwardStack.Clear();
            _navigationHistory.Clear();
        }

        private object _currentViewModel;
        public object CurrentViewModel => _currentViewModel;

        private Type GetViewTypeForViewModel(Type viewModelType)
        {
            // First, check if we have a mapping for this view model type
            foreach (var mapping in _viewMappings)
            {
                if (mapping.Value.ViewModelType == viewModelType)
                {
                    return mapping.Value.ViewType;
                }
            }

            // If no mapping found, try the old way as fallback
            var viewName = viewModelType.FullName.Replace("ViewModel", "View");
            var viewType = Type.GetType(viewName);

            if (viewType == null)
            {
                // Try to find the view type in the loaded assemblies
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    viewType = assembly
                        .GetTypes()
                        .FirstOrDefault(t =>
                            t.FullName != null
                            && t.FullName.Equals(viewName, StringComparison.OrdinalIgnoreCase)
                        );

                    if (viewType != null)
                        break;
                }
            }

            return viewType
                ?? throw new InvalidOperationException(
                    $"View type for {viewModelType.FullName} not found. Tried looking for {viewName}"
                );
        }

        private string GetRouteForViewType(Type viewType)
        {
            foreach (var mapping in _viewMappings)
            {
                if (mapping.Value.ViewType == viewType)
                {
                    return mapping.Key;
                }
            }
            throw new InvalidOperationException(
                $"No route found for view type: {viewType.FullName}"
            );
        }

        private string GetRouteForViewModelType(Type viewModelType)
        {
            foreach (var mapping in _viewMappings)
            {
                if (mapping.Value.ViewModelType == viewModelType)
                {
                    return mapping.Key;
                }
            }
            throw new InvalidOperationException(
                $"No route found for view model type: {viewModelType.FullName}"
            );
        }
    }
}

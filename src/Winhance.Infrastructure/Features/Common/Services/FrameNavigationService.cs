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

namespace Winhance.Infrastructure.Features.Common.Services
{
    /// <summary>
    /// Service for navigating between views in the application using ContentPresenter-based navigation.
    /// </summary>
    public class FrameNavigationService : Winhance.Core.Features.Common.Interfaces.INavigationService
    {
        private readonly Stack<Type> _backStack = new();
        private readonly Stack<(Type ViewModelType, object Parameter)> _forwardStack = new();
        private readonly List<string> _navigationHistory = new();
        private readonly Dictionary<string, (Type ViewType, Type ViewModelType)> _viewMappings = new();
        private readonly IServiceProvider _serviceProvider;
        private readonly IParameterSerializer _parameterSerializer;
        private readonly SemaphoreSlim _navigationLock = new(1, 1);
        private readonly ConcurrentQueue<(Type ViewType, Type ViewModelType, object Parameter, TaskCompletionSource<bool> CompletionSource)> _navigationQueue = new();
        private object _currentParameter;
        private string _currentRoute;
        private const int MaxHistorySize = 50;
        private CancellationTokenSource _currentNavigationCts;
        private ICommand _navigateCommand;

        /// <summary>
        /// Gets the command for navigation.
        /// </summary>
        public ICommand NavigateCommand => _navigateCommand ??= new RelayCommand<string>(route => NavigateTo(route));

        /// <summary>
        /// Initializes a new instance of the <see cref="FrameNavigationService"/> class.
        /// </summary>
        /// <param name="serviceProvider">The service provider.</param>
        /// <param name="parameterSerializer">The parameter serializer.</param>
        public FrameNavigationService(IServiceProvider serviceProvider, IParameterSerializer parameterSerializer)
        {
            _serviceProvider = serviceProvider;
            _parameterSerializer = parameterSerializer ?? throw new ArgumentNullException(nameof(parameterSerializer));
        }

        /// <summary>
        /// Determines whether navigation back is possible.
        /// </summary>
        public bool CanGoBack => _backStack.Count > 1;
        
        /// <summary>
        /// Gets a list of navigation history.
        /// </summary>
        public IReadOnlyList<string> NavigationHistory => _navigationHistory.AsReadOnly();

        /// <summary>
        /// Gets the current view name.
        /// </summary>
        public string CurrentView => _currentRoute;

        /// <summary>
        /// Event raised when navigation occurs.
        /// </summary>
        public event EventHandler<Winhance.Core.Features.Common.Interfaces.NavigationEventArgs> Navigated;

        /// <summary>
        /// Event raised before navigation occurs.
        /// </summary>
        public event EventHandler<Winhance.Core.Features.Common.Interfaces.NavigationEventArgs> Navigating;

        /// <summary>
        /// Event raised when navigation fails.
        /// </summary>
        public event EventHandler<Winhance.Core.Features.Common.Interfaces.NavigationEventArgs> NavigationFailed;

        /// <summary>
        /// Initializes the navigation service.
        /// </summary>
        public void Initialize()
        {
            // No initialization needed for ContentPresenter-based navigation
        }

        /// <summary>
        /// Registers a view mapping.
        /// </summary>
        /// <param name="route">The route.</param>
        /// <param name="viewType">The view type.</param>
        /// <param name="viewModelType">The view model type.</param>
        public void RegisterViewMapping(string route, Type viewType, Type viewModelType)
        {
            if (string.IsNullOrWhiteSpace(route))
                throw new ArgumentException("Route cannot be empty", nameof(route));

            _viewMappings[route] = (viewType, viewModelType);
        }

        /// <summary>
        /// Checks if navigation to a route is possible.
        /// </summary>
        /// <param name="route">The route to check.</param>
        /// <returns>True if navigation is possible; otherwise, false.</returns>
        public bool CanNavigateTo(string route) => _viewMappings.ContainsKey(route);

        /// <summary>
        /// Navigates to a view by route.
        /// </summary>
        /// <param name="viewName">The route to navigate to.</param>
        /// <returns>True if navigation was successful; otherwise, false.</returns>
        public bool NavigateTo(string viewName)
        {
            try
            {
                NavigateToAsync(viewName).GetAwaiter().GetResult();
                return true;
            }
            catch (Exception ex)
            {
                // Log the error
                System.Diagnostics.Debug.WriteLine($"Navigation error: {ex.Message}");

                var args = new Winhance.Core.Features.Common.Interfaces.NavigationEventArgs(CurrentView, viewName, null, false);
                NavigationFailed?.Invoke(this, args);
                return false;
            }
        }

        /// <summary>
        /// Navigates to a view by route with a parameter.
        /// </summary>
        /// <param name="viewName">The route to navigate to.</param>
        /// <param name="parameter">The navigation parameter.</param>
        /// <returns>True if navigation was successful; otherwise, false.</returns>
        public bool NavigateTo(string viewName, object parameter)
        {
            try
            {
                NavigateToAsync(viewName, parameter).GetAwaiter().GetResult();
                return true;
            }
            catch (Exception ex)
            {
                // Log the error
                System.Diagnostics.Debug.WriteLine($"Navigation error: {ex.Message}");

                var args = new Winhance.Core.Features.Common.Interfaces.NavigationEventArgs(CurrentView, viewName, parameter, false);
                NavigationFailed?.Invoke(this, args);
                return false;
            }
        }

        /// <summary>
        /// Navigates back to the previous view.
        /// </summary>
        /// <returns>True if navigation was successful; otherwise, false.</returns>
        public bool NavigateBack()
        {
            try
            {
                GoBackAsync().GetAwaiter().GetResult();
                return true;
            }
            catch (Exception ex)
            {
                // Log the error
                System.Diagnostics.Debug.WriteLine($"Navigation back error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Navigates to a view model type.
        /// </summary>
        /// <typeparam name="TViewModel">The view model type.</typeparam>
        /// <param name="parameter">The navigation parameter.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task NavigateToAsync<TViewModel>(object parameter = null) where TViewModel : class
            => await NavigateToAsync(typeof(TViewModel), parameter);

        /// <summary>
        /// Navigates to a view model type.
        /// </summary>
        /// <param name="viewModelType">The view model type.</param>
        /// <param name="parameter">The navigation parameter.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task NavigateToAsync(Type viewModelType, object parameter = null)
        {
            var viewType = GetViewTypeForViewModel(viewModelType);
            var tcs = new TaskCompletionSource<bool>();
            _navigationQueue.Enqueue((viewType, viewModelType, parameter, tcs));
            
            await ProcessNavigationQueueAsync();
            await tcs.Task;
        }

        /// <summary>
        /// Navigates to a route.
        /// </summary>
        /// <param name="route">The route to navigate to.</param>
        /// <param name="parameter">The navigation parameter.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task NavigateToAsync(string route, object parameter = null)
        {
            if (!_viewMappings.TryGetValue(route, out var mapping))
                throw new InvalidOperationException($"No view mapping found for route: {route}");

            await NavigateInternalAsync(mapping.ViewType, mapping.ViewModelType, parameter);
        }

        private async Task ProcessNavigationQueueAsync()
        {
            if (!await _navigationLock.WaitAsync(TimeSpan.FromMilliseconds(100)))
                return;

            try
            {
                while (_navigationQueue.TryDequeue(out var navigationRequest))
                {
                    _currentNavigationCts?.Cancel();
                    _currentNavigationCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                    
                    try
                    {
                        await NavigateInternalAsync(
                            navigationRequest.ViewType,
                            navigationRequest.ViewModelType,
                            navigationRequest.Parameter,
                            _currentNavigationCts.Token);
                        navigationRequest.CompletionSource.SetResult(true);
                    }
                    catch (Exception ex)
                    {
                        navigationRequest.CompletionSource.SetException(ex);
                    }
                }
            }
            finally
            {
                _navigationLock.Release();
            }
        }

        private async Task NavigateInternalAsync(Type viewType, Type viewModelType, object parameter, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
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

            var args = new Winhance.Core.Features.Common.Interfaces.NavigationEventArgs(sourceView, targetView, parameter, true);
            Navigating?.Invoke(this, args);

            if (args.Cancel)
            {
                return;
            }
            
            _currentParameter = parameter;
            
            // Get the view model from the service provider - we don't need the view when using ContentPresenter
            object viewModel;
            
            try
            {
                viewModel = _serviceProvider.GetRequiredService(viewModelType);
                if (viewModel == null)
                {
                    throw new InvalidOperationException($"Failed to create view model of type {viewModelType.FullName}. The service provider returned null.");
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error creating view model: {ex.Message}", ex);
            }

            // Update the current route and view model
            _currentRoute = route;
            _currentViewModel = viewModel;
            
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
            
            // Call OnNavigatedTo on the view model if it implements IViewModel
            if (viewModel is IViewModel vm)
            {
                vm.OnNavigatedTo(parameter);
            }
            
            // Update navigation history
            if (!string.IsNullOrEmpty(_currentRoute))
            {
                _navigationHistory.Add(_currentRoute);
            }
            
            // Raise the Navigated event which will update the UI
            var navigatedArgs = new Winhance.Core.Features.Common.Interfaces.NavigationEventArgs(sourceView, targetView, viewModel, false);
            Navigated?.Invoke(this, navigatedArgs);
        }

        /// <summary>
        /// Navigates back to the previous view.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task GoBackAsync()
        {
            if (!CanGoBack) return;
            
            var tcs = new TaskCompletionSource<bool>();
            _navigationQueue.Enqueue((null, null, null, tcs));
            
            await ProcessNavigationQueueAsync();
            await tcs.Task;
            
            var currentViewModelType = _backStack.Peek();
            
            var currentType = _backStack.Pop();
            _forwardStack.Push((currentType, _currentParameter));
            var previousViewModelType = _backStack.Peek();
            var previousParameter = _backStack.Count > 1 ? _backStack.ElementAt(_backStack.Count - 2) : null;
            await NavigateToAsync(previousViewModelType, previousParameter);
        }

        /// <summary>
        /// Navigates forward to the next view.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task GoForwardAsync()
        {
            if (_forwardStack.Count == 0) return;
            
            var tcs = new TaskCompletionSource<bool>();
            _navigationQueue.Enqueue((null, null, null, tcs));
            
            await ProcessNavigationQueueAsync();
            await tcs.Task;
            
            var (nextType, nextParameter) = _forwardStack.Pop();
            
            _backStack.Push(nextType);
            await NavigateToAsync(nextType, nextParameter);
        }

        /// <summary>
        /// Clears the navigation history.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task ClearHistoryAsync()
        {
            await _navigationLock.WaitAsync();
            try
            {
                _backStack.Clear();
                _forwardStack.Clear();
                _navigationHistory.Clear();
            }
            finally
            {
                _navigationLock.Release();
            }
        }

        /// <summary>
        /// Cancels the current navigation.
        /// </summary>
        public void CancelCurrentNavigation()
        {
            _currentNavigationCts?.Cancel();
        }

        // We track the current view model directly now instead of getting it from the Frame
        private object _currentViewModel;
        
        /// <summary>
        /// Gets the current view model.
        /// </summary>
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
                    viewType = assembly.GetTypes()
                        .FirstOrDefault(t => t.FullName != null && t.FullName.Equals(viewName, StringComparison.OrdinalIgnoreCase));
                    
                    if (viewType != null)
                        break;
                }
            }
            
            return viewType ?? 
                throw new InvalidOperationException($"View type for {viewModelType.FullName} not found. Tried looking for {viewName}");
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
            throw new InvalidOperationException($"No route found for view type: {viewType.FullName}");
        }
    }
}

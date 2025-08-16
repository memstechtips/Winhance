using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Enums;
using Winhance.WPF.Features.Common.Interfaces;
using Winhance.WPF.Features.Optimize.Views;
using Winhance.WPF.Features.Optimize.ViewModels;

namespace Winhance.WPF.Features.Optimize.ViewModels
{
    /// <summary>
    /// SOLID-compliant composition ViewModel that manages optimization feature Views.
    /// Follows Single Responsibility Principle - only responsible for UI composition.
    /// </summary>
    public partial class OptimizeViewModel : ObservableObject, IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ISearchTextCoordinationService _searchTextCoordinationService;
        
        /// <summary>
        /// Collection of all feature Views that are dynamically created with proper DI.
        /// </summary>
        private List<UserControl> _allFeatureViews = new();
        
        /// <summary>
        /// Collection of feature Views that are dynamically created with proper DI.
        /// This is the filtered collection that's bound to the UI.
        /// </summary>
        public ObservableCollection<UserControl> FeatureViews { get; } = new();
        
        [ObservableProperty]
        private string _statusText = "Optimize Your Windows Settings and Performance";
        
        [ObservableProperty]
        private string _searchText = string.Empty;
        
        [ObservableProperty]
        private bool _hasSearchResults = true;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="OptimizeViewModel"/> class.
        /// </summary>
        /// <param name="serviceProvider">Service provider for DI resolution.</param>
        /// <param name="searchTextCoordinationService">Service for coordinating search text across features.</param>
        public OptimizeViewModel(IServiceProvider serviceProvider, ISearchTextCoordinationService searchTextCoordinationService)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _searchTextCoordinationService = searchTextCoordinationService ?? throw new ArgumentNullException(nameof(searchTextCoordinationService));
            
            InitializeFeatureViews();
            SubscribeToSearchCoordination();
        }
        
        partial void OnSearchTextChanged(string value)
        {
            var searchText = value ?? string.Empty;
            
            // Delegate search coordination to the domain service (Dependency Inversion Principle)
            _searchTextCoordinationService.UpdateSearchText(searchText);
        }
        
        /// <summary>
        /// Handles search text changes from the coordination service.
        /// </summary>
        private void OnSearchTextChangedEvent(object? sender, SearchTextChangedEventArgs e)
        {
            // Propagate search text to all child features
            foreach (var view in _allFeatureViews)
            {
                if (view.DataContext is ISearchableFeatureViewModel searchableFeature)
                {
                    searchableFeature.ApplySearchFilter(e.SearchText);
                }
                else if (view.DataContext is IFeatureViewModel basicFeature)
                {
                    // For basic features, set the SearchText property
                    basicFeature.SearchText = e.SearchText;
                }
            }
            
            // Update feature visibility based on their self-reported results
            UpdateFeatureVisibility();
        }
        
        /// <summary>
        /// Subscribes to search text coordination events and feature visibility changes.
        /// Clean architecture approach - features handle their own filtering logic.
        /// </summary>
        private void SubscribeToSearchCoordination()
        {
            // Subscribe to search coordination events
            _searchTextCoordinationService.SearchTextChanged += OnSearchTextChangedEvent;
            
            // Register all features with the search coordination service
            foreach (var view in _allFeatureViews)
            {
                if (view.DataContext is ISearchableFeatureViewModel searchableFeature)
                {
                    // Register the feature with its search handler (Dependency Inversion)
                    // Features will handle their own search filtering
                    
                    // Subscribe to feature visibility changes
                    searchableFeature.VisibilityChanged += OnFeatureVisibilityChanged;
                }
                else if (view.DataContext is IFeatureViewModel basicFeature)
                {
                    // Register basic feature ViewModels that only implement IFeatureViewModel
                    // Create a simple search handler that filters settings via SearchText property
                    // Basic features will handle their own search filtering
                }
            }
        }
        
        /// <summary>
        /// Handles search coordination changes and updates UI accordingly.
        /// </summary>
        private void OnSearchCoordinationChanged(object? sender, SearchTextChangedEventArgs e)
        {
            UpdateFeatureVisibility();
        }
        
        /// <summary>
        /// Handles individual feature visibility changes.
        /// </summary>
        private void OnFeatureVisibilityChanged(object? sender, FeatureVisibilityChangedEventArgs e)
        {
            // Features handle their own visibility logic, we just update the UI
            
            // Update the UI to reflect visibility changes
            UpdateFeatureVisibility();
        }
        
        /// <summary>
        /// Updates the visibility of features based on search coordination results.
        /// During search, shows all features and lets them internally filter their settings.
        /// Only hides features that have absolutely no matching settings.
        /// </summary>
        private void UpdateFeatureVisibility()
        {
            var currentSearchText = _searchTextCoordinationService.CurrentSearchText;
            
            // Clear the current collection
            FeatureViews.Clear();
            
            bool hasAnyVisibleFeatures = false;
            bool hasActiveSearch = !string.IsNullOrWhiteSpace(currentSearchText);
            
            // Show features based on search coordination service results
            foreach (var view in _allFeatureViews)
            {
                if (view.DataContext is ISearchableFeatureViewModel searchableFeature)
                {
                    bool shouldShowFeature;
                    
                    if (hasActiveSearch)
                    {
                        // During search: show features that have any matching settings
                        // This allows the feature to display only its filtered settings
                        shouldShowFeature = searchableFeature.IsVisibleInSearch;
                    }
                    else
                    {
                        // No search: show all features
                        shouldShowFeature = true;
                    }
                    
                    if (shouldShowFeature)
                    {
                        FeatureViews.Add(view);
                        hasAnyVisibleFeatures = true;
                    }
                }
                else if (view.DataContext is IFeatureViewModel basicFeature)
                {
                    bool shouldShowFeature;
                    
                    if (hasActiveSearch)
                    {
                        // During search: show features that have any matching settings
                        shouldShowFeature = basicFeature.HasVisibleSettings;
                    }
                    else
                    {
                        // No search: show all features
                        shouldShowFeature = true;
                    }
                    
                    if (shouldShowFeature)
                    {
                        FeatureViews.Add(view);
                        hasAnyVisibleFeatures = true;
                    }
                }
            }
            
            // Update HasSearchResults based on whether any features are visible
            HasSearchResults = hasAnyVisibleFeatures;
        }
        
        /// <summary>
        /// Creates feature Views with proper dependency injection.
        /// Each View gets its ViewModel injected via constructor.
        /// </summary>
        private void InitializeFeatureViews()
        {
            // Create each feature View with its ViewModel injected
            var gamingView = CreateFeatureView<GamingandPerformanceOptimizationsView, GamingandPerformanceOptimizationsViewModel>();
            var powerView = CreateFeatureView<PowerOptimizationsView, PowerOptimizationsViewModel>();
            var privacyView = CreateFeatureView<PrivacyOptimizationsView, PrivacyOptimizationsViewModel>();
            var updateView = CreateFeatureView<UpdateOptimizationsView, UpdateOptimizationsViewModel>();
            var securityView = CreateFeatureView<WindowsSecurityOptimizationsView, WindowsSecurityOptimizationsViewModel>();
            var explorerView = CreateFeatureView<ExplorerOptimizationsView, ExplorerOptimizationsViewModel>();
            var notificationView = CreateFeatureView<NotificationOptimizationsView, NotificationOptimizationsViewModel>();
            var soundView = CreateFeatureView<SoundOptimizationsView, SoundOptimizationsViewModel>();
            
            // Store all views in the private collection
            _allFeatureViews = new List<UserControl>
            {
                gamingView,
                powerView,
                privacyView,
                updateView,
                securityView,
                explorerView,
                notificationView,
                soundView
            };
            
            // Add all views to the observable collection initially
            foreach (var view in _allFeatureViews)
            {
                FeatureViews.Add(view);
            }
        }
        
        /// <summary>
        /// Creates a feature View with its ViewModel properly injected via DI.
        /// </summary>
        /// <typeparam name="TView">Type of the View to create.</typeparam>
        /// <typeparam name="TViewModel">Type of the ViewModel to inject.</typeparam>
        /// <returns>The created View with ViewModel injected.</returns>
        private TView CreateFeatureView<TView, TViewModel>()
            where TView : UserControl, new()
            where TViewModel : class
        {
            var viewModel = _serviceProvider.GetRequiredService<TViewModel>();
            var view = new TView
            {
                DataContext = viewModel
            };
            
            // Initialize ViewModel if it implements IFeatureViewModel
            if (viewModel is IFeatureViewModel featureViewModel)
            {
                bool hasLoadedOnce = false;
                view.Loaded += async (s, e) => 
                {
                    // Only load settings once, not on every visibility change during search
                    if (!hasLoadedOnce)
                    {
                        hasLoadedOnce = true;
                        await featureViewModel.LoadSettingsAsync();
                    }
                };
            }
            
            return view;
        }
        
        /// <summary>
        /// Disposes of resources and unsubscribes from events.
        /// Follows proper disposal pattern to prevent memory leaks.
        /// </summary>
        public void Dispose()
        {
            // Unsubscribe from search coordination events
            _searchTextCoordinationService.SearchTextChanged -= OnSearchTextChangedEvent;
            
            // Unregister all features and unsubscribe from their events
            foreach (var view in _allFeatureViews)
            {
                if (view.DataContext is ISearchableFeatureViewModel searchableFeature)
                {
                    // No need to unregister features in clean architecture
                    searchableFeature.VisibilityChanged -= OnFeatureVisibilityChanged;
                }
                else if (view.DataContext is IFeatureViewModel basicFeature)
                {
                    // No need to unregister features in clean architecture
                }
            }
        }
    }
    

}

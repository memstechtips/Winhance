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
    /// Search coordination is delegated to ISearchCoordinationService (Dependency Inversion).
    /// </summary>
    public partial class OptimizeCompositionViewModel : ObservableObject, IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ISearchCoordinationService _searchCoordinationService;
        
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
        /// Initializes a new instance of the <see cref="OptimizeCompositionViewModel"/> class.
        /// </summary>
        /// <param name="serviceProvider">Service provider for DI resolution.</param>
        /// <param name="searchCoordinationService">Service for coordinating search across features.</param>
        public OptimizeCompositionViewModel(IServiceProvider serviceProvider, ISearchCoordinationService searchCoordinationService)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _searchCoordinationService = searchCoordinationService ?? throw new ArgumentNullException(nameof(searchCoordinationService));
            
            InitializeFeatureViews();
            SubscribeToSearchCoordination();
        }
        
        partial void OnSearchTextChanged(string value)
        {
            var searchText = value ?? string.Empty;
            
            // Delegate search coordination to the domain service (Dependency Inversion Principle)
            _searchCoordinationService.UpdateSearchText(searchText);
        }
        
        /// <summary>
        /// Subscribes to search coordination events and sets up feature registration.
        /// Follows Open/Closed Principle - extends functionality without modifying existing code.
        /// </summary>
        private void SubscribeToSearchCoordination()
        {
            // Subscribe to search coordination events
            _searchCoordinationService.SearchTextChanged += OnSearchCoordinationChanged;
            
            // Register all features with the search coordination service
            foreach (var view in _allFeatureViews)
            {
                if (view.DataContext is ISearchableFeatureViewModel searchableFeature)
                {
                    // Register the feature with its search handler (Dependency Inversion)
                    _searchCoordinationService.RegisterFeature(
                        searchableFeature.ModuleId, 
                        searchText => searchableFeature.ApplySearchFilter(searchText)
                    );
                    
                    // Subscribe to feature visibility changes
                    searchableFeature.VisibilityChanged += OnFeatureVisibilityChanged;
                }
                else if (view.DataContext is IFeatureViewModel basicFeature)
                {
                    // Register basic feature ViewModels that only implement IFeatureViewModel
                    // Create a simple search handler that filters settings via SearchText property
                    _searchCoordinationService.RegisterFeature(
                        basicFeature.ModuleId, 
                        searchText => 
                        {
                            basicFeature.SearchText = searchText;
                            // Update feature visibility based on whether it has visible settings
                            _searchCoordinationService.UpdateFeatureVisibility(basicFeature.ModuleId, basicFeature.HasVisibleSettings);
                        }
                    );
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
            // Update the search coordination service with the feature's visibility state
            _searchCoordinationService.UpdateFeatureVisibility(e.FeatureId, e.IsVisible);
            
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
            var currentSearchText = _searchCoordinationService.CurrentSearchText;
            
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
                        bool hasMatchingSettings = _searchCoordinationService.GetFeatureVisibility(searchableFeature.ModuleId);
                        shouldShowFeature = hasMatchingSettings;
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
                        bool hasMatchingSettings = _searchCoordinationService.GetFeatureVisibility(basicFeature.ModuleId);
                        shouldShowFeature = hasMatchingSettings;
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
            _searchCoordinationService.SearchTextChanged -= OnSearchCoordinationChanged;
            
            // Unregister all features and unsubscribe from their events
            foreach (var view in _allFeatureViews)
            {
                if (view.DataContext is ISearchableFeatureViewModel searchableFeature)
                {
                    _searchCoordinationService.UnregisterFeature(searchableFeature.ModuleId);
                    searchableFeature.VisibilityChanged -= OnFeatureVisibilityChanged;
                }
                else if (view.DataContext is IFeatureViewModel basicFeature)
                {
                    _searchCoordinationService.UnregisterFeature(basicFeature.ModuleId);
                }
            }
        }
    }
    

}

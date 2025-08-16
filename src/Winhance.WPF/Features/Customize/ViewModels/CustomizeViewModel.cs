using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Enums;
using Winhance.WPF.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Factories;
using Winhance.WPF.Features.Common.Models;
using Winhance.WPF.Features.Common.Resources.Theme;
using Winhance.WPF.Features.Customize.Views;

namespace Winhance.WPF.Features.Customize.ViewModels
{
    /// <summary>
    /// Clean architecture composition ViewModel for Customize feature.
    /// Follows SOLID principles with proper dependency injection.
    /// Uses dynamic feature discovery and factory patterns.
    /// </summary>
    public partial class CustomizeViewModel : ObservableObject, IDisposable
    {
        #region Private Fields
        
        private readonly IFeatureDiscoveryService _featureDiscoveryService;
        private readonly IFeatureViewModelFactory _featureViewModelFactory;
        private readonly IThemeManager _themeManager;
        private readonly IEventBus _eventBus;
        private readonly ILogService _logService;
        private readonly ISearchTextCoordinationService _searchTextCoordinationService;
        
        #endregion
        
        #region Observable Properties
        
        /// <summary>
        /// Collection of feature Views dynamically created via factory pattern.
        /// </summary>
        public ObservableCollection<UserControl> FeatureViews { get; } = new();
        
        [ObservableProperty]
        private string _statusText = "Customize Your Windows Experience";
        
        [ObservableProperty]
        private string _searchText = string.Empty;
        
        [ObservableProperty]
        private bool _hasSearchResults = true;
        
        [ObservableProperty]
        private bool _isLoading;
        
        /// <summary>
        /// Gets a value indicating whether the ViewModel is initialized.
        /// </summary>
        public bool IsInitialized { get; private set; }
        
        /// <summary>
        /// Gets the aggregated settings from all feature ViewModels.
        /// </summary>
        public ObservableCollection<SettingUIItem> Settings
        {
            get
            {
                var allSettings = new ObservableCollection<SettingUIItem>();
                foreach (var view in FeatureViews)
                {
                    if (view.DataContext is IFeatureViewModel featureViewModel)
                    {
                        foreach (var setting in featureViewModel.Settings)
                            allSettings.Add(setting);
                    }
                }
                return allSettings;
            }
        }
        
        #endregion
        
        #region Commands
        
        /// <summary>
        /// Command to initialize the ViewModel asynchronously.
        /// </summary>
        public IAsyncRelayCommand InitializeCommand { get; }
        
        #endregion
        
        #region Constructor
        
        /// <summary>
        /// Initializes a new instance of the CustomizeViewModel with proper dependency injection.
        /// Follows Dependency Inversion Principle - depends on abstractions, not concretions.
        /// </summary>
        /// <param name="featureDiscoveryService">Service for discovering customize features.</param>
        /// <param name="featureViewModelFactory">Factory for creating feature ViewModels.</param>
        /// <param name="themeManager">Service for managing application themes.</param>
        /// <param name="eventBus">Service for event communication.</param>
        /// <param name="logService">Service for logging operations.</param>
        /// <param name="searchTextCoordinationService">Service for coordinating search text across features.</param>
        public CustomizeViewModel(
            IFeatureDiscoveryService featureDiscoveryService,
            IFeatureViewModelFactory featureViewModelFactory,
            IThemeManager themeManager,
            IEventBus eventBus,
            ILogService logService,
            ISearchTextCoordinationService searchTextCoordinationService)
        {
            // Null checks for Fail-Fast principle
            _featureDiscoveryService = featureDiscoveryService ?? throw new ArgumentNullException(nameof(featureDiscoveryService));
            _featureViewModelFactory = featureViewModelFactory ?? throw new ArgumentNullException(nameof(featureViewModelFactory));
            _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            
            _searchTextCoordinationService = searchTextCoordinationService ?? throw new ArgumentNullException(nameof(searchTextCoordinationService));
            
            // Initialize commands
            InitializeCommand = new AsyncRelayCommand(InitializeAsync);
            
            // Subscribe to search text coordination
            _searchTextCoordinationService.SearchTextChanged += OnSearchTextChanged;
            
            // Auto-initialize on UI thread
            _ = InitializeAsync();
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Initializes the ViewModel asynchronously following clean architecture patterns.
        /// </summary>
        public async Task InitializeAsync()
        {
            if (IsInitialized) return;
            
            try
            {
                IsLoading = true;
                StatusText = "Discovering customization features...";
                
                // Use domain service to discover features (Single Responsibility)
                var features = await _featureDiscoveryService.DiscoverFeaturesAsync("Customization");
                
                StatusText = "Loading customization features...";
                
                // Create feature views using factory pattern (Open/Closed Principle)
                await CreateFeatureViewsAsync(features);
                
                // Load settings for all features first
                await LoadSettingsAsync();
                
                // Subscribe to feature visibility changes after settings are loaded
                SubscribeToFeatureVisibilityChanges();
                
                StatusText = $"Loaded {FeatureViews.Count} customization features";
                IsInitialized = true;
                
                _logService.Log(LogLevel.Info, $"CustomizeViewModel initialized with {FeatureViews.Count} features");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error initializing CustomizeViewModel: {ex.Message}");
                StatusText = "Error loading customization features";
                throw;
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        /// <summary>
        /// Loads settings asynchronously (for configuration services compatibility).
        /// </summary>
        public async Task LoadSettingsAsync()
        {
            // Load settings for all feature ViewModels
            var loadTasks = new List<Task>();
            
            foreach (var view in FeatureViews)
            {
                if (view.DataContext is IFeatureViewModel featureViewModel)
                {
                    loadTasks.Add(LoadFeatureSettingsAsync(featureViewModel));
                }
            }
            
            if (loadTasks.Any())
            {
                await Task.WhenAll(loadTasks);
            }
            
            // Notify that Settings property has changed
            OnPropertyChanged(nameof(Settings));
        }
        
        private async Task LoadFeatureSettingsAsync(IFeatureViewModel featureViewModel)
        {
            await featureViewModel.LoadSettingsAsync();
            _logService.Log(LogLevel.Info, $"ViewModel {featureViewModel.ModuleId}: HasVisibleSettings = {featureViewModel.HasVisibleSettings}, SettingsCount = {featureViewModel.SettingsCount}");
        }
        
        #endregion
        
        #region Private Methods
        
        /// <summary>
        /// Creates feature views using factory pattern and proper error handling.
        /// Follows Single Responsibility and Interface Segregation principles.
        /// </summary>
        private async Task CreateFeatureViewsAsync(IEnumerable<IFeatureDescriptor> features)
        {
            FeatureViews.Clear();
            
            foreach (var descriptor in features.OrderBy(f => f.SortOrder))
            {
                try
                {
                    // Use factory to create ViewModel (Dependency Inversion)
                    var featureViewModel = await _featureViewModelFactory.CreateAsync(descriptor);
                    if (featureViewModel == null) continue;
                    
                    // Create corresponding view based on ModuleId
                    var view = CreateViewForFeature(descriptor.ModuleId, featureViewModel);
                    if (view != null)
                    {
                        FeatureViews.Add(view);
                        _logService.Log(LogLevel.Info, $"Created customize feature: {descriptor.DisplayName}");
                        _logService.Log(LogLevel.Info, $"FeatureViews collection now has {FeatureViews.Count} items");
                    }
                    else
                    {
                        _logService.Log(LogLevel.Warning, $"Failed to create view for feature: {descriptor.DisplayName} (ModuleId: {descriptor.ModuleId})");
                    }
                }
                catch (Exception ex)
                {
                    _logService.Log(LogLevel.Warning, $"Failed to create customize feature '{descriptor.DisplayName}': {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Creates the appropriate View for a feature based on its ModuleId.
        /// Follows Factory pattern principles.
        /// </summary>
        private UserControl CreateViewForFeature(string moduleId, IFeatureViewModel viewModel)
        {
            _logService.Log(LogLevel.Info, $"Creating view for moduleId: {moduleId}");
            
            UserControl view = moduleId switch
            {
                "explorer-customization" => new ExplorerCustomizationsView(),
                "start-menu" => new StartMenuCustomizationsView(),
                "taskbar" => new TaskbarCustomizationsView(),
                "windows-theme" => new WindowsThemeCustomizationsView(),
                _ => null
            };
            
            if (view != null)
            {
                _logService.Log(LogLevel.Info, $"Successfully created view for moduleId: {moduleId}");
                view.DataContext = viewModel;
            }
            
            return view;
        }
        
        /// <summary>
        /// Subscribes to feature visibility changes for search coordination.
        /// Clean architecture approach - features handle their own filtering logic.
        /// </summary>
        private void SubscribeToFeatureVisibilityChanges()
        {
            foreach (var view in FeatureViews)
            {
                if (view.DataContext is ISearchableFeatureViewModel searchableFeature)
                {
                    // Subscribe to visibility changes from searchable features
                    searchableFeature.VisibilityChanged += OnFeatureVisibilityChanged;
                }
            }
        }
        
        /// <summary>
        /// Handles search text changes and propagates to child features.
        /// Clean architecture - each child handles its own filtering logic.
        /// </summary>
        private void OnSearchTextChanged(object? sender, SearchTextChangedEventArgs e)
        {
            // Propagate search text to all child features
            foreach (var view in FeatureViews)
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
        /// Handles individual feature visibility changes.
        /// Clean architecture - features report their own visibility state.
        /// </summary>
        private void OnFeatureVisibilityChanged(object? sender, FeatureVisibilityChangedEventArgs e)
        {
            // Features handle their own visibility logic, we just update the UI
            UpdateFeatureVisibility();
        }
        
        /// <summary>
        /// Updates feature visibility based on search results.
        /// Clean architecture - features determine their own visibility.
        /// </summary>
        private void UpdateFeatureVisibility()
        {
            bool hasAnyVisibleFeatures = false;
            bool hasActiveSearch = !string.IsNullOrWhiteSpace(_searchTextCoordinationService.CurrentSearchText);
            
            foreach (var view in FeatureViews)
            {
                bool shouldShow = true;
                
                if (hasActiveSearch)
                {
                    if (view.DataContext is ISearchableFeatureViewModel searchableFeature)
                    {
                        shouldShow = searchableFeature.IsVisibleInSearch;
                    }
                    else if (view.DataContext is IFeatureViewModel basicFeature)
                    {
                        shouldShow = basicFeature.HasVisibleSettings;
                    }
                }
                
                view.Visibility = shouldShow ? Visibility.Visible : Visibility.Collapsed;
                
                if (shouldShow)
                    hasAnyVisibleFeatures = true;
            }
            
            HasSearchResults = hasAnyVisibleFeatures;
        }
        
        /// <summary>
        /// Handles SearchText property changes.
        /// </summary>
        partial void OnSearchTextChanged(string value)
        {
            _searchTextCoordinationService.UpdateSearchText(value ?? string.Empty);
        }
        
        #endregion
        
        #region IDisposable
        
        /// <summary>
        /// Disposes resources and unsubscribes from events.
        /// Follows proper disposal pattern to prevent memory leaks.
        /// </summary>
        public void Dispose()
        {
            // Unsubscribe from search text coordination
            _searchTextCoordinationService.SearchTextChanged -= OnSearchTextChanged;
            
            // Clean up feature event subscriptions
            foreach (var view in FeatureViews)
            {
                if (view.DataContext is ISearchableFeatureViewModel searchableFeature)
                {
                    searchableFeature.VisibilityChanged -= OnFeatureVisibilityChanged;
                }
            }
            
            GC.SuppressFinalize(this);
        }
        
        #endregion
    }
}
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Models;
using Winhance.WPF.Features.Common.Factories;
using Winhance.WPF.Features.Common.Resources.Theme;

namespace Winhance.WPF.Features.Customize.ViewModels
{
    /// <summary>
    /// Factory-based container ViewModel for the Customize view that dynamically loads customization features.
    /// Uses the new architecture with feature discovery and factory pattern for clean separation of concerns.
    /// </summary>
    public partial class CustomizeViewModel : ObservableObject
    {
        #region Private Fields

        private readonly IFeatureDiscoveryService _featureDiscoveryService;
        private readonly IFeatureViewModelFactory _featureViewModelFactory;
        private readonly ILogService _logService;
        private readonly IThemeManager _themeManager;
        private readonly IEventBus _eventBus;

        #endregion

        #region Dynamic Features

        /// <summary>
        /// Gets the collection of dynamically loaded customization feature ViewModels.
        /// </summary>
        public ObservableCollection<IFeatureViewModel> Features { get; } = new();

        // Legacy property accessors for backward compatibility with existing Views and configuration services
        public WindowsThemeCustomizationsViewModel WindowsThemeViewModel => 
            Features.OfType<WindowsThemeCustomizationsViewModel>().FirstOrDefault();
        public StartMenuCustomizationsViewModel StartMenuViewModel => 
            Features.OfType<StartMenuCustomizationsViewModel>().FirstOrDefault();
        public TaskbarCustomizationsViewModel TaskbarViewModel => 
            Features.OfType<TaskbarCustomizationsViewModel>().FirstOrDefault();
        public ExplorerCustomizationsViewModel ExplorerViewModel => 
            Features.OfType<ExplorerCustomizationsViewModel>().FirstOrDefault();

        #endregion

        #region Observable Properties

        /// <summary>
        /// Gets or sets a value indicating whether search has any results across all child ViewModels.
        /// </summary>
        [ObservableProperty]
        private bool _hasSearchResults = true;

        /// <summary>
        /// Gets or sets the status text.
        /// </summary>
        [ObservableProperty]
        private string _statusText = "Customize Your Windows Experience";

        /// <summary>
        /// Gets or sets the search text that will be applied to all child ViewModels.
        /// </summary>
        [ObservableProperty]
        private string _searchText = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether any child ViewModel is loading.
        /// </summary>
        [ObservableProperty]
        private bool _isLoading;

        #endregion

        #region Aggregate Properties

        /// <summary>
        /// Gets a value indicating whether any feature ViewModel has visible settings.
        /// </summary>
        public bool HasAnyVisibleSettings => Features.Any(f => f.HasVisibleSettings);

        /// <summary>
        /// Gets the total count of all settings across feature ViewModels.
        /// </summary>
        public int TotalSettingsCount => Features.Sum(f => f.Settings.Count);

        /// <summary>
        /// Gets the command to load all settings from child ViewModels.
        /// </summary>
        public IAsyncRelayCommand LoadAllSettingsCommand { get; }

        /// <summary>
        /// Gets the command to search across all child ViewModels.
        /// </summary>
        public IRelayCommand<string> SearchAllCommand { get; }
        
        /// <summary>
        /// Gets a value indicating whether the ViewModel is initialized.
        /// </summary>
        public bool IsInitialized { get; private set; }
        
        /// <summary>
        /// Gets the command to initialize the ViewModel.
        /// </summary>
        public IAsyncRelayCommand InitializeCommand { get; }
        
        /// <summary>
        /// Gets the aggregated settings from all child ViewModels.
        /// </summary>
        public ObservableCollection<SettingUIItem> Settings
        {
            get
            {
                var allSettings = new ObservableCollection<SettingUIItem>();
                foreach (var setting in WindowsThemeViewModel.Settings) allSettings.Add(setting);
                foreach (var setting in StartMenuViewModel.Settings) allSettings.Add(setting);
                foreach (var setting in TaskbarViewModel.Settings) allSettings.Add(setting);
                foreach (var setting in ExplorerViewModel.Settings) allSettings.Add(setting);
                return allSettings;
            }
        }
        
        /// <summary>
        /// Loads settings asynchronously (public method for configuration services).
        /// </summary>
        public async Task LoadSettingsAsync() => await LoadAllSettingsAsync();
        
        /// <summary>
        /// Initializes the ViewModel asynchronously.
        /// </summary>
        public async Task InitializeAsync()
        {
            if (!IsInitialized)
            {
                await LoadAllSettingsAsync();
                IsInitialized = true;
            }
        }

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="CustomizeViewModel"/> class.
        /// </summary>
        /// <param name="featureDiscoveryService">The feature discovery service.</param>
        /// <param name="featureViewModelFactory">The feature ViewModel factory.</param>
        /// <param name="themeManager">The theme manager.</param>
        /// <param name="eventBus">The event bus.</param>
        /// <param name="logService">The log service.</param>
        public CustomizeViewModel(
            IFeatureDiscoveryService featureDiscoveryService,
            IFeatureViewModelFactory featureViewModelFactory,
            IThemeManager themeManager,
            IEventBus eventBus,
            ILogService logService)
        {
            _featureDiscoveryService = featureDiscoveryService ?? throw new ArgumentNullException(nameof(featureDiscoveryService));
            _featureViewModelFactory = featureViewModelFactory ?? throw new ArgumentNullException(nameof(featureViewModelFactory));
            _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            
            // Initialize commands
            LoadAllSettingsCommand = new AsyncRelayCommand(LoadAllSettingsAsync);
            InitializeCommand = new AsyncRelayCommand(InitializeAsync);
            SearchAllCommand = new RelayCommand<string>(SearchAll);
            
            // Subscribe to SearchText changes to propagate to children
            PropertyChanged += OnPropertyChanged;
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets the event bus for communication with other ViewModels.
        /// </summary>
        public IEventBus EventBus => _eventBus;

        #endregion

        #region Private Methods

        /// <summary>
        /// Loads all customization features dynamically using the factory pattern.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task LoadAllSettingsAsync()
        {
            try
            {
                IsLoading = true;
                StatusText = "Discovering customization features...";
                
                // Clear existing features
                Features.Clear();
                
                // Discover customization features
                var customizationFeatures = await _featureDiscoveryService.DiscoverFeaturesAsync("Customization");
                
                StatusText = "Loading customization features...";
                
                // Create ViewModels for each discovered feature
                var featureViewModels = new List<IFeatureViewModel>();
                foreach (var descriptor in customizationFeatures)
                {
                    try
                    {
                        var viewModel = await _featureViewModelFactory.CreateAsync(descriptor);
                        if (viewModel != null)
                        {
                            featureViewModels.Add(viewModel);
                            _logService.Log(LogLevel.Info, $"Loaded customization feature: {descriptor.DisplayName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logService.Log(LogLevel.Warning, $"Failed to load customization feature '{descriptor.DisplayName}': {ex.Message}");
                    }
                }
                
                // Add features to collection
                foreach (var viewModel in featureViewModels.OrderBy(f => f.SortOrder))
                {
                    Features.Add(viewModel);
                }
                
                // Load settings from all feature ViewModels in parallel
                if (Features.Any())
                {
                    StatusText = "Loading customization settings...";
                    var loadTasks = Features.Select(f => f.LoadSettingsAsync());
                    await Task.WhenAll(loadTasks);
                }

                StatusText = $"Loaded {Features.Count} customization features with {TotalSettingsCount} settings";
                OnPropertyChanged(nameof(HasAnyVisibleSettings));
                OnPropertyChanged(nameof(TotalSettingsCount));
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error loading customization features: {ex.Message}");
                StatusText = "Error loading customization features";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Searches across all feature ViewModels.
        /// </summary>
        /// <param name="searchText">The search text to apply.</param>
        private void SearchAll(string searchText)
        {
            var search = searchText ?? string.Empty;
            
            // Apply search to all feature ViewModels
            foreach (var feature in Features)
            {
                feature.SearchText = search;
            }
            
            // Update aggregate properties
            OnPropertyChanged(nameof(HasAnyVisibleSettings));
            UpdateSearchResults();
        }

        /// <summary>
        /// Handles property changes to propagate SearchText changes.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The property changed event args.</param>
        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SearchText))
            {
                SearchAll(SearchText);
            }
        }

        /// <summary>
        /// Updates the search results status.
        /// </summary>
        private void UpdateSearchResults()
        {
            HasSearchResults = string.IsNullOrEmpty(SearchText) || HasAnyVisibleSettings;
        }

        #endregion


    }
}

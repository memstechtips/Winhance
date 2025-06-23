using System;
using System.Threading.Tasks;
using System.Windows;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.WPF.Features.Common.ViewModels;
using Winhance.WPF.Features.Common.Services;
using Winhance.WPF.Features.SoftwareApps.Views;

namespace Winhance.WPF.Features.SoftwareApps.ViewModels
{
    /// <summary>
    /// ViewModel for the SoftwareAppsView that coordinates WindowsApps and ExternalApps sections.
    /// </summary>
    public partial class SoftwareAppsViewModel : BaseViewModel
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IPackageManager _packageManager;

        [ObservableProperty]
        private string _statusText =
            "Manage Windows Apps, Capabilities & Features and Install External Software";

        [ObservableProperty]
        private WindowsAppsViewModel _windowsAppsViewModel;

        [ObservableProperty]
        private ExternalAppsViewModel _externalAppsViewModel;
        
        [ObservableProperty]
        private string _searchText = string.Empty;
        
        [ObservableProperty]
        private bool _isWindowsAppsTabSelected = true;
        
        [ObservableProperty]
        private bool _isExternalAppsTabSelected = false;
        
        [ObservableProperty]
        private Visibility _windowsAppsContentVisibility = Visibility.Visible;
        
        [ObservableProperty]
        private Visibility _externalAppsContentVisibility = Visibility.Collapsed;
        
        [ObservableProperty]
        private bool _canInstallItems = false;
        
        [ObservableProperty]
        private bool _canRemoveItems = false;
        
        [ObservableProperty]
        private string _removeButtonText = "Remove Selected Items";
        
        [ObservableProperty]
        private object _currentHelpContent = null;
        
        [ObservableProperty]
        private FrameworkElement _helpButtonElement = null;
        
        [ObservableProperty]
        private bool _isHelpVisible = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="SoftwareAppsViewModel"/> class.
        /// </summary>
        /// <param name="progressService">The task progress service.</param>
        /// <param name="packageManager">The package manager.</param>
        /// <param name="serviceProvider">The service provider for dependency resolution.</param>
        public SoftwareAppsViewModel(
            ITaskProgressService progressService,
            IPackageManager packageManager,
            IServiceProvider serviceProvider
        )
            : base(progressService)
        {
            _packageManager =
                packageManager ?? throw new ArgumentNullException(nameof(packageManager));
            _serviceProvider =
                serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

            // Resolve the dependencies via DI container
            WindowsAppsViewModel = _serviceProvider.GetRequiredService<WindowsAppsViewModel>();
            ExternalAppsViewModel = _serviceProvider.GetRequiredService<ExternalAppsViewModel>();
            
            // Subscribe to property changes to handle search text routing and button states
            this.PropertyChanged += SoftwareAppsViewModel_PropertyChanged;
            
            // Subscribe to property changes in child view models to update button states
            WindowsAppsViewModel.PropertyChanged += ChildViewModel_PropertyChanged;
            ExternalAppsViewModel.PropertyChanged += ChildViewModel_PropertyChanged;
            
            // Subscribe to property changes in ExternalAppsViewModel to detect when ExternalAppsPackageManagerViewModel is available
            ExternalAppsViewModel.PropertyChanged += ExternalAppsViewModel_PropertyChanged;
            
            // Try to subscribe to search text changes from the package manager view model if it's already available
            SubscribeToPackageManagerSearchTextChanges();
            
            // Initial update of button states
            UpdateButtonStates();
        }

        /// <summary>
        /// Initializes child view models and prepares the view.
        /// </summary>
        [RelayCommand]
        public async Task Initialize()
        {
            StatusText = "Initializing Software Apps...";
            IsLoading = true;

            try
            {
                // Initialize WindowsAppsViewModel if not already initialized
                if (!WindowsAppsViewModel.IsInitialized)
                {
                    await WindowsAppsViewModel.LoadAppsAndCheckInstallationStatusAsync();
                }

                // Initialize ExternalAppsViewModel if not already initialized
                if (!ExternalAppsViewModel.IsInitialized)
                {
                    await ExternalAppsViewModel.LoadAppsAndCheckInstallationStatusAsync();
                }

                StatusText =
                    "Manage Windows Apps, Capabilities & Features and Install External Software";
            }
            catch (Exception ex)
            {
                StatusText = $"Error initializing: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Toggles the view mode between list view and table view based on the selected tab
        /// </summary>
        /// <param name="parameter">Optional parameter indicating the desired view mode</param>
        [RelayCommand]
        private void ToggleViewMode(object parameter)
        {
            if (IsWindowsAppsTabSelected)
            {
                // Route to WindowsAppsViewModel
                WindowsAppsViewModel.ToggleViewModeCommand.Execute(parameter);
            }
            else
            {
                // Route to ExternalAppsViewModel
                ExternalAppsViewModel.ToggleViewModeCommand.Execute(parameter);
            }
        }
        
        /// <summary>
        /// Called when the view is navigated to.
        /// </summary>
        /// <param name="parameter">Navigation parameter.</param>
        public override async void OnNavigatedTo(object parameter)
        {
            try
            {
                // Initialize when navigated to this view
                await Initialize();
            }
            catch (Exception ex)
            {
                StatusText = $"Error during navigation: {ex.Message}";
                // Log the error or handle it appropriately
            }
        }
        
        /// <summary>
        /// Command to select a tab
        /// </summary>
        /// <param name="parameter">"True" for Windows Apps tab, "False" for External Apps tab</param>
        [RelayCommand]
        public void SelectTab(object parameter)
        {
            bool isWindowsAppsTab = true;
            
            // Handle both string and bool parameters
            if (parameter is string strParam)
            {
                isWindowsAppsTab = bool.Parse(strParam);
            }
            else if (parameter is bool boolParam)
            {
                isWindowsAppsTab = boolParam;
            }
            
            // Update tab selection state
            IsWindowsAppsTabSelected = isWindowsAppsTab;
            IsExternalAppsTabSelected = !isWindowsAppsTab;
            
            // Update content visibility
            WindowsAppsContentVisibility = isWindowsAppsTab ? Visibility.Visible : Visibility.Collapsed;
            ExternalAppsContentVisibility = isWindowsAppsTab ? Visibility.Collapsed : Visibility.Visible;
            
            // Update the Remove button text based on the selected tab
            RemoveButtonText = isWindowsAppsTab ? "Remove Selected Items" : "Clear Selection";
            
            // Apply current search text to the newly selected tab
            RouteSearchTextToActiveViewModel();
            
            // Explicitly update button states based on the newly selected tab's view model
            UpdateButtonStates();
        }
        
        /// <summary>
        /// Handles property changes to route search text to the appropriate view model
        /// </summary>
        private void SoftwareAppsViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // When search text changes, route it to the active view model
            if (e.PropertyName == nameof(SearchText))
            {
                RouteSearchTextToActiveViewModel();
            }
            else if (e.PropertyName == nameof(IsWindowsAppsTabSelected) || 
                     e.PropertyName == nameof(IsExternalAppsTabSelected))
            {
                UpdateButtonStates();
            }
        }
        
        /// <summary>
        /// Routes the current search text to the active view model
        /// </summary>
        private void RouteSearchTextToActiveViewModel()
        {
            // Clear search text from both view models first to avoid stale search results
            WindowsAppsViewModel.SearchText = string.Empty;
            ExternalAppsViewModel.SearchText = string.Empty;
            
            // Route search text to the active view model
            if (IsWindowsAppsTabSelected)
            {
                WindowsAppsViewModel.SearchText = SearchText;
            }
            else
            {
                // Always route search text to ExternalAppsViewModel when in External Apps tab
                ExternalAppsViewModel.SearchText = SearchText;
                
                // If in package manager mode, also route search directly to the package manager view model
                if (ExternalAppsViewModel.IsPackageManagerViewMode &&
                    ExternalAppsViewModel.ExternalAppsPackageManagerViewModel != null)
                {
                    // Temporarily unsubscribe from the event to prevent infinite loop
                    ExternalAppsViewModel.ExternalAppsPackageManagerViewModel.SearchTextChanged -= OnPackageManagerSearchTextChanged;
                    
                    ExternalAppsViewModel.ExternalAppsPackageManagerViewModel.SearchText = SearchText;
                    
                    // Re-subscribe to the event
                    ExternalAppsViewModel.ExternalAppsPackageManagerViewModel.SearchTextChanged += OnPackageManagerSearchTextChanged;
                    
                    // If search text is not empty, trigger the search command
                    if (!string.IsNullOrWhiteSpace(SearchText))
                    {
                        ExternalAppsViewModel.ExternalAppsPackageManagerViewModel.ExecuteSearchCommand();
                    }
                }
                else if (ExternalAppsViewModel.IsPackageManagerViewMode)
                {
                    // If package manager view model is not available yet, ensure subscription happens when it becomes available
                    SubscribeToPackageManagerSearchTextChanges();
                }
            }
        }
        
        /// <summary>
        /// Handles property changes in child view models to update button states
        /// </summary>
        private void ChildViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Update button states when items are selected/deselected in child view models
            if (e.PropertyName?.Contains("Selected") == true || e.PropertyName == nameof(WindowsAppsViewModel.HasSelectedItems) || e.PropertyName == nameof(ExternalAppsViewModel.HasSelectedItems))
            {
                UpdateButtonStates();
            }
        }
        
        /// <summary>
        /// Updates the states of the Install and Remove buttons based on the active tab
        /// and selected items in the active view model
        /// </summary>
        private void UpdateButtonStates()
        {
            bool oldCanInstallItems = CanInstallItems;
            bool oldCanRemoveItems = CanRemoveItems;
            
            if (IsWindowsAppsTabSelected)
            {
                // For Windows Apps tab, both Install and Remove buttons can be enabled
                // Use the HasSelectedItems property we added to WindowsAppsViewModel
                CanInstallItems = WindowsAppsViewModel.HasSelectedItems;
                CanRemoveItems = WindowsAppsViewModel.HasSelectedItems;
                RemoveButtonText = "Remove Selected Items";
            }
            else
            {
                // For External Apps tab, both Install and Clear Selection buttons should be enabled if items are selected
                // Use the HasSelectedItems property we added to ExternalAppsViewModel
                CanInstallItems = ExternalAppsViewModel.HasSelectedItems;
                CanRemoveItems = ExternalAppsViewModel.HasSelectedItems; // Enable the Clear Selection button when items are selected
                RemoveButtonText = "Clear Selection";
            }
            
            // Force command CanExecute to be re-evaluated if the button states changed
            if (oldCanInstallItems != CanInstallItems)
            {
                InstallSelectedItemsCommand.NotifyCanExecuteChanged();
            }
            
            if (oldCanRemoveItems != CanRemoveItems)
            {
                RemoveSelectedItemsCommand.NotifyCanExecuteChanged();
            }
        }
        
        /// <summary>
        /// Command to install selected items in the active view model
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanInstallItems))]
        private async Task InstallSelectedItems()
        {
            if (IsWindowsAppsTabSelected)
            {
                // Route to WindowsAppsViewModel's InstallApps command
                await WindowsAppsViewModel.InstallApps();
            }
            else
            {
                // Route to ExternalAppsViewModel's InstallApps command
                await ExternalAppsViewModel.InstallApps();
            }
            
            // Update button states after operation completes
            UpdateButtonStates();
        }
        
        /// <summary>
        /// Command to remove selected items in the active view model or clear selection based on the active tab
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanRemoveItems))]
        private async Task RemoveSelectedItems()
        {
            if (IsWindowsAppsTabSelected)
            {
                // Route to WindowsAppsViewModel's RemoveApps command
                await WindowsAppsViewModel.RemoveApps();
            }
            else
            {
                // For External apps, clear selection
                ExternalAppsViewModel.ClearSelectedItems();
            }
            
            // Force update button states after operation completes
            UpdateButtonStates();
        }
        
        /// <summary>
        /// Command to show or hide help content for the active tab
        /// </summary>
        [RelayCommand]
        private void ShowHelp()
        {   
            if (HelpButtonElement == null)
            {
                return;
            }
            
            // Toggle help visibility
            if (IsHelpVisible)
            {
                // If help is currently visible, close it
                HelpService.CloseCurrentPopup();
                IsHelpVisible = false;
                return;
            }
            
            // Set help as visible
            IsHelpVisible = true;
            
            if (IsWindowsAppsTabSelected)
            {   
                // Create the Windows Apps help content
                var helpContent = new WindowsAppsHelpContent();
                
                // Show the help content in a popup
                HelpService.ShowHelp(helpContent, HelpButtonElement);
            }
            else
            {   
                // Create the External Apps help content
                var helpContent = new ExternalAppsHelpContent();
                
                // Show the help content in a popup
                HelpService.ShowHelp(helpContent, HelpButtonElement);
            }
        }
        
        /// <summary>
        /// Handles search text changes from the package manager view model
        /// </summary>
        /// <param name="searchText">The new search text</param>
        private void OnPackageManagerSearchTextChanged(string searchText)
        {
            // Update the main search text to keep both search boxes in sync
            // This will trigger RouteSearchTextToActiveViewModel through the PropertyChanged event
            SearchText = searchText;
        }
        
        /// <summary>
        /// Handles property changes in the ExternalAppsViewModel to detect when ExternalAppsPackageManagerViewModel becomes available
        /// </summary>
        private void ExternalAppsViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ExternalAppsViewModel.ExternalAppsPackageManagerViewModel))
            {
                SubscribeToPackageManagerSearchTextChanges();
            }
        }
        
        /// <summary>
        /// Subscribes to search text changes from the package manager view model if it's available
        /// </summary>
        private void SubscribeToPackageManagerSearchTextChanges()
        {
            if (ExternalAppsViewModel.ExternalAppsPackageManagerViewModel != null)
            {
                // Unsubscribe first to prevent duplicate subscriptions
                ExternalAppsViewModel.ExternalAppsPackageManagerViewModel.SearchTextChanged -= OnPackageManagerSearchTextChanged;
                // Subscribe to the event
                ExternalAppsViewModel.ExternalAppsPackageManagerViewModel.SearchTextChanged += OnPackageManagerSearchTextChanged;
            }
        }
    }
}

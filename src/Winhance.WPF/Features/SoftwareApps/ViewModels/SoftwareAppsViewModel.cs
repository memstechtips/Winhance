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
        private bool _isWindowsAppsTableViewMode = false;
        
        [ObservableProperty]
        private bool _isExternalAppsTableViewMode = false;
        
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
        
        // Removed duplicate ToggleViewMode method

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

            // Initialize parent view mode properties based on child view models
            IsWindowsAppsTableViewMode = WindowsAppsViewModel.IsTableViewMode;
            IsExternalAppsTableViewMode = ExternalAppsViewModel.IsTableViewMode;
            
            // Subscribe to property changes to handle search text routing and button states
            this.PropertyChanged += SoftwareAppsViewModel_PropertyChanged;
            
            // Subscribe to property changes in child view models to update button states
            WindowsAppsViewModel.PropertyChanged += ChildViewModel_PropertyChanged;
            ExternalAppsViewModel.PropertyChanged += ChildViewModel_PropertyChanged;
            
            // Initial update of UI states
            UpdateButtonStates();
            UpdateChildViewModels();
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
            // If a parameter is provided, use it to set the view mode directly
            if (parameter != null)
            {
                // Handle both bool and string parameters
                if (parameter is bool tableViewMode)
                {
                    if (IsWindowsAppsTabSelected)
                    {
                        IsWindowsAppsTableViewMode = tableViewMode;
                    }
                    else
                    {
                        IsExternalAppsTableViewMode = tableViewMode;
                    }
                }
                else if (parameter is string stringParam)
                {
                    // Parse string parameter ("True" or "False")
                    if (bool.TryParse(stringParam, out bool result))
                    {
                        if (IsWindowsAppsTabSelected)
                        {
                            IsWindowsAppsTableViewMode = result;
                        }
                        else
                        {
                            IsExternalAppsTableViewMode = result;
                        }
                    }
                }
            }
            // Otherwise toggle the current mode
            else
            {
                if (IsWindowsAppsTabSelected)
                {
                    IsWindowsAppsTableViewMode = !IsWindowsAppsTableViewMode;
                }
                else
                {
                    IsExternalAppsTableViewMode = !IsExternalAppsTableViewMode;
                }
            }
            
            // Apply changes to child view models
            UpdateChildViewModels();
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
            else if (e.PropertyName == nameof(IsWindowsAppsTableViewMode) ||
                     e.PropertyName == nameof(IsExternalAppsTableViewMode))
            {
                // Update child view models when table view mode changes
                UpdateChildViewModels();
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
        /// Updates the child view models' table view mode properties based on the parent's properties
        /// and ensures table data is properly populated when in table view mode
        /// </summary>
        private void UpdateChildViewModels()
        {
            // Update WindowsAppsViewModel's IsTableViewMode property
            if (WindowsAppsViewModel.IsTableViewMode != IsWindowsAppsTableViewMode)
            {
                // First update the property value
                WindowsAppsViewModel.IsTableViewMode = IsWindowsAppsTableViewMode;
                
                // Force update the collection for the table view if switching to table view
                if (IsWindowsAppsTableViewMode)
                {
                    // Force a full refresh of data for the table view
                    WindowsAppsViewModel.UpdateAllItemsCollection();
                    System.Diagnostics.Debug.WriteLine("WindowsAppsViewModel: Forced table view refresh");
                }
            }
            
            // Update ExternalAppsViewModel's IsTableViewMode property
            if (ExternalAppsViewModel.IsTableViewMode != IsExternalAppsTableViewMode)
            {
                // First update the property value
                ExternalAppsViewModel.IsTableViewMode = IsExternalAppsTableViewMode;
                
                // Force update the collection for the table view if switching to table view
                if (IsExternalAppsTableViewMode)
                {
                    // Force a full refresh of data for the table view
                    ExternalAppsViewModel.UpdateAllItemsCollection();
                    System.Diagnostics.Debug.WriteLine("ExternalAppsViewModel: Forced table view refresh");
                }
            }
        }
        

    }
}

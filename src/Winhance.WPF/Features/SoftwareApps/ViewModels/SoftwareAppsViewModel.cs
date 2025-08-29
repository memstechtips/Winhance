using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.WPF.Features.Common.Services;
using Winhance.WPF.Features.Common.ViewModels;
using Winhance.WPF.Features.Common.Views;
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
        private readonly AppViewModeManager _viewModeManager;
        private readonly ITaskProgressService _progressService;

        /// <summary>
        /// Gets whether the view is in table view mode
        /// </summary>
        public bool IsTableViewMode
        {
            get => _viewModeManager.IsTableViewMode;
            set => _viewModeManager.IsTableViewMode = value;
        }

        /// <summary>
        /// Gets the visibility for the grid view
        /// </summary>
        public Visibility GridViewVisibility => _viewModeManager.GridViewVisibility;

        /// <summary>
        /// Gets the visibility for the table view
        /// </summary>
        public Visibility TableViewVisibility => _viewModeManager.TableViewVisibility;

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

        [ObservableProperty]
        private bool _isHelpFlyoutVisible = false;

        [ObservableProperty]
        private double _helpFlyoutLeft = 0;

        [ObservableProperty]
        private double _helpFlyoutTop = 0;

        [ObservableProperty]
        private bool _shouldFocusHelpOverlay = false;

        [ObservableProperty]
        private bool _isHelpButtonActive = false;

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
            _progressService = progressService;
            _packageManager =
                packageManager ?? throw new ArgumentNullException(nameof(packageManager));
            _serviceProvider =
                serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _viewModeManager = new AppViewModeManager();

            // Resolve the dependencies via DI container
            WindowsAppsViewModel = _serviceProvider.GetRequiredService<WindowsAppsViewModel>();
            ExternalAppsViewModel = _serviceProvider.GetRequiredService<ExternalAppsViewModel>();

            // Initialize view mode based on active tab's child view model
            _viewModeManager.IsTableViewMode = IsWindowsAppsTabSelected
                ? WindowsAppsViewModel.IsTableViewMode
                : ExternalAppsViewModel.IsTableViewMode;

            // Subscribe to view mode changes
            _viewModeManager.ViewModeChanged += OnViewModeChanged;

            // Subscribe to property changes to handle search text routing and button states
            this.PropertyChanged += SoftwareAppsViewModel_PropertyChanged;

            // Subscribe to property changes in child view models to update button states
            WindowsAppsViewModel.PropertyChanged += ChildViewModel_PropertyChanged;
            ExternalAppsViewModel.PropertyChanged += ChildViewModel_PropertyChanged;

            // Initial update of UI states
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

                // Subscribe to selection change events
                WindowsAppsViewModel.SelectedItemsChanged -= ChildViewModel_SelectedItemsChanged;
                WindowsAppsViewModel.SelectedItemsChanged += ChildViewModel_SelectedItemsChanged;

                // Initialize ExternalAppsViewModel if not already initialized
                if (!ExternalAppsViewModel.IsInitialized)
                {
                    await ExternalAppsViewModel.LoadAppsAndCheckInstallationStatusAsync();
                }

                // Subscribe to selection change events
                ExternalAppsViewModel.SelectedItemsChanged -= ChildViewModel_SelectedItemsChanged;
                ExternalAppsViewModel.SelectedItemsChanged += ChildViewModel_SelectedItemsChanged;

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
            bool newViewMode;
            // If a parameter is provided, use it to set the view mode directly
            if (parameter != null)
            {
                // Handle both bool and string parameters
                if (parameter is bool tableViewMode)
                {
                    newViewMode = tableViewMode;
                }
                else if (
                    parameter is string stringParam
                    && bool.TryParse(stringParam, out bool result)
                )
                {
                    newViewMode = result;
                }
                else
                {
                    // Default to current state if parameter can't be parsed
                    newViewMode = _viewModeManager.IsTableViewMode;
                }
            }
            else
            {
                // Toggle the current mode if no parameter is provided
                newViewMode = !_viewModeManager.IsTableViewMode;
            }

            // First update the view mode manager
            _viewModeManager.IsTableViewMode = newViewMode;

            // Then update the active child view model
            if (IsWindowsAppsTabSelected)
            {
                // Update Windows Apps view model and explicitly notify the change
                WindowsAppsViewModel.IsTableViewMode = newViewMode;

                // Make sure content template selector gets notified of the change
                OnPropertyChanged(nameof(WindowsAppsViewModel));
                OnPropertyChanged(nameof(IsTableViewMode));
            }
            else
            {
                // Update External Apps view model and explicitly notify the change
                ExternalAppsViewModel.IsTableViewMode = newViewMode;

                // Make sure content template selector gets notified of the change
                OnPropertyChanged(nameof(ExternalAppsViewModel));
                OnPropertyChanged(nameof(IsTableViewMode));
            }

            // Notify property changes for visibility properties
            OnPropertyChanged(nameof(GridViewVisibility));
            OnPropertyChanged(nameof(TableViewVisibility));

            // Force button state update after view mode synchronization
            UpdateButtonStates();
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
            WindowsAppsContentVisibility = isWindowsAppsTab
                ? Visibility.Visible
                : Visibility.Collapsed;
            ExternalAppsContentVisibility = isWindowsAppsTab
                ? Visibility.Collapsed
                : Visibility.Visible;

            // Update the Remove button text based on the selected tab
            RemoveButtonText = isWindowsAppsTab ? "Remove Selected Items" : "Clear Selection";

            // Apply current search text to the newly selected tab
            RouteSearchTextToActiveViewModel();

            // Update the parent view mode to match the newly selected tab's view model
            _viewModeManager.IsTableViewMode = isWindowsAppsTab
                ? WindowsAppsViewModel.IsTableViewMode
                : ExternalAppsViewModel.IsTableViewMode;

            // Explicitly update button states based on the newly selected tab's view model
            UpdateButtonStates();
        }

        /// <summary>
        /// Handles property changes to route search text to the appropriate view model
        /// </summary>
        private void SoftwareAppsViewModel_PropertyChanged(
            object sender,
            PropertyChangedEventArgs e
        )
        {
            // When search text changes, route it to the active view model
            if (e.PropertyName == nameof(SearchText))
            {
                RouteSearchTextToActiveViewModel();
            }
            else if (
                e.PropertyName == nameof(IsWindowsAppsTabSelected)
                || e.PropertyName == nameof(IsExternalAppsTabSelected)
            )
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
            }
        }

        /// <summary>
        /// Handles property changes in child view models to update button states
        /// </summary>
        private void ChildViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Update button states when items are selected/deselected in child view models
            if (
                e.PropertyName?.Contains("Selected") == true
                || e.PropertyName == nameof(WindowsAppsViewModel.HasSelectedItems)
                || e.PropertyName == nameof(ExternalAppsViewModel.HasSelectedItems)
            )
            {
                UpdateButtonStates();
            }
        }

        /// <summary>
        /// Updates the states of the Install and Remove buttons based on the active tab
        /// and selected items in the active view model
        /// </summary>
        private bool _isUpdatingButtonStates = false;

        private void UpdateButtonStates()
        {
            // Guard against recursive calls
            if (_isUpdatingButtonStates)
            {
                return;
            }

            try
            {
                // Set flag to prevent recursive calls
                _isUpdatingButtonStates = true;

                bool oldCanInstallItems = CanInstallItems;
                bool oldCanRemoveItems = CanRemoveItems;

                if (IsWindowsAppsTabSelected)
                {
                    // For Windows Apps tab, both Install and Remove buttons can be enabled
                    // Use the HasSelectedItems property we added to WindowsAppsViewModel

                    var hasSelected = WindowsAppsViewModel.HasSelectedItems;

                    CanInstallItems = hasSelected;
                    CanRemoveItems = hasSelected;
                    RemoveButtonText = "Remove Selected Items";
                }
                else if (IsExternalAppsTabSelected)
                {
                    // For External Apps tab, both Install and Clear Selection buttons should be enabled if items are selected
                    // Use the HasSelectedItems property we added to ExternalAppsViewModel

                    var hasSelected = ExternalAppsViewModel.HasSelectedItems;

                    CanInstallItems = hasSelected;
                    CanRemoveItems = hasSelected; // Enable the Clear Selection button when items are selected
                    RemoveButtonText = "Clear Selection";
                }
                else
                {
                    // Fallback - should not happen but added for safety
                    CanInstallItems = false;
                    CanRemoveItems = false;
                }

                // Always notify of changes
                OnPropertyChanged(nameof(CanInstallItems));
                OnPropertyChanged(nameof(CanRemoveItems));

                if (oldCanInstallItems != CanInstallItems)
                {
                    InstallSelectedItemsCommand.NotifyCanExecuteChanged();
                }

                if (oldCanRemoveItems != CanRemoveItems)
                {
                    RemoveSelectedItemsCommand.NotifyCanExecuteChanged();
                }
            }
            finally
            {
                // Reset flag when we're done to allow future updates
                _isUpdatingButtonStates = false;
            }
        }

        /// <summary>
        /// Command to install selected items in the active view model
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanInstallSelectedItems))]
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
        /// Determines whether the InstallSelectedItems command can be executed
        /// </summary>
        /// <returns>True if items can be installed, false otherwise</returns>
        private bool CanInstallSelectedItems()
        {
            return CanInstallItems;
        }

        /// <summary>
        /// Command to remove selected items in the active view model or clear selection based on the active tab
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanRemoveSelectedItems))]
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
        /// Determines whether the RemoveSelectedItems command can be executed
        /// </summary>
        /// <returns>True if items can be removed, false otherwise</returns>
        private bool CanRemoveSelectedItems()
        {
            return CanRemoveItems;
        }

        /// <summary>
        /// Command to show help content for the active tab as a flyout
        /// </summary>
        [RelayCommand]
        private void ShowHelp()
        {
            if (IsWindowsAppsTabSelected)
            {
                // Create Windows Apps help content
                var logService = _serviceProvider.GetRequiredService<ILogService>();
                var scheduledTaskService = _serviceProvider.GetRequiredService<IScheduledTaskService>();
                var scriptPathDetectionService = _serviceProvider.GetRequiredService<IScriptPathDetectionService>();

                var viewModel = new WindowsAppsHelpContentViewModel(
                    scriptPathDetectionService,
                    scheduledTaskService,
                    logService
                );

                var helpContent = new WindowsAppsHelpContent(viewModel);
                CurrentHelpContent = helpContent;
            }
            else
            {
                // Create External Apps help content with a simple ViewModel for the close command
                var helpContent = new ExternalAppsHelpContent();

                // Create a simple ViewModel with CloseHelpCommand
                var viewModel = new ExternalAppsHelpViewModel { CloseHelpCommand = HideHelpFlyoutCommand };
                helpContent.DataContext = viewModel;

                CurrentHelpContent = helpContent;
            }

            // Calculate position relative to help button and show the flyout
            CalculateHelpFlyoutPosition();
            IsHelpFlyoutVisible = true;
            IsHelpButtonActive = true; // Highlight the Help button

            // Trigger focus on the overlay to enable keyboard input
            ShouldFocusHelpOverlay = !ShouldFocusHelpOverlay; // Toggle to trigger property change
        }

        /// <summary>
        /// Command to hide the help flyout
        /// </summary>
        [RelayCommand]
        private void HideHelpFlyout()
        {
            IsHelpFlyoutVisible = false;
            IsHelpButtonActive = false; // Remove highlight from Help button

            // Dispose ViewModels if they implement IDisposable
            if (CurrentHelpContent is UserControl helpControl &&
                helpControl.DataContext is IDisposable disposableViewModel)
            {
                disposableViewModel.Dispose();
            }

            CurrentHelpContent = null;
        }

        /// <summary>
        /// Calculates the position for the help flyout relative to the help button
        /// </summary>
        private void CalculateHelpFlyoutPosition()
        {
            if (HelpButtonElement == null) return;

            try
            {
                // Find the SoftwareAppsView container
                var softwareAppsView = FindAncestorOfType<UserControl>(HelpButtonElement);
                if (softwareAppsView == null) return;

                // Get the position of the help button relative to the SoftwareAppsView
                var buttonPosition = HelpButtonElement.TransformToAncestor(softwareAppsView).Transform(new Point(0, 0));

                // Position the flyout to the left of the button, aligned to its bottom
                // This creates an "attached" appearance similar to MoreMenuFlyout
                HelpFlyoutLeft = buttonPosition.X - 520; // Offset to position to the left (flyout width ~500 + margin)
                HelpFlyoutTop = buttonPosition.Y + HelpButtonElement.ActualHeight + 5; // Below the button with small gap

                // Ensure the flyout doesn't go off-screen within the SoftwareAppsView
                if (softwareAppsView.ActualWidth > 0 && softwareAppsView.ActualHeight > 0)
                {
                    // Adjust horizontal position if it would go off the left edge
                    if (HelpFlyoutLeft < 20)
                    {
                        HelpFlyoutLeft = 20;
                    }

                    // Adjust horizontal position if it would go off the right edge
                    if (HelpFlyoutLeft + 520 > softwareAppsView.ActualWidth - 20)
                    {
                        HelpFlyoutLeft = softwareAppsView.ActualWidth - 540;
                    }

                    // Adjust vertical position if it would go off the bottom edge
                    if (HelpFlyoutTop + 450 > softwareAppsView.ActualHeight - 20)
                    {
                        // Position above the button instead
                        HelpFlyoutTop = buttonPosition.Y - 455; // Above the button with small gap

                        // Ensure it doesn't go off the top
                        if (HelpFlyoutTop < 20)
                        {
                            HelpFlyoutTop = 20;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Fallback to center positioning if calculation fails
                var logService = _serviceProvider.GetRequiredService<ILogService>();
                logService.LogWarning($"Failed to calculate help flyout position: {ex.Message}");

                // Use positioning near the top-right as fallback (where help button typically is)
                HelpFlyoutLeft = 200; // Reasonable offset from left
                HelpFlyoutTop = 100;  // Reasonable offset from top
            }
        }

        /// <summary>
        /// Helper method to find an ancestor of a specific type
        /// </summary>
        private static T FindAncestorOfType<T>(DependencyObject element) where T : DependencyObject
        {
            var parent = VisualTreeHelper.GetParent(element);
            while (parent != null)
            {
                if (parent is T)
                    return (T)parent;
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }



        /// <summary>
        /// Finds the actual main window for setting as Owner of modal dialogs
        /// </summary>
        /// <returns>The main window, or null if not found</returns>
        private Window FindMainWindow()
        {
            // First try Application.Current.MainWindow
            if (
                Application.Current?.MainWindow != null
                && Application.Current.MainWindow.GetType().Name != "ModalDialog"
            )
            {
                return Application.Current.MainWindow;
            }

            // If that doesn't work, find the first non-modal window
            if (Application.Current?.Windows != null)
            {
                foreach (Window window in Application.Current.Windows)
                {
                    // Skip modal dialogs and find the main application window
                    if (
                        window.GetType().Name != "ModalDialog"
                        && !string.IsNullOrEmpty(window.Title)
                        && window.Title.Contains("Winhance")
                    )
                    {
                        return window;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Event handler for selection changes in child view models
        /// </summary>
        private void ChildViewModel_SelectedItemsChanged(object sender, EventArgs e)
        {
            // Update button states when selection changes in either child view model
            UpdateButtonStates();
        }

        /// <summary>
        /// Handles view mode changes from the view mode manager
        /// </summary>
        private void OnViewModeChanged(bool isTableViewMode)
        {
            // Set table view mode on the active child view model only
            if (IsWindowsAppsTabSelected)
            {
                if (WindowsAppsViewModel.IsTableViewMode != isTableViewMode)
                {
                    WindowsAppsViewModel.IsTableViewMode = isTableViewMode;
                }
            }
            else if (IsExternalAppsTabSelected)
            {
                if (ExternalAppsViewModel.IsTableViewMode != isTableViewMode)
                {
                    ExternalAppsViewModel.IsTableViewMode = isTableViewMode;
                }
            }
        }
    }
}

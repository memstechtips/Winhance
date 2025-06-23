using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Interfaces;
using Winhance.WPF.Features.Common.ViewModels;
using Winhance.WPF.Features.SoftwareApps.Models;
using System.Diagnostics;

namespace Winhance.WPF.Features.SoftwareApps.ViewModels
{
    /// <summary>
    /// ViewModel for displaying and managing WinGet search results.
    /// </summary>
    public partial class WinGetSearchResultsViewModel : BaseViewModel
    {
        private readonly IWinGetInstaller _winGetInstaller;
        private readonly IAppInstallationService _appInstallationService;
        private readonly ITaskProgressService _taskProgressService;
        private readonly IInternetConnectivityService _connectivityService;
        
        // Debounce timer for search functionality
        private CancellationTokenSource _debounceTokenSource;
        private readonly int _debounceDelayMs = 1000; // 1 second delay
        
        [ObservableProperty]
        private bool _isExpanded = false;
        
        [ObservableProperty]
        private bool _hasResults = false;
        
        [ObservableProperty]
        private bool _isAllSelected = false;
        
        [ObservableProperty]
        private string _statusText = string.Empty;
        
        [ObservableProperty]
        private ObservableCollection<WinGetSearchResult> _searchResults = new();
        
        private ICollectionView _searchResultsView;
        
        /// <summary>
        /// Gets a value indicating whether there are any selected items.
        /// </summary>
        public bool HasSelectedItems => SearchResults.Any(item => item.IsSelected);
        
        /// <summary>
        /// Gets the collection view for search results.
        /// </summary>
        public ICollectionView SearchResultsView => _searchResultsView ??= CollectionViewSource.GetDefaultView(SearchResults);
        
        /// <summary>
        /// Initializes a new instance of the <see cref="WinGetSearchResultsViewModel"/> class.
        /// </summary>
        public WinGetSearchResultsViewModel(
            IWinGetInstaller winGetInstaller,
            IAppInstallationService appInstallationService,
            ITaskProgressService taskProgressService,
            IInternetConnectivityService connectivityService)
            : base(taskProgressService)
        {
            _winGetInstaller = winGetInstaller ?? throw new ArgumentNullException(nameof(winGetInstaller));
            _appInstallationService = appInstallationService ?? throw new ArgumentNullException(nameof(appInstallationService));
            _taskProgressService = taskProgressService ?? throw new ArgumentNullException(nameof(taskProgressService));
            _connectivityService = connectivityService ?? throw new ArgumentNullException(nameof(connectivityService));
            
            // Initialize the SearchResults collection
            SearchResults = new ObservableCollection<WinGetSearchResult>();
            
            // Subscribe to collection changed events
            SearchResults.CollectionChanged += SearchResults_CollectionChanged;
        }
        
        /// <summary>
        /// Handles changes to the search results collection.
        /// </summary>
        private void SearchResults_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Log collection changes
            System.Diagnostics.Debug.WriteLine($"SearchResults_CollectionChanged: Action={e.Action}, NewItems={(e.NewItems?.Count ?? 0)}, OldItems={(e.OldItems?.Count ?? 0)}, Current Count={_searchResults.Count}");
            
            // Subscribe to property changed events for new items
            if (e.NewItems != null)
            {
                foreach (WinGetSearchResult item in e.NewItems)
                {
                    item.PropertyChanged -= Item_PropertyChanged;
                    item.PropertyChanged += Item_PropertyChanged;
                }
            }
            
            // Unsubscribe from property changed events for removed items
            if (e.OldItems != null)
            {
                foreach (WinGetSearchResult item in e.OldItems)
                {
                    item.PropertyChanged -= Item_PropertyChanged;
                }
            }
            
            // Update HasSelectedItems property
            OnPropertyChanged(nameof(HasSelectedItems));
            
            // Update HasResults property
            HasResults = SearchResults.Count > 0;
            System.Diagnostics.Debug.WriteLine($"SearchResults_CollectionChanged: HasResults set to {HasResults} based on count {SearchResults.Count}");
        }
        
        /// <summary>
        /// Handles property changes for search result items.
        /// </summary>
        private void Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(WinGetSearchResult.IsSelected))
            {
                OnPropertyChanged(nameof(HasSelectedItems));
            }
        }
        
        /// <summary>
        /// Handles changes to the IsAllSelected property.
        /// </summary>
        partial void OnIsAllSelectedChanged(bool value)
        {
            // Apply the selection state to all items
            foreach (var item in SearchResults)
            {
                item.IsSelected = value;
            }
        }
        
        /// <summary>
        /// Performs a search using WinGet with debounce functionality.
        /// </summary>
        [RelayCommand]
        public async Task SearchAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                ClearResults();
                return;
            }
            
            // Cancel any previous debounce operation
            _debounceTokenSource?.Cancel();
            _debounceTokenSource = new CancellationTokenSource();
            var token = _debounceTokenSource.Token;
            
            // Set status to indicate we're waiting
            StatusText = $"Waiting to search for '{query}'...";
            
            try
            {
                // Wait for the debounce delay
                await Task.Delay(_debounceDelayMs, token);
                
                // If cancelled during the delay, exit early
                if (token.IsCancellationRequested)
                {
                    return;
                }
                
                IsLoading = true;
                StatusText = $"Searching for '{query}' using WinGet...";
                
                // Try to install WinGet if not already installed
                StatusText = "Checking if WinGet is installed...";
                bool installed = await _winGetInstaller.TryInstallWinGetAsync();
                if (!installed)
                {
                    StatusText = "Failed to install WinGet. Search cannot be performed.";
                    return;
                }
                
                // Clear previous results
                SearchResults.Clear();
                
                // Perform the search
                var results = await _winGetInstaller.SearchPackagesAsync(
                    query,
                    new SearchOptions { Count = 50 },
                    token);
                
                // If cancelled during the search, exit early
                if (token.IsCancellationRequested)
                {
                    return;
                }
                
                // Clear existing results first to ensure a clean slate
                SearchResults.Clear();
                
                // Add results to the collection
                foreach (var result in results)
                {
                    SearchResults.Add(new WinGetSearchResult(
                        result.Name,
                        result.Id,
                        result.Version,
                        result.Source));
                }
                
                // Update status and HasResults property
                bool hasAnyResults = SearchResults.Count > 0;
                Debug.WriteLine($"WinGetSearchResultsViewModel: Setting HasResults to {hasAnyResults} based on count {SearchResults.Count}");
                HasResults = hasAnyResults;
                
                if (SearchResults.Count > 0)
                {
                    StatusText = $"Found {SearchResults.Count} results for '{query}'";
                    IsExpanded = true;
                    Debug.WriteLine($"WinGetSearchResultsViewModel: Found {SearchResults.Count} results for '{query}'. HasResults={HasResults}");
                    foreach (var result in SearchResults.Take(3))
                    {
                        Debug.WriteLine($"  - {result.Name} ({result.Id})");
                    }
                }
                else
                {
                    StatusText = $"No results found for '{query}'";
                    Debug.WriteLine($"WinGetSearchResultsViewModel: No results found for '{query}'. HasResults={HasResults}");
                }
            }
            catch (OperationCanceledException)
            {
                // Search was cancelled, do nothing
            }
            catch (Exception ex)
            {
                StatusText = $"Error searching for '{query}': {ex.Message}";
            }
            finally
            {
                // Only update IsLoading if this is the most recent search
                if (!token.IsCancellationRequested)
                {
                    IsLoading = false;
                }
            }
        }
        
        /// <summary>
        /// Clears the search results.
        /// </summary>
        [RelayCommand]
        public void ClearResults()
        {
            // Cancel any pending search
            _debounceTokenSource?.Cancel();
            _debounceTokenSource = null;
            
            SearchResults.Clear();
            StatusText = string.Empty;
            IsExpanded = false;
            HasResults = false;
        }
        
        /// <summary>
        /// Installs the selected items.
        /// </summary>
        [RelayCommand]
        public async Task InstallSelectedItemsAsync()
        {
            var selectedItems = SearchResults.Where(item => item.IsSelected).ToList();
            if (selectedItems.Count == 0)
            {
                return;
            }
            
            // Check for internet connectivity
            bool isInternetConnected = await _connectivityService.IsInternetConnectedAsync(true);
            if (!isInternetConnected)
            {
                StatusText = "No internet connection available. Installation cannot proceed.";
                return;
            }
            
            IsLoading = true;
            StatusText = $"Installing {selectedItems.Count} selected items...";
            
            try
            {
                int successCount = 0;
                
                foreach (var item in selectedItems)
                {
                    StatusText = $"Installing {item.Name}...";
                    
                    var progressAdapter = new Progress<TaskProgressDetail>(progress => 
                    {
                        _taskProgressService.UpdateDetailedProgress(progress);
                    });
                    
                    // Execute the installation with progress reporting
                    var result = await ExecuteWithProgressAsync<InstallationResult>(
                        async (progress, token) => await _winGetInstaller.InstallPackageAsync(
                            item.Id,
                            null, // Default installation options
                            item.Name,
                            token),
                        $"Installing {item.Name}",
                        false);
                    
                    bool success = result.Success;
                    
                    if (success)
                    {
                        successCount++;
                    }
                }
                
                StatusText = $"Successfully installed {successCount} of {selectedItems.Count} items";
            }
            catch (Exception ex)
            {
                StatusText = $"Error during installation: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        /// <summary>
        /// Clears the selection of all items.
        /// </summary>
        [RelayCommand]
        public void ClearSelection()
        {
            IsAllSelected = false;
            foreach (var item in SearchResults)
            {
                item.IsSelected = false;
            }
        }
        
        /// <summary>
        /// Installs a WinGet package by ID.
        /// </summary>
        /// <param name="packageId">The ID of the package to install.</param>
        /// <param name="packageName">The name of the package for display purposes.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>The installation result.</returns>
        public async Task<InstallationResult> InstallWinGetPackageAsync(string packageId, string packageName, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(packageId))
            {
                return new InstallationResult
                {
                    Success = false,
                    Message = "Package ID cannot be null or empty."
                };
            }
            
            try
            {
                // Use the WinGet installer service to install the package
                return await _winGetInstaller.InstallPackageAsync(
                    packageId,
                    null, // Default installation options
                    packageName,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw; // Let the caller handle cancellation
            }
            catch (Exception ex)
            {
                return new InstallationResult
                {
                    Success = false,
                    Message = $"Failed to install {packageName}: {ex.Message}"
                };
            }
        }
    }
}

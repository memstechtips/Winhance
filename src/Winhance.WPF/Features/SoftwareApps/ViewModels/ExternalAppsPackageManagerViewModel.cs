using System;
using System.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.ViewModels;

namespace Winhance.WPF.Features.SoftwareApps.ViewModels
{
    /// <summary>
    /// ViewModel for displaying and managing package manager search results.
    /// </summary>
    public partial class ExternalAppsPackageManagerViewModel : BaseViewModel
    {
        [ObservableProperty]
        private WinGetSearchResultsViewModel _winGetSearchResultsViewModel;

        [ObservableProperty]
        private bool _hasSearchResults = false;
        
        [ObservableProperty]
        private bool _hasSearchBeenAttempted = false;
        
        [ObservableProperty]
        private bool _showSearchResults = false;
        
        /// <summary>
        /// Updates the ShowSearchResults property based on current state.
        /// </summary>
        private void UpdateShowSearchResults()
        {
            var newValue = HasSearchBeenAttempted && HasSearchResults && WinGetSearchResultsViewModel?.SearchResults.Count > 0;
            System.Diagnostics.Debug.WriteLine($"UpdateShowSearchResults: {newValue} (HasSearchBeenAttempted={HasSearchBeenAttempted}, HasSearchResults={HasSearchResults}, SearchResultsCount={WinGetSearchResultsViewModel?.SearchResults.Count ?? 0})");
            ShowSearchResults = newValue;
        }

        [ObservableProperty]
        private string _statusText = string.Empty;

        private string _searchText = string.Empty;
        
        /// <summary>
        /// Gets or sets the search text.
        /// </summary>
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    // Notify the parent SoftwareAppsViewModel about the search text change
                    SearchTextChanged?.Invoke(value);
                }
            }
        }
        
        /// <summary>
        /// Event raised when the search text changes.
        /// </summary>
        public event Action<string> SearchTextChanged;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExternalAppsPackageManagerViewModel"/> class.
        /// </summary>
        public ExternalAppsPackageManagerViewModel(
            ITaskProgressService taskProgressService,
            IServiceProvider serviceProvider)
            : base(taskProgressService)
        {
            // Initialize WinGetSearchResultsViewModel
            WinGetSearchResultsViewModel = serviceProvider.GetRequiredService<WinGetSearchResultsViewModel>();

            // Subscribe to property changes in the WinGetSearchResultsViewModel
            WinGetSearchResultsViewModel.PropertyChanged += WinGetSearchResultsViewModel_PropertyChanged;
        
            // Initialize the search command
            SearchCommand = new RelayCommand(ExecuteSearch);
        }

        /// <summary>
        /// Handles property changes in the WinGetSearchResultsViewModel.
        /// </summary>
        private void WinGetSearchResultsViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // When HasResults changes in WinGetSearchResultsViewModel, update our HasSearchResults property
            if (e.PropertyName == nameof(WinGetSearchResultsViewModel.HasResults))
            {
                HasSearchResults = WinGetSearchResultsViewModel.HasResults;
                System.Diagnostics.Debug.WriteLine($"WinGetSearchResultsViewModel_PropertyChanged: HasResults changed to {WinGetSearchResultsViewModel.HasResults}");
                
                // Ensure HasSearchBeenAttempted is true when we have results
                if (WinGetSearchResultsViewModel.HasResults)
                {
                    HasSearchBeenAttempted = true;
                }
                
                // Update ShowSearchResults
                UpdateShowSearchResults();
            }

            // When StatusText changes in WinGetSearchResultsViewModel, update our StatusText property
            if (e.PropertyName == nameof(WinGetSearchResultsViewModel.StatusText))
            {
                StatusText = WinGetSearchResultsViewModel.StatusText;
            }
            
            // When SearchResults collection changes, update our HasSearchResults property
            if (e.PropertyName == nameof(WinGetSearchResultsViewModel.SearchResults))
            {
                bool hasResults = WinGetSearchResultsViewModel.SearchResults.Count > 0;
                System.Diagnostics.Debug.WriteLine($"WinGetSearchResultsViewModel_PropertyChanged: SearchResults changed, count={WinGetSearchResultsViewModel.SearchResults.Count}, setting HasSearchResults to {hasResults}");
                
                HasSearchResults = hasResults;
                
                // Update ShowSearchResults
                UpdateShowSearchResults();
            }
        }

        /// <summary>
        /// Gets the command that executes a search.
        /// </summary>
        public ICommand SearchCommand { get; }

        /// <summary>
        /// Executes the search command using the current SearchText.
        /// </summary>
        private void ExecuteSearch()
        {
            ApplySearch(SearchText);
        }
        
        /// <summary>
        /// Public method to execute the search command from other view models.
        /// </summary>
        public void ExecuteSearchCommand()
        {
            ApplySearch(SearchText);
        }
    
        /// <summary>
        /// Applies a search query to the WinGetSearchResultsViewModel.
        /// </summary>
        /// <param name="query">The search query to apply.</param>
        public void ApplySearch(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                WinGetSearchResultsViewModel.ClearResults();
                HasSearchBeenAttempted = false;
                HasSearchResults = false;
                UpdateShowSearchResults();
                return;
            }

            // Mark that a search has been attempted
            HasSearchBeenAttempted = true;
            UpdateShowSearchResults();
            
            // Execute WinGet search asynchronously
            _ = WinGetSearchResultsViewModel.SearchAsync(query).ContinueWith(task =>
            {
                // Update HasSearchResults after the search completes
                HasSearchResults = WinGetSearchResultsViewModel.HasResults;
                System.Diagnostics.Debug.WriteLine($"ApplySearch: HasSearchResults set to {HasSearchResults} based on WinGetSearchResultsViewModel.HasResults");
                
                // Ensure UI updates happen on the UI thread
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    UpdateShowSearchResults();
                });
            });
        }
    }
}

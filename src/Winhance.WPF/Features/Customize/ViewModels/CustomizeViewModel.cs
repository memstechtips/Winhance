using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Services;
using Winhance.WPF.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Services;

namespace Winhance.WPF.Features.Customize.ViewModels
{
    public partial class CustomizeViewModel : ObservableObject, IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ISearchTextCoordinationService _searchTextCoordinationService;

        public ObservableCollection<Control> FeatureViews { get; } = new();

        [ObservableProperty]
        private string _statusText = "Customize Your Windows Appearance and Behaviour";

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private bool _hasSearchResults = true;

        public CustomizeViewModel(
            IServiceProvider serviceProvider,
            ISearchTextCoordinationService searchTextCoordinationService
        )
        {
            _serviceProvider = serviceProvider;
            _searchTextCoordinationService = searchTextCoordinationService;

            _searchTextCoordinationService.SearchTextChanged += OnSearchTextChanged;
            InitializeFeaturesAsync();
        }

        private async void InitializeFeaturesAsync()
        {
            try
            {
                var features = FeatureRegistry.GetFeaturesForCategory("Customize");
                if (features == null || !features.Any())
                    return;

                foreach (var feature in features.OrderBy(f => f.SortOrder))
                {
                    var composedView = await FeatureViewModelFactory.CreateFeatureAsync(feature, _serviceProvider);
                    if (composedView != null)
                        FeatureViews.Add(composedView);
                }
            }
            catch
            {
            }
        }

        private void OnSearchTextChanged(object sender, SearchTextChangedEventArgs e)
        {
            HasSearchResults = FeatureViews.Any(view =>
                view.DataContext is IFeatureViewModel vm && vm.HasVisibleSettings
            );
        }

        partial void OnSearchTextChanged(string value) =>
            _searchTextCoordinationService.UpdateSearchText(value ?? string.Empty);

        public void Dispose()
        {
            _searchTextCoordinationService.SearchTextChanged -= OnSearchTextChanged;
            GC.SuppressFinalize(this);
        }
    }
}

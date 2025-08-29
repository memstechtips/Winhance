using System.Collections.ObjectModel;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Services;
using Winhance.WPF.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Services;

namespace Winhance.WPF.Features.Common.ViewModels
{
    public abstract partial class BaseCategoryViewModel : ObservableObject, IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ISearchTextCoordinationService _searchTextCoordinationService;

        public ObservableCollection<Control> FeatureViews { get; } = new();

        [ObservableProperty]
        private string _statusText;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private bool _hasSearchResults = true;

        protected abstract string CategoryName { get; }
        protected abstract string DefaultStatusText { get; }

        protected BaseCategoryViewModel(
            IServiceProvider serviceProvider,
            ISearchTextCoordinationService searchTextCoordinationService)
        {
            _serviceProvider = serviceProvider;
            _searchTextCoordinationService = searchTextCoordinationService;
            _statusText = DefaultStatusText;

            _searchTextCoordinationService.SearchTextChanged += OnSearchTextChanged;
            InitializeFeaturesAsync();
        }

        private async void InitializeFeaturesAsync()
        {
            try
            {
                var features = FeatureRegistry.GetFeaturesForCategory(CategoryName);
                if (features == null || !features.Any())
                    return;

                foreach (var feature in features.OrderBy(f => f.SortOrder))
                {
                    var composedView = await FeatureViewModelFactory.CreateFeatureAsync(
                        feature,
                        _serviceProvider
                    );
                    if (composedView != null)
                        FeatureViews.Add(composedView);
                }
            }
            catch { }
        }

        private void OnSearchTextChanged(object sender, SearchTextChangedEventArgs e)
        {
            foreach (var view in FeatureViews)
            {
                if (view.DataContext is IFeatureViewModel featureVm)
                {
                    featureVm.ApplySearchFilter(e.SearchText);
                }
            }

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
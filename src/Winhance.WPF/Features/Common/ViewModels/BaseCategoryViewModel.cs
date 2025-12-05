using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Services;
using Winhance.WPF.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Extensions;
using Winhance.WPF.Features.Common.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Winhance.WPF.Features.Common.ViewModels
{
    public abstract partial class BaseCategoryViewModel : BaseContainerViewModel, IInitializableViewModel, IPreloadableViewModel
    {
        private bool _isDisposed;
        private bool _isFeaturesCached;
        protected readonly IViewPoolService viewPoolService;

        public ObservableCollection<Control> FeatureViews { get; } = new();
        public ObservableCollection<QuickNavItem> QuickNavItems { get; } = new();

        [ObservableProperty]
        private bool _hasSearchResults = true;

        [ObservableProperty]
        private double _scrollPosition;

        [ObservableProperty]
        private QuickNavItem? _selectedNavItem;

        public ICommand NavigateToFeatureCommand { get; }
        public ICommand UpdateScrollPositionCommand { get; }
        public ICommand InitializeCommand { get; }

        protected abstract string CategoryName { get; }

        public override string ModuleId => CategoryName;
        public override string DisplayName => CategoryName;

        protected BaseCategoryViewModel(
            IServiceProvider serviceProvider,
            ISearchTextCoordinationService searchTextCoordinationService,
            IViewPoolService viewPoolService)
            : base(serviceProvider, searchTextCoordinationService)
        {
            this.viewPoolService = viewPoolService;
            NavigateToFeatureCommand = new RelayCommand<QuickNavItem>(NavigateToFeature);
            UpdateScrollPositionCommand = new RelayCommand<double>(UpdateScrollPosition);
            InitializeCommand = new RelayCommand(Initialize);
        }

        public void Initialize()
        {
            StatusText = DefaultStatusText;
            searchTextCoordinationService.SearchTextChanged += OnSearchTextChanged;
        }

        public virtual async Task PreloadFeaturesAsync()
        {

            if (_isFeaturesCached && FeatureViews.Any())
            {
                return;
            }

            var features = FeatureRegistry.GetFeaturesForCategory(CategoryName);
            if (features == null || !features.Any())
            {
                return;
            }

            if (FeatureViews.Any())
            {
                FeatureViews.Clear();
            }

            var featureTasks = features
                .OrderBy(f => f.SortOrder)
                .Select(async feature =>
                {
                    var composedView = await FeatureViewModelFactory.CreateFeatureAsync(
                        feature,
                        serviceProvider,
                        viewPoolService
                    );
                    return new { Feature = feature, View = composedView };
                });

            var results = await Task.WhenAll(featureTasks);

            foreach (var result in results.Where(r => r.View != null).OrderBy(r => r.Feature.SortOrder))
            {
                result.View.Tag = result.Feature;
                FeatureViews.Add(result.View);
            }

            _isFeaturesCached = true;
            PopulateQuickNavItems();
        }

        protected override void OnSearchTextChanged(object sender, SearchTextChangedEventArgs e)
        {
            base.OnSearchTextChanged(sender, e);

            foreach (var view in FeatureViews)
            {
                if (view.DataContext is ISettingsFeatureViewModel settingsVm)
                {
                    settingsVm.ApplySearchFilter(e.SearchText);
                }
                else if (view.DataContext is BaseFeatureViewModel appVm)
                {
                    appVm.SearchText = e.SearchText;
                }
            }

            HasSearchResults = FeatureViews.Any(view =>
                (view.DataContext is ISettingsFeatureViewModel settingsVm && settingsVm.HasVisibleSettings) ||
                (view.DataContext is BaseFeatureViewModel appVm && !string.IsNullOrEmpty(appVm.SearchText))
            );
        }

        partial void OnScrollPositionChanged(double value) =>
            UpdateSelectedNavItemFromScroll(value);

        private void PopulateQuickNavItems()
        {
            QuickNavItems.Clear();
            var localizationService = serviceProvider.GetRequiredService<ILocalizationService>();

            foreach (var view in FeatureViews)
            {
                if (view.DataContext is ISettingsFeatureViewModel featureVm && view.Tag is FeatureMetadata feature)
                {
                    var navItem = new QuickNavItem
                    {
                        DisplayName = localizationService.GetString(feature.LocalizationKey),
                        ViewModelType = featureVm.GetType(),
                        TargetView = view as UserControl,
                        ViewModel = featureVm,
                        SortOrder = feature.SortOrder
                    };
                    QuickNavItems.Add(navItem);
                }
            }

            if (QuickNavItems.Any())
            {
                SelectedNavItem = QuickNavItems.First();
                SelectedNavItem.IsSelected = true;
            }
        }

        private void NavigateToFeature(QuickNavItem? navItem)
        {
            if (navItem?.TargetView == null) return;

            foreach (var item in QuickNavItems)
                item.IsSelected = false;

            navItem.IsSelected = true;
            SelectedNavItem = navItem;

            var scrollViewer = navItem.TargetView.FindVisualParent<ScrollViewer>();
            if (scrollViewer != null)
            {
                var transform = navItem.TargetView.TransformToAncestor(scrollViewer);
                var position = transform.Transform(new System.Windows.Point(0, 0));
                scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + position.Y);
            }
            else
            {
                navItem.TargetView.BringIntoView();
            }
        }

        private void UpdateScrollPosition(double position)
        {
            ScrollPosition = position;
        }

        private void UpdateSelectedNavItemFromScroll(double scrollPosition)
        {
            if (!QuickNavItems.Any()) return;

            var scrollViewer = QuickNavItems.First().TargetView?.FindVisualParent<ScrollViewer>();
            if (scrollViewer != null)
            {
                if (scrollViewer.VerticalOffset <= 5)
                {
                    var firstItem = QuickNavItems.First();
                    if (firstItem != SelectedNavItem)
                    {
                        foreach (var item in QuickNavItems) item.IsSelected = false;
                        firstItem.IsSelected = true;
                        SelectedNavItem = firstItem;
                    }
                    return;
                }

                if (scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight - 5)
                {
                    var lastItem = QuickNavItems.Last();
                    if (lastItem != SelectedNavItem)
                    {
                        foreach (var item in QuickNavItems) item.IsSelected = false;
                        lastItem.IsSelected = true;
                        SelectedNavItem = lastItem;
                    }
                    return;
                }
            }

            if (SelectedNavItem != null)
            {
                var currentPos = GetItemScrollPosition(SelectedNavItem);
                if (currentPos.HasValue && currentPos >= -30 && currentPos <= 200)
                    return;
            }

            var bestItem = QuickNavItems
                .Where(item => item.TargetView != null)
                .Select(item => new
                {
                    Item = item,
                    Position = GetItemScrollPosition(item) ?? double.MaxValue,
                    ContentOverlap = CalculateContentOverlap(item)
                })
                .Where(x => x.Position >= -2000 && x.Position <= 400)
                .OrderByDescending(x => x.ContentOverlap)
                .ThenBy(x => Math.Abs(x.Position))
                .FirstOrDefault()?.Item;

            if (bestItem != null && bestItem != SelectedNavItem)
            {
                foreach (var item in QuickNavItems) item.IsSelected = false;
                bestItem.IsSelected = true;
                SelectedNavItem = bestItem;
            }
        }

        private double? GetItemScrollPosition(QuickNavItem item)
        {
            if (item.TargetView == null) return null;

            var scrollViewer = item.TargetView.FindVisualParent<ScrollViewer>();
            if (scrollViewer == null) return null;

            var transform = item.TargetView.TransformToAncestor(scrollViewer);
            var position = transform.Transform(new System.Windows.Point(0, 0));
            return position.Y;
        }

        private double CalculateContentOverlap(QuickNavItem item)
        {
            var headerPos = GetItemScrollPosition(item) ?? double.MaxValue;
            if (headerPos == double.MaxValue) return 0;

            var contentStart = headerPos;
            var contentEnd = headerPos + 500;

            var viewportStart = 0;
            var viewportEnd = 600;

            var overlapStart = Math.Max(contentStart, viewportStart);
            var overlapEnd = Math.Min(contentEnd, viewportEnd);

            return Math.Max(0, overlapEnd - overlapStart);
        }


        public void OnNavigatedTo(object parameter = null) { }

        public void OnNavigatedFrom() { }

        public async Task RefreshAllFeaturesAsync()
        {
            foreach (var view in FeatureViews)
            {
                if (view.DataContext is ISettingsFeatureViewModel settingsVm)
                {
                    await settingsVm.RefreshSettingsAsync();
                }
            }
        }

        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected override void Dispose(bool disposing)
        {
            if (!_isDisposed && disposing)
            {

                searchTextCoordinationService.SearchTextChanged -= OnSearchTextChanged;

                _isFeaturesCached = false;
                int disposedCount = 0;
                foreach (var view in FeatureViews.ToList())
                {
                    if (view.DataContext is IDisposable disposableVm)
                    {
                        var vmType = disposableVm.GetType().Name;
                        var vmHash = disposableVm.GetHashCode();
                        disposableVm.Dispose();
                        disposedCount++;
                    }

                    var viewType = view.GetType();
                    viewPoolService.ReturnView(viewType, view, clearDataContext: true);
                }


                FeatureViews.Clear();
                QuickNavItems.Clear();

                _isDisposed = true;
            }

            base.Dispose(disposing);
        }
    }
}
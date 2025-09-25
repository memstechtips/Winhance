using System;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Services;
using Winhance.WPF.Features.Common.ViewModels;

namespace Winhance.WPF.Features.Common.ViewModels
{
    public abstract partial class BaseContainerViewModel : BaseFeatureViewModel, IDisposable
    {
        protected readonly IServiceProvider serviceProvider;
        protected readonly ISearchTextCoordinationService searchTextCoordinationService;
        private bool _isDisposed;

        [ObservableProperty]
        private string _statusText = string.Empty;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private FrameworkElement? _helpButtonElement;

        [ObservableProperty]
        private bool _shouldFocusHelpOverlay;

        public ICommand HideHelpFlyoutCommand { get; }

        protected abstract string DefaultStatusText { get; }

        protected BaseContainerViewModel(
            IServiceProvider serviceProvider,
            ISearchTextCoordinationService searchTextCoordinationService)
            : base()
        {
            this.serviceProvider = serviceProvider;
            this.searchTextCoordinationService = searchTextCoordinationService;
            HideHelpFlyoutCommand = new RelayCommand(HideHelpFlyout);
        }

        protected virtual void Initialize()
        {
            StatusText = DefaultStatusText;
            searchTextCoordinationService.SearchTextChanged += OnSearchTextChanged;
        }

        protected virtual void OnSearchTextChanged(object sender, SearchTextChangedEventArgs e)
        {
            SearchText = e.SearchText ?? string.Empty;
        }

        partial void OnSearchTextChanged(string value) =>
            searchTextCoordinationService.UpdateSearchText(value ?? string.Empty);

        private void HideHelpFlyout()
        {
            ShouldFocusHelpOverlay = false;
        }

        public virtual void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed && disposing)
            {
                searchTextCoordinationService.SearchTextChanged -= OnSearchTextChanged;
                _isDisposed = true;
            }
        }

        ~BaseContainerViewModel()
        {
            Dispose(false);
        }
    }
}
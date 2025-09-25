using CommunityToolkit.Mvvm.ComponentModel;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.WPF.Features.Common.ViewModels
{
    public abstract partial class BaseFeatureViewModel : BaseViewModel, IFeatureViewModel
    {
        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private bool _isLoading = false;

        public abstract string ModuleId { get; }
        public abstract string DisplayName { get; }
        public virtual bool IsVisibleInSearch => true;

        protected BaseFeatureViewModel()
        {
        }

        public virtual void ApplySearchFilter(string searchText)
        {
            SearchText = searchText ?? string.Empty;
        }

        partial void OnSearchTextChanged(string value)
        {
            OnSearchTextChangedCore(value);
        }

        protected virtual void OnSearchTextChangedCore(string value)
        {
        }
    }
}
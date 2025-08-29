using System.Collections.ObjectModel;
using System.Windows.Input;
using Winhance.WPF.Features.Common.ViewModels;

namespace Winhance.WPF.Features.Common.Interfaces
{
    public interface IFeatureViewModel
    {
        string ModuleId { get; }

        string DisplayName { get; }

        ObservableCollection<SettingItemViewModel> Settings { get; }

        bool HasVisibleSettings { get; }

        string SearchText { get; set; }

        bool IsLoading { get; }

        int SettingsCount { get; }

        string Category { get; }

        string Description { get; }

        int SortOrder { get; }

        event EventHandler<FeatureVisibilityChangedEventArgs> VisibilityChanged;

        void ApplySearchFilter(string searchText);

        bool IsVisibleInSearch { get; }

        ICommand LoadSettingsCommand { get; }

        Task LoadSettingsAsync();

    }

    public class FeatureVisibilityChangedEventArgs : EventArgs
    {
        public string FeatureId { get; }
        public bool IsVisible { get; }
        public string SearchText { get; }

        public FeatureVisibilityChangedEventArgs(
            string featureId,
            bool isVisible,
            string searchText
        )
        {
            FeatureId = featureId;
            IsVisible = isVisible;
            SearchText = searchText;
        }
    }
}

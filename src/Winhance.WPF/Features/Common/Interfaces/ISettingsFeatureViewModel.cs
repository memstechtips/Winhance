using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Winhance.Core.Features.Common.Models;
using Winhance.WPF.Features.Common.ViewModels;

namespace Winhance.WPF.Features.Common.Interfaces
{
    public interface ISettingsFeatureViewModel : Core.Features.Common.Interfaces.IFeatureViewModel,IDisposable
    {
        ObservableCollection<SettingItemViewModel> Settings { get; }
        bool HasVisibleSettings { get; }
        int SettingsCount { get; }
        ICommand LoadSettingsCommand { get; }
        Task LoadSettingsAsync();
        Task<bool> HandleDomainContextSettingAsync(SettingDefinition setting, object? value, bool additionalContext = false);
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

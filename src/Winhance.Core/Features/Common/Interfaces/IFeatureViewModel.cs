namespace Winhance.Core.Features.Common.Interfaces
{
    public interface IFeatureViewModel
    {
        void OnNavigatedTo(object? parameter = null);
        void OnNavigatedFrom();
        string ModuleId { get; }
        string DisplayName { get; }
        string SearchText { get; set; }
        bool IsLoading { get; }
        bool IsVisibleInSearch { get; }
        void ApplySearchFilter(string searchText);
    }
}
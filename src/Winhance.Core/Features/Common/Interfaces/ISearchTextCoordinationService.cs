namespace Winhance.Core.Features.Common.Interfaces
{
    public interface ISearchTextCoordinationService
    {
        event EventHandler<SearchTextChangedEventArgs> SearchTextChanged;

        string CurrentSearchText { get; }

        void UpdateSearchText(string searchText);
    }

    public class SearchTextChangedEventArgs : EventArgs
    {
        public string SearchText { get; }
        public string PreviousSearchText { get; }

        public SearchTextChangedEventArgs(string searchText, string previousSearchText)
        {
            SearchText = searchText;
            PreviousSearchText = previousSearchText;
        }
    }
}
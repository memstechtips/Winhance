using CommunityToolkit.Mvvm.ComponentModel;

namespace Winhance.WPF.Features.SoftwareApps.Models
{
    /// <summary>
    /// Represents a search result from WinGet.
    /// </summary>
    public partial class WinGetSearchResult : ObservableObject
    {
        [ObservableProperty]
        private string _name;

        [ObservableProperty]
        private string _id;

        [ObservableProperty]
        private string _version;

        [ObservableProperty]
        private string _source;

        [ObservableProperty]
        private bool _isSelected;

        /// <summary>
        /// Creates a new instance of WinGetSearchResult.
        /// </summary>
        public WinGetSearchResult(string name, string id, string version, string source)
        {
            _name = name;
            _id = id;
            _version = version;
            _source = source;
            _isSelected = false;
        }
    }
}

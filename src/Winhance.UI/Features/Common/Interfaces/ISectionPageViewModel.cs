using System.Collections.ObjectModel;
using System.ComponentModel;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Common.ViewModels;

namespace Winhance.UI.Features.Common.Interfaces;

// The non-generic face of SectionPageViewModel<T>: everything the shared page infrastructure reads off a
// section page's ViewModel. Exists because WinUI 3 XAML has no x:TypeArguments, so the shared page base cannot
// be generic. Add a member here when the shell binds it, and not before.
public interface ISectionPageViewModel : INotifyPropertyChanged
{
    string CurrentSectionKey { get; set; }

    bool IsInDetailPage { get; }

    string CurrentSectionName { get; }

    string SearchText { get; set; }

    bool IsLoading { get; }

    bool IsNotLoading { get; }

    string PageTitle { get; }

    string PageDescription { get; }

    string BreadcrumbRootText { get; }

    string SearchPlaceholder { get; }

    ObservableCollection<SearchSuggestionItem> SearchSuggestions { get; }

    bool HasNoSearchResults { get; }

    // One item per section, in declaration order - the page iterates these instead of a static Sections list plus
    // a key-to-ViewModel lookup, so "the sections of this page" has one meaning.
    IReadOnlyList<SectionOverviewItemViewModel> OverviewItems { get; }

    SectionOverviewItemViewModel? CurrentSectionItem { get; }

    // Idempotent.
    Task InitializeAsync();

    void OnNavigatedFrom();

    string GetSectionDisplayName(string sectionKey);

    string? FindSectionForSetting(string settingId);
}

using System.Collections.ObjectModel;
using System.ComponentModel;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Common.ViewModels;

namespace Winhance.UI.Features.Common.Interfaces;

/// <summary>
/// The non-generic face of <c>SectionPageViewModel&lt;TSectionInfo&gt;</c>: everything the shared
/// page infrastructure — <see cref="Controls.SectionPage"/> and
/// <see cref="Controls.SectionPageShell"/> — reads off a section page's ViewModel.
///
/// It exists because WinUI 3 XAML has no <c>x:TypeArguments</c>, so a shared page base cannot be
/// generic and therefore cannot hold a <c>SectionPageViewModel&lt;T&gt;</c> reference.
///
/// <para>The presentation members below (PageTitle, SearchPlaceholder, …) were once left off
/// deliberately, because each page's XAML bound them against its own concrete <c>ViewModel</c>
/// property and never through this interface. Extracting that markup into one shared shell
/// removed the concrete type from the binding path — the shell holds an
/// <c>ISectionPageViewModel</c> — so the contract now has to carry them. Add a member here when
/// the shell binds it, and not before.</para>
/// </summary>
public interface ISectionPageViewModel : INotifyPropertyChanged
{
    /// <summary>The open section's key, or "Overview" when the section list is showing.</summary>
    string CurrentSectionKey { get; set; }

    /// <summary>False while the overview is showing.</summary>
    bool IsInDetailPage { get; }

    /// <summary>Localized display name of the open section.</summary>
    string CurrentSectionName { get; }

    /// <summary>The live search text; the page re-applies it when clearing a filter.</summary>
    string SearchText { get; set; }

    /// <summary>True while the feature ViewModels are loading; the detail page binds its spinner.</summary>
    bool IsLoading { get; }

    /// <summary>Inverse of <see cref="IsLoading"/>; the overview list binds its visibility.</summary>
    bool IsNotLoading { get; }

    /// <summary>Localized page heading, shown beside the page icon.</summary>
    string PageTitle { get; }

    /// <summary>Localized one-line subtitle under the heading.</summary>
    string PageDescription { get; }

    /// <summary>Label on the breadcrumb's root button — the page's own name.</summary>
    string BreadcrumbRootText { get; }

    /// <summary>Placeholder text for the search box.</summary>
    string SearchPlaceholder { get; }

    /// <summary>
    /// Live search matches for the current <see cref="SearchText"/>.
    ///
    /// Typed as the concrete collection because interface implementation in C# requires an exact
    /// type match, and the implementer's generated <c>[ObservableProperty]</c> member is an
    /// <see cref="ObservableCollection{T}"/>.
    /// </summary>
    ObservableCollection<SearchSuggestionItem> SearchSuggestions { get; }

    /// <summary>True when a search is active and the open section has nothing matching it.</summary>
    bool HasNoSearchResults { get; }

    /// <summary>
    /// One item per section, in declaration order. The page iterates these instead of a static
    /// Sections list plus a key-to-ViewModel lookup, so "the sections of this page" has one meaning.
    /// </summary>
    IReadOnlyList<SectionOverviewItemViewModel> OverviewItems { get; }

    /// <summary>The open section's item, or null on the overview.</summary>
    SectionOverviewItemViewModel? CurrentSectionItem { get; }

    /// <summary>Loads every feature ViewModel. Idempotent.</summary>
    Task InitializeAsync();

    /// <summary>Clears transient per-visit state (currently the search text).</summary>
    void OnNavigatedFrom();

    /// <summary>Localized display name for a section key, or "Overview" when unknown.</summary>
    string GetSectionDisplayName(string sectionKey);

    /// <summary>
    /// Which of this page's sections holds a setting, or null when none does — the setting lives on
    /// the other page, or under an id this page has never loaded.
    /// </summary>
    string? FindSectionForSetting(string settingId);
}

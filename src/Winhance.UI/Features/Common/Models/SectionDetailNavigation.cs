using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Models;

/// <summary>
/// The navigation parameter a section page passes when opening a detail page.
///
/// It carries the ViewModels rather than having the detail page resolve them, because a single
/// detail page type serves both Optimize and Customize and cannot know which one hosts it. The
/// host does know, so it says. Detail pages set <c>NavigationCacheMode="Disabled"</c>, so each
/// navigation builds a fresh page and these references never outlive the visit.
/// </summary>
/// <param name="SectionKey">The section being opened; the host reads this back in Navigated.</param>
/// <param name="SearchText">Pre-applied filter, or null when the user opened the section directly.</param>
/// <param name="PageViewModel">The host page's ViewModel — supplies loading and no-results state.</param>
/// <param name="Feature">The feature whose settings this page lists.</param>
public sealed record SectionDetailNavigation(
    string SectionKey,
    string? SearchText,
    ISectionPageViewModel PageViewModel,
    ISettingsFeatureViewModel Feature);

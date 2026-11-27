using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Models;

// Carries the ViewModels rather than having the detail page resolve them, because one detail page type serves
// both Optimize and Customize and cannot know which hosts it. Detail pages set NavigationCacheMode="Disabled",
// so each navigation builds a fresh page and these references never outlive the visit.
public sealed record SectionDetailNavigation(
    string SectionKey,
    string? SearchText,
    ISectionPageViewModel PageViewModel,
    ISettingsFeatureViewModel Feature);

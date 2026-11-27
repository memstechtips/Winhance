using FluentAssertions;
using Xunit;
using static Winhance.UI.Tests.Conventions.ConventionSource;

namespace Winhance.UI.Tests.Conventions;

// A Visibility toggle keeps the hidden subtree built and in memory, which is what creating an element only when it
// is shown avoids. Every hide site left in the UI carries the reason a toggle is the right tool there.
public class VisibilityConventionTests
{
    [Fact]
    public void VisibilityHideSites_MatchTheLedger()
    {
        // XAML: a Visibility="..." attribute, literal or bound (attached Foo.Visibility and x:-prefixed forms excluded),
        // a Setter with Target="x.Visibility" or Property="Visibility", and a storyboard animating Visibility.
        // C#: an assignment to any Visibility member, including object initialisers; == and => do not count.
        var actual = Merge(
            Count(XamlUi, @"(?<![\w.:])Visibility="""),
            Count(XamlUi, @"<Setter\b[^>]*\bTarget=""[^""]*\.Visibility"""),
            Count(XamlUi, @"<Setter\b[^>]*\bProperty=""Visibility"""),
            Count(XamlUi, @"\bStoryboard\.TargetProperty=""(?:\(UIElement\.)?Visibility\)?"""),
            Count(CsUi, @"(?<![\w])Visibility\s*=(?![=>])"));

        Ledger.AssertMatches("Visibility", actual,
            "create the element only when it is shown (x:Load or built in code) instead of toggling Visibility");
    }

    // Leaving a section page clears its search, and the filter lands on a debounce tick after the list has gone, so
    // an x:Load on what the filter flips realizes an element with no page under it and kills the process.
    [Fact]
    public void SearchFilterFlags_NeverDriveXLoad()
    {
        var sites = Count(XamlUi, @"x:Load=""\{x:Bind\b[^""]*\b(?:EffectiveIsVisible|HasVisibleItems)\b");

        sites.Should().BeEmpty("the search filter changes these while the list may be off screen; bind Visibility instead");
    }
}

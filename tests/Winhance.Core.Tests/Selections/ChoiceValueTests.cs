using FluentAssertions;
using Winhance.Core.Features.Common.Selections;
using Xunit;

namespace Winhance.Core.Tests.Selections;

public class ChoiceValueTests
{
    // The union is closed on purpose: a switch that forgets a case is CS8509 (a warning here, not an error).
    // This pins the case list so a new nested record fails a test instead of reaching a user.
    [Fact]
    public void EveryCase_IsOneOfTheSevenKnownShapes()
    {
        var cases = typeof(ChoiceValue).GetNestedTypes().Where(t => t.IsSubclassOf(typeof(ChoiceValue))).Select(t => t.Name).OrderBy(n => n);
        cases.Should().Equal("AcDcNumber", "AcDcOption", "CustomValues", "Number", "Option", "PowerPlan", "Toggle");
    }

    [Fact]
    public void ChoiceValue_CannotBeSubclassedOutsideTheFile()
    {
        typeof(ChoiceValue).GetConstructors(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public).Should().BeEmpty();
    }

    [Fact]
    public void SelectionSet_Empty_HasNoChoices()
    {
        SelectionSet.Empty.Settings.Should().BeEmpty();
        SelectionSet.Empty.WindowsApps.Should().BeEmpty();
        SelectionSet.Empty.ExternalApps.Should().BeEmpty();
        SelectionSet.Empty.Autounattend.Should().BeSameAs(AutounattendChoices.None);
    }
}

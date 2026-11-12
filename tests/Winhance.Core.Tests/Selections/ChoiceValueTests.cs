using FluentAssertions;
using Winhance.Core.Features.Common.Selections;
using Xunit;

namespace Winhance.Core.Tests.Selections;

public class ChoiceValueTests
{
    // The union is closed on purpose: a switch that forgets a case is CS8509 (a warning here, not an error).
    // This pins the case list so a new nested record fails a test instead of reaching a user.
    [Fact]
    public void EveryCase_IsOneOfTheTenKnownShapes()
    {
        var cases = typeof(ChoiceValue).GetNestedTypes().Where(t => t.IsSubclassOf(typeof(ChoiceValue))).Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal);
        cases.Should().Equal("AcDcNumber", "AcDcOption", "CheckBox", "CustomValues", "Keyed", "Keys", "List", "Number", "Option", "Text", "Toggle");
    }

    [Fact]
    public void ChoiceValue_CannotBeSubclassedOutsideTheFile()
    {
        typeof(ChoiceValue).GetConstructors(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public).Should().BeEmpty();
    }

    private static ChoiceValue.ListRow Row(string name, string password = "pw") =>
        new(new Dictionary<string, string> { ["name"] = name, ["password"] = password, ["group"] = "0" });

    [Fact]
    public void Equals_TablesWithEqualRows_AreEqual()
    {
        var one = new ChoiceValue.List([Row("a1"), Row("a2")]);
        var two = new ChoiceValue.List([Row("a1"), Row("a2")]);

        one.Should().Be(two);
        one.GetHashCode().Should().Be(two.GetHashCode());
    }

    [Fact]
    public void Equals_TablesWithADifferentField_AreNotEqual()
    {
        var one = new ChoiceValue.List([Row("a1")]);
        var two = new ChoiceValue.List([Row("a1", password: "other")]);

        one.Should().NotBe(two);
    }

    [Fact]
    public void Equals_RowFieldsInAnotherOrder_AreEqual()
    {
        var one = new ChoiceValue.ListRow(new Dictionary<string, string> { ["name"] = "a1", ["group"] = "0" });
        var two = new ChoiceValue.ListRow(new Dictionary<string, string> { ["group"] = "0", ["name"] = "a1" });

        one.Should().Be(two);
        one.GetHashCode().Should().Be(two.GetHashCode());
    }

    [Fact]
    public void Equals_RowWithAnExtraField_IsADifferentRow()
    {
        var one = new ChoiceValue.ListRow(new Dictionary<string, string> { ["name"] = "a1" });
        var two = new ChoiceValue.ListRow(new Dictionary<string, string> { ["name"] = "a1", ["group"] = "0" });

        one.Should().NotBe(two);
    }

    // The card compares a read-back with what it wrote; left out of equality, a tick would look like no change.
    [Fact]
    public void Equals_OnlyOneTableSavesPasswords_AreNotEqual()
    {
        var one = new ChoiceValue.List([Row("a1")], SavePasswords: true);
        var two = new ChoiceValue.List([Row("a1")]);

        one.Should().NotBe(two);
    }

    [Fact]
    public void Equals_BothTablesSavePasswords_AreEqual()
    {
        var one = new ChoiceValue.List([Row("a1")], SavePasswords: true);
        var two = new ChoiceValue.List([Row("a1")], SavePasswords: true);

        one.Should().Be(two);
        one.GetHashCode().Should().Be(two.GetHashCode());
    }

    [Fact]
    public void SelectionSet_Empty_HasNoChoices()
    {
        SelectionSet.Empty.Settings.Should().BeEmpty();
        SelectionSet.Empty.WindowsApps.Should().BeEmpty();
        SelectionSet.Empty.ExternalApps.Should().BeEmpty();
        SelectionSet.Empty.Files.Should().BeEmpty();
    }
}

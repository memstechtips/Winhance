using FluentAssertions;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Selections;
using Xunit;
using Winhance.TestSupport;

namespace Winhance.Core.Tests.Catalog;

public class ListShapeTests
{
    private static readonly TextRule NameRule =
        new("^[a-z]+$", UpperCase: false, TestKeys.Of("Lower-case letters only."));

    private static Setting ListSetting() => new()
    {
        Id = "a",
        Display = new() { Name = TestKeys.Of("n"), Description = TestKeys.Of("d") },
        Targets = [new AutounattendElement("accounts", "oobeSystem", "Microsoft-Windows-Shell-Setup", "UserAccounts/LocalAccounts")],
        List = new([new Field("name", FieldKind.Text, TestKeys.Of("Name"), Rule: NameRule)]),
    };

    [Fact]
    public void Control_FieldList_DerivesListControl()
    {
        ListSetting().Control.Should().Be(ControlKind.List);
    }

    [Fact]
    public void Control_TextRuleBesideList_DerivesTextBox()
    {
        var setting = ListSetting() with { TextBox = new(NameRule) };

        setting.Control.Should().Be(ControlKind.TextBox);
    }

    [Fact]
    public void IsAnswerFileOnly_ListSetting_IsTrue()
    {
        ListSetting().IsAnswerFileOnly.Should().BeTrue();
    }

    [Fact]
    public void InputTypeFor_ListSetting_ReturnsList()
    {
        ConfigFileMapper.InputTypeFor(ListSetting()).Should().Be(InputType.List);
    }

    [Fact]
    public void List_ListSetting_NamesItsFieldsAndNoOptionCaptions()
    {
        var setting = ListSetting();

        setting.List!.Fields.Single().Rule!.Message.Value.Should().Be("Lower-case letters only.");
        setting.States.Should().BeEmpty();
    }

    // The row template draws each kind, so a new one without a template would be a blank row.
    [Fact]
    public void FieldKind_Always_NamesTheControlThatDrawsIt()
    {
        Enum.GetValues<FieldKind>().Should().Equal(
            FieldKind.Text, FieldKind.Password, FieldKind.Selection, FieldKind.CheckBox, FieldKind.File);
    }

    [Fact]
    public void AccountsList_Always_NamesItsFieldsAndWhereSignInMaySit()
    {
        var fields = SettingCatalog.Find("autounattend-accounts")!.List!.Fields;

        fields.Select(f => f.Key).Should().Equal("name", "display-name", "group", "password", "obscure", "auto-logon", "change-password");
        fields.Select(f => f.Kind).Should().Equal(
            FieldKind.Text, FieldKind.Text, FieldKind.Selection, FieldKind.Password, FieldKind.CheckBox, FieldKind.CheckBox,
            FieldKind.CheckBox);

        var autoLogon = fields.Single(f => f.Key == "auto-logon");
        autoLogon.OneRowOnly.Should().BeTrue();
        autoLogon.OnlyWhen.Should().Be(new FieldCondition("group", 0));
        fields.Where(f => f.Key != "auto-logon").Should().OnlyContain(f => !f.OneRowOnly && f.OnlyWhen == null);
    }
}

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
    public void A_field_list_derives_a_table_card()
    {
        ListSetting().Control.Should().Be(ControlKind.List);
    }

    [Fact]
    public void A_text_rule_beside_the_table_wins_the_derivation()
    {
        var setting = ListSetting() with { TextBox = new(NameRule) };

        setting.Control.Should().Be(ControlKind.TextBox);
    }

    [Fact]
    public void A_list_setting_is_answer_file_only()
    {
        ListSetting().IsAnswerFileOnly.Should().BeTrue();
    }

    [Fact]
    public void A_list_setting_is_a_table_in_the_config_file()
    {
        ConfigFileMapper.InputTypeFor(ListSetting()).Should().Be(InputType.List);
    }

    [Fact]
    public void A_list_setting_names_its_fields_and_no_option_captions()
    {
        var setting = ListSetting();

        setting.List!.Fields.Single().Rule!.Message.Value.Should().Be("Lower-case letters only.");
        setting.States.Should().BeEmpty();
    }

    // The row template draws each kind, so a new one without a template would be a blank row.
    [Fact]
    public void A_field_declares_which_control_draws_it()
    {
        Enum.GetValues<FieldKind>().Should().Equal(FieldKind.Text, FieldKind.Password, FieldKind.Selection, FieldKind.CheckBox);
    }

    [Fact]
    public void The_accounts_table_names_its_fields_and_where_the_sign_in_may_sit()
    {
        var fields = SettingCatalog.Find("autounattend-accounts")!.List!.Fields;

        fields.Select(f => f.Key).Should().Equal("name", "display-name", "group", "password", "obscure", "auto-logon");
        fields.Select(f => f.Kind).Should().Equal(
            FieldKind.Text, FieldKind.Text, FieldKind.Selection, FieldKind.Password, FieldKind.CheckBox, FieldKind.CheckBox);

        var autoLogon = fields.Single(f => f.Key == "auto-logon");
        autoLogon.OneRowOnly.Should().BeTrue();
        autoLogon.OnlyWhen.Should().Be(new FieldCondition("group", 0));
        fields.Where(f => f.Key != "auto-logon").Should().OnlyContain(f => !f.OneRowOnly && f.OnlyWhen == null);
    }
}

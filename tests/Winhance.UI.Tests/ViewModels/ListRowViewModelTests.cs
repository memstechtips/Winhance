using FluentAssertions;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Selections;
using Winhance.TestSupport;
using Winhance.UI.Features.Common.ViewModels;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

public class ListRowViewModelTests
{
    private static readonly TextRule NameRule =
        new("^[A-Za-z]+$", UpperCase: false, TestKeys.Of("Rule_Message"));

    private static readonly IReadOnlyList<Field> Fields =
    [
        new("name", FieldKind.Text, TestKeys.Of("Field_Name"), Rule: NameRule),
        new("group", FieldKind.Selection, TestKeys.Of("Field_Group"),
            Options: [TestKeys.Of("Group_Admins"), TestKeys.Of("Group_Users")], Default: "0"),
        new("password", FieldKind.Password, TestKeys.Of("Field_Password")),
        new("obscure", FieldKind.CheckBox, TestKeys.Of("Field_Obscure"), Default: "true"),
        new("sign-in", FieldKind.CheckBox, TestKeys.Of("Field_SignIn"),
            Tooltip: TestKeys.Of("Field_SignIn_Tooltip"),
            OneRowOnly: true,
            OnlyWhen: new FieldCondition("group", 0)),
    ];

    private readonly List<ListRowViewModel> _changed = [];
    private readonly List<ListRowViewModel> _removed = [];

    private ListRowViewModel Row(ChoiceValue.ListRow? values = null) =>
        new(Fields, key => key.Value, "Remove", values, _changed.Add, _removed.Add);

    [Fact]
    public void A_new_row_starts_on_the_catalog_defaults()
    {
        var row = Row();

        row.FieldFor("name")!.Text.Should().BeEmpty();
        row.FieldFor("group")!.OptionIndex.Should().Be(0);
        row.FieldFor("obscure")!.Checked.Should().BeTrue();
    }

    [Fact]
    public void Every_caption_comes_from_the_field_the_catalog_declared()
    {
        var row = Row();

        row.FieldFor("name")!.Label.Should().Be("Field_Name");
        row.FieldFor("name")!.Error.Should().Be("Rule_Message");
        row.FieldFor("group")!.Options.Should().Equal("Group_Admins", "Group_Users");
        row.FieldFor("sign-in")!.Tooltip.Should().Be("Field_SignIn_Tooltip");
        row.RemoveLabel.Should().Be("Remove");
    }

    [Fact]
    public void A_value_the_rule_refuses_is_shown_and_flagged()
    {
        var row = Row();

        row.FieldFor("name")!.Text = "not a name";

        row.FieldFor("name")!.Text.Should().Be("not a name",
            because: "the box keeps what was typed; Winhance validation reports, it never blocks");
        row.FieldFor("name")!.HasError.Should().BeTrue();
    }

    [Fact]
    public void A_field_whose_condition_is_unmet_is_disabled_and_refuses_the_tick()
    {
        var row = Row();
        row.FieldFor("group")!.OptionIndex = 1;

        row.FieldFor("sign-in")!.Checked = true;

        row.FieldFor("sign-in")!.IsEnabled.Should().BeFalse();
        row.FieldFor("sign-in")!.Checked.Should().BeFalse();
    }

    [Fact]
    public void Moving_the_named_field_away_drops_a_tick_already_taken()
    {
        var row = Row();
        row.FieldFor("sign-in")!.Checked = true;

        row.FieldFor("group")!.OptionIndex = 1;

        row.FieldFor("sign-in")!.Checked.Should().BeFalse(
            because: "the file should never carry a choice the row no longer shows");
    }

    [Fact]
    public void The_row_reports_every_field_by_its_catalog_key()
    {
        var row = Row();
        row.FieldFor("name")!.Text = "Marco";
        row.FieldFor("password")!.Text = "hunter2";
        row.FieldFor("group")!.OptionIndex = 1;

        var recorded = row.ToRow();

        recorded.Values.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["name"] = "Marco",
            ["group"] = "1",
            ["password"] = "hunter2",
            ["obscure"] = "true",
            ["sign-in"] = "false",
        });
    }

    [Fact]
    public void A_password_is_reported_exactly_as_typed()
    {
        var row = Row();
        row.FieldFor("name")!.Text = " Marco ";
        row.FieldFor("password")!.Text = " hunter2 ";

        var recorded = row.ToRow();

        recorded.Values["password"].Should().Be(" hunter2 ");
        recorded.Values["name"].Should().Be("Marco");
    }

    [Fact]
    public void A_row_built_from_a_record_comes_back_holding_it()
    {
        var row = Row(new ChoiceValue.ListRow(new Dictionary<string, string>
        {
            ["name"] = "Marco",
            ["group"] = "1",
            ["obscure"] = "false",
        }));

        row.FieldFor("name")!.Text.Should().Be("Marco");
        row.FieldFor("group")!.OptionIndex.Should().Be(1);
        row.FieldFor("obscure")!.Checked.Should().BeFalse();
        row.FieldFor("password")!.Text.Should().BeEmpty(
            because: "a key the record does not carry falls back to the catalog default, not to the last row's value");
    }

    [Fact]
    public void An_out_of_range_index_is_what_a_rebuilding_dropdown_pushes_and_is_refused()
    {
        var row = Row();

        row.FieldFor("group")!.OptionIndex = -1;

        row.FieldFor("group")!.OptionIndex.Should().Be(0);
    }

    [Fact]
    public void Every_edit_reports_the_whole_row_back_to_the_card()
    {
        var row = Row();

        row.FieldFor("name")!.Text = "Marco";
        row.FieldFor("obscure")!.Checked = false;

        _changed.Should().HaveCount(2);
        _changed.Should().OnlyContain(reported => ReferenceEquals(reported, row));
    }

    [Fact]
    public void Remove_hands_the_row_back_rather_than_removing_itself()
    {
        var row = Row();

        row.Remove();

        _removed.Should().ContainSingle().Which.Should().BeSameAs(row);
    }
}

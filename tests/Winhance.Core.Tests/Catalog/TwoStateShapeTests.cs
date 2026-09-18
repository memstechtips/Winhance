using Winhance.Core.Features.Common.Localization;
using FluentAssertions;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Selections;
using Xunit;
using Winhance.TestSupport;

namespace Winhance.Core.Tests.Catalog;

public class TwoStateShapeTests
{
    private static Setting TwoStates(LocKey on, LocKey off, RoleKind? offRole = null) => new()
    {
        Id = "t",
        Display = new() { Name = TestKeys.Of("n"), Description = TestKeys.Of("d") },
        Targets = [new RegTarget("K", ["HKCU\\S"], "V", Microsoft.Win32.RegistryValueKind.DWord)],
        States =
        [
            new() { Label = on, Set = new Dictionary<string, StateValue> { ["K"] = StateValue.Of(1) }, Roles = [new StateRole(RoleKind.WindowsDefault)] },
            new() { Label = off, Set = new Dictionary<string, StateValue> { ["K"] = StateValue.Of(0) }, Roles = offRole is { } role ? new[] { new StateRole(role) } : Array.Empty<StateRole>() },
        ],
    };

    [Fact]
    public void Enabled_and_Disabled_derive_a_toggle()
    {
        TwoStates(LocKey.Common.Enabled, LocKey.Common.Disabled).Control.Should().Be(ControlKind.Toggle);
    }

    [Fact]
    public void Checked_and_Unchecked_derive_a_check_box()
    {
        TwoStates(LocKey.Common.Checked, LocKey.Common.Unchecked).Control.Should().Be(ControlKind.CheckBox);
    }

    [Fact]
    public void A_pair_needs_both_of_its_labels()
    {
        TwoStates(LocKey.Common.Checked, LocKey.Common.Checked).Control.Should().Be(ControlKind.Selection);
        TwoStates(LocKey.Common.Enabled, LocKey.Common.Enabled).Control.Should().Be(ControlKind.Selection);
    }

    [Fact]
    public void A_mixed_pair_is_a_selection()
    {
        TwoStates(LocKey.Common.Enabled, LocKey.Common.Unchecked).Control.Should().Be(ControlKind.Selection);
    }

    [Fact]
    public void Each_kind_owns_its_own_label_pair()
    {
        TwoState.OnLabel(ControlKind.Toggle).Value.Should().Be("Common_Enabled");
        TwoState.OffLabel(ControlKind.Toggle).Value.Should().Be("Common_Disabled");
        TwoState.OnLabel(ControlKind.CheckBox).Value.Should().Be("Common_Checked");
        TwoState.OffLabel(ControlKind.CheckBox).Value.Should().Be("Common_Unchecked");
        TwoState.Label(ControlKind.CheckBox, on: false).Value.Should().Be("Common_Unchecked");
    }

    [Fact]
    public void Only_the_two_two_state_kinds_are_two_state()
    {
        TwoState.Is(ControlKind.Toggle).Should().BeTrue();
        TwoState.Is(ControlKind.CheckBox).Should().BeTrue();
        TwoState.Is(ControlKind.Selection).Should().BeFalse();
        TwoState.Is(ControlKind.Action).Should().BeFalse();
    }

    [Fact]
    public void The_default_role_reads_through_the_kinds_own_labels()
    {
        TwoState.GetDefault(TwoStates(LocKey.Common.Checked, LocKey.Common.Unchecked), default).Should().BeTrue();
        TwoState.GetDefault(TwoStates(LocKey.Common.Enabled, LocKey.Common.Disabled), default).Should().BeTrue();
        TwoState.GetRecommended(TwoStates(LocKey.Common.Checked, LocKey.Common.Unchecked, RoleKind.Recommended), default).Should().BeFalse();
    }

    [Fact]
    public void A_check_box_is_a_check_box_in_the_config_file()
    {
        ConfigFileMapper.InputTypeFor(TwoStates(LocKey.Common.Checked, LocKey.Common.Unchecked)).Should().Be(InputType.CheckBox);
        ConfigFileMapper.InputTypeFor(TwoStates(LocKey.Common.Enabled, LocKey.Common.Disabled)).Should().Be(InputType.Toggle);
    }

    [Fact]
    public void The_choice_factory_answers_the_kinds_own_shape()
    {
        ChoiceValue.TwoState(ControlKind.CheckBox, on: true).Should().Be(new ChoiceValue.CheckBox(true));
        ChoiceValue.TwoState(ControlKind.Toggle, on: false).Should().Be(new ChoiceValue.Toggle(false));
    }
}

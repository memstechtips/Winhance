using FluentAssertions;
using Winhance.Core.Features.Common.Enums;
using Winhance.UI.Features.Common.Converters;
using Xunit;

namespace Winhance.UI.Tests.Converters;

public class BadgeIconTemplateSelectorTests
{
    // Selector.SelectTemplateCore cannot be instantiated in the xunit host (WinUI
    // DataTemplateSelector requires a running UI dispatcher). The selector's enum-to-slot
    // branching is extracted into the public static PickByState<T> helper, which is tested
    // here with plain strings — reference-identity is preserved by the switch, so a passing
    // test on strings guarantees the same mapping holds for DataTemplate references at runtime.

    private const string Rec = "rec";
    private const string Def = "def";
    private const string Cust = "cust";
    private const string Pref = "pref";

    [Theory]
    [InlineData(SettingBadgeState.Recommended, Rec)]
    [InlineData(SettingBadgeState.Default, Def)]
    [InlineData(SettingBadgeState.Custom, Cust)]
    [InlineData(SettingBadgeState.Preference, Pref)]
    public void PickByState_ReturnsMatchingSlot(SettingBadgeState state, string expected)
    {
        var result = BadgeIconTemplateSelector.PickByState(state, Rec, Def, Cust, Pref);
        result.Should().Be(expected);
    }

    [Fact]
    public void PickByState_OutOfRangeEnumValue_ReturnsNull()
    {
        var result = BadgeIconTemplateSelector.PickByState((SettingBadgeState)999, Rec, Def, Cust, Pref);
        result.Should().BeNull();
    }

    [Theory]
    [InlineData(SettingBadgeState.Recommended)]
    [InlineData(SettingBadgeState.Default)]
    [InlineData(SettingBadgeState.Custom)]
    [InlineData(SettingBadgeState.Preference)]
    public void PickByState_NullSlot_ReturnsNull(SettingBadgeState state)
    {
        var result = BadgeIconTemplateSelector.PickByState<string>(state, null, null, null, null);
        result.Should().BeNull();
    }
}

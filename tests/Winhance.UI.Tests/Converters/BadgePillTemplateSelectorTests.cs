using FluentAssertions;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Converters;
using Xunit;

namespace Winhance.UI.Tests.Converters;

public class BadgePillTemplateSelectorTests
{
    // DataTemplateSelector.SelectTemplateCore cannot be instantiated in the xunit host
    // (WinUI DataTemplate requires a running UI dispatcher). The selector's kind-to-slot
    // mapping is extracted into public static PickByKind<T>, which is tested here with
    // plain strings — reference-identity is preserved by the switch, so a passing test
    // on strings guarantees the same mapping holds for DataTemplate references at runtime.

    private const string Rec = "rec";
    private const string Def = "def";
    private const string Cust = "cust";
    private const string Pref = "pref";

    [Theory]
    [InlineData(SettingBadgeKind.Recommended, Rec)]
    [InlineData(SettingBadgeKind.Default,     Def)]
    [InlineData(SettingBadgeKind.Custom,      Cust)]
    [InlineData(SettingBadgeKind.Preference,  Pref)]
    public void PickByKind_ReturnsMatchingSlot(SettingBadgeKind kind, string expected)
    {
        var result = BadgePillTemplateSelector.PickByKind(kind, Rec, Def, Cust, Pref);
        result.Should().Be(expected);
    }

    [Fact]
    public void PickByKind_OutOfRangeEnum_ReturnsNull()
    {
        var result = BadgePillTemplateSelector.PickByKind((SettingBadgeKind)999, Rec, Def, Cust, Pref);
        result.Should().BeNull();
    }

    [Theory]
    [InlineData(SettingBadgeKind.Recommended)]
    [InlineData(SettingBadgeKind.Default)]
    [InlineData(SettingBadgeKind.Custom)]
    [InlineData(SettingBadgeKind.Preference)]
    public void PickByKind_NullSlot_ReturnsNull(SettingBadgeKind kind)
    {
        var result = BadgePillTemplateSelector.PickByKind<string>(kind, null, null, null, null);
        result.Should().BeNull();
    }
}

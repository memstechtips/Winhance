using FluentAssertions;
using Winhance.UI.Features.Common.Controls;
using Xunit;

namespace Winhance.UI.Tests.Controls;

/// <summary>
/// The pin guards. Width accepts NaN as "auto", but MinWidth and MaxWidth reject it with E_INVALIDARG
/// ("Value does not fall within the expected range"). MinWidth was bound to the raw pin, so every
/// unpinned dropdown threw on load - 102 identical unhandled UI exceptions in one session's log.
/// </summary>
public class SettingComboBoxWidthTests
{
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(120d)]
    [InlineData(0d)]
    public void MinWidthForPin_IsNeverNaN(double pin)
    {
        double.IsNaN(SettingComboBox.MinWidthForPin(pin)).Should().BeFalse();
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(120d)]
    [InlineData(0d)]
    public void MaxWidthForPin_IsNeverNaN(double pin)
    {
        double.IsNaN(SettingComboBox.MaxWidthForPin(pin)).Should().BeFalse();
    }

    [Fact]
    public void UnpinnedMinWidth_CollapsesToZero_SoTheControlStillAutoSizes()
    {
        SettingComboBox.MinWidthForPin(double.NaN).Should().Be(0d);
    }

    [Fact]
    public void UnpinnedMaxWidth_IsUnbounded()
    {
        SettingComboBox.MaxWidthForPin(double.NaN).Should().Be(double.PositiveInfinity);
    }

    [Theory]
    [InlineData(120d)]
    [InlineData(64d)]
    public void PinnedWidth_PassesThroughToBothBounds(double pin)
    {
        SettingComboBox.MinWidthForPin(pin).Should().Be(pin);
        SettingComboBox.MaxWidthForPin(pin).Should().Be(pin);
    }
}

using FluentAssertions;
using Winhance.UI.Features.Common.Controls;
using Xunit;

namespace Winhance.UI.Tests.Controls;

public class UniformWrapPanelFitTests
{
    [Fact]
    public void Fit_WideRow_KeepsFullSizeCards()
    {
        UniformWrapPanel.Fit(1400, 420, 320, 16, 50).Should().Be((3, 420d));
    }

    [Fact]
    public void Fit_RowTooNarrowForTwoFullCards_ShrinksThemToKeepTwo()
    {
        UniformWrapPanel.Fit(694, 420, 320, 16, 50).Should().Be((2, 339d));
    }

    [Fact]
    public void Fit_RowTooNarrowForTwoSmallCards_FillsItWithOne()
    {
        UniformWrapPanel.Fit(560, 420, 320, 16, 50).Should().Be((1, 560d));
    }

    [Fact]
    public void Fit_WithoutAMinimum_KeepsTheCellAtItsWidth()
    {
        UniformWrapPanel.Fit(560, 420, 0, 16, 50).Should().Be((1, 420d));
    }
}

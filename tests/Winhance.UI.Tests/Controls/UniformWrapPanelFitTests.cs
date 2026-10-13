using FluentAssertions;
using Winhance.UI.Features.Common.Controls;
using Xunit;

namespace Winhance.UI.Tests.Controls;

public class UniformWrapPanelFitTests
{
    [Fact]
    public void A_wide_row_keeps_full_size_cards()
    {
        UniformWrapPanel.Fit(1400, 420, 320, 16, 50).Should().Be((3, 420d));
    }

    [Fact]
    public void A_row_too_narrow_for_two_full_cards_shrinks_them_to_keep_two()
    {
        UniformWrapPanel.Fit(694, 420, 320, 16, 50).Should().Be((2, 339d));
    }

    [Fact]
    public void A_row_too_narrow_for_two_small_cards_fills_it_with_one()
    {
        UniformWrapPanel.Fit(560, 420, 320, 16, 50).Should().Be((1, 560d));
    }

    [Fact]
    public void Without_a_minimum_the_cell_stays_at_its_width()
    {
        UniformWrapPanel.Fit(560, 420, 0, 16, 50).Should().Be((1, 420d));
    }
}

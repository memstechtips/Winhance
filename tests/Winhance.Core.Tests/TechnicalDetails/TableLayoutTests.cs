using FluentAssertions;
using Winhance.Core.Features.Common.TechnicalDetails;
using Xunit;

namespace Winhance.Core.Tests.TechnicalDetails;

/// <summary>
/// The horizontal arithmetic that used to live inside the panel's ArrangeOverride, where the only
/// way to check it was a screenshot. Two frozen columns (Option, Role) then three value columns.
/// </summary>
public class TableLayoutTests
{
    private static readonly double[] Widths = [200, 120, 100, 100, 100];   // total 620, frozen edge 320
    private const int Frozen = 2;

    [Fact]
    public void FrozenWidth_IsTheSumOfTheLeadingColumns()
    {
        TableLayout.FrozenWidth(Widths, Frozen).Should().Be(320);
        TableLayout.FrozenWidth(Widths, 0).Should().Be(0);
    }

    [Fact]
    public void FrozenWidth_ClampsAFrozenCountBeyondTheColumns()
    {
        TableLayout.FrozenWidth(Widths, 99).Should().Be(620);
        TableLayout.FrozenWidth(Widths, -5).Should().Be(0);
    }

    [Fact]
    public void StretchToViewport_GivesTheLeftoverWidthToTheLastColumn()
    {
        // 620 of content in a 800 viewport: the 180 of slack goes to the last column, so the table
        // reaches the edge of its card instead of stopping mid-way and leaving dead space.
        TableLayout.StretchToViewport(Widths, viewportWidth: 800)
            .Should().Equal(200, 120, 100, 100, 280);
    }

    [Fact]
    public void StretchToViewport_LeavesOverflowingContentAlone()
    {
        // Wider than the viewport already: nothing to fill, and shrinking would clip content that
        // the horizontal scroll exists to reach.
        TableLayout.StretchToViewport(Widths, viewportWidth: 500).Should().Equal(Widths);
        TableLayout.StretchToViewport(Widths, viewportWidth: 620).Should().Equal(Widths);
    }

    [Fact]
    public void StretchToViewport_IgnoresAnUnconstrainedOrEmptyViewport()
    {
        TableLayout.StretchToViewport(Widths, double.PositiveInfinity).Should().Equal(Widths);
        TableLayout.StretchToViewport(Widths, double.NaN).Should().Equal(Widths);
        TableLayout.StretchToViewport(Widths, 0).Should().Equal(Widths);
        TableLayout.StretchToViewport(Widths, -50).Should().Equal(Widths);
    }

    [Fact]
    public void StretchToViewport_DoesNotMutateTheCallersArray()
    {
        var original = new double[] { 10, 20 };
        TableLayout.StretchToViewport(original, viewportWidth: 100);
        original.Should().Equal(10, 20);
    }

    [Fact]
    public void StretchToViewport_HandlesNoColumns() =>
        TableLayout.StretchToViewport([], viewportWidth: 500).Should().BeEmpty();

    [Fact]
    public void StretchedWidths_LeaveNothingToScroll()
    {
        // The two together are what hides the scrollbar on a table that fits: stretch consumes the
        // slack, so MaxOffset over the stretched widths is zero.
        var stretched = TableLayout.StretchToViewport(Widths, viewportWidth: 800);
        TableLayout.MaxOffset(stretched, viewportWidth: 800).Should().Be(0);
    }

    [Fact]
    public void MaxOffset_IsContentMinusViewport()
    {
        TableLayout.MaxOffset(Widths, viewportWidth: 500).Should().Be(120);
        TableLayout.MaxOffset(Widths, viewportWidth: 620).Should().Be(0);
        TableLayout.MaxOffset(Widths, viewportWidth: 900).Should().Be(0, "content narrower than the viewport cannot scroll");
    }

    [Fact]
    public void ClampOffset_KeepsTheOffsetInRange()
    {
        TableLayout.ClampOffset(-40, Widths, 500).Should().Be(0);
        TableLayout.ClampOffset(60, Widths, 500).Should().Be(60);
        TableLayout.ClampOffset(9999, Widths, 500).Should().Be(120);
        TableLayout.ClampOffset(double.NaN, Widths, 500).Should().Be(0);
    }

    // ---------------------------------------------------------------------------------------------
    // Frozen cells
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void FrozenCells_DoNotMoveWhenScrolled()
    {
        var atRest = TableLayout.Place(Widths, Frozen, horizontalOffset: 0, column: 1);
        var scrolled = TableLayout.Place(Widths, Frozen, horizontalOffset: 120, column: 1);

        scrolled.Should().Be(atRest);
        scrolled.X.Should().Be(200);
        scrolled.NeedsClip.Should().BeFalse("a frozen cell never overlaps anything");
    }

    // ---------------------------------------------------------------------------------------------
    // Scrolling cells — the clip is what keeps them out of the frozen region
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void ScrollingCell_AtRest_SitsAtItsColumnOffsetWithNoClip()
    {
        var cell = TableLayout.Place(Widths, Frozen, horizontalOffset: 0, column: 2);

        cell.X.Should().Be(320);
        cell.Width.Should().Be(100);
        cell.NeedsClip.Should().BeFalse();
        cell.IsHidden.Should().BeFalse();
    }

    [Fact]
    public void ScrollingCell_MovesLeftByTheOffset()
    {
        TableLayout.Place(Widths, Frozen, horizontalOffset: 40, column: 2).X.Should().Be(280);
        TableLayout.Place(Widths, Frozen, horizontalOffset: 40, column: 3).X.Should().Be(380);
    }

    [Fact]
    public void ScrollingCell_PartlyBehindTheFrozenEdge_IsClippedNotCovered()
    {
        // Column 2 starts at 320; scrolled 40px it starts at 280, so 40px of it would sit left of
        // the frozen edge at 320. That 40px is cut away rather than painted over.
        var cell = TableLayout.Place(Widths, Frozen, horizontalOffset: 40, column: 2);

        cell.X.Should().Be(280);
        cell.ClipFromLeft.Should().Be(40);
        cell.NeedsClip.Should().BeTrue();
        cell.VisibleWidth.Should().Be(60);
        cell.IsHidden.Should().BeFalse();
    }

    [Fact]
    public void ScrollingCell_FullyBehindTheFrozenEdge_IsHidden()
    {
        // Scrolled a full column width, column 2 is entirely under the frozen region.
        var cell = TableLayout.Place(Widths, Frozen, horizontalOffset: 100, column: 2);

        cell.ClipFromLeft.Should().Be(100);
        cell.IsHidden.Should().BeTrue();
        cell.VisibleWidth.Should().Be(0);
        cell.NeedsClip.Should().BeFalse("a hidden cell is not drawn at all, so it needs no clip geometry");
    }

    [Fact]
    public void ClipNeverExceedsTheCellWidth()
    {
        foreach (var offset in new double[] { 0, 1, 50, 100, 250, 620, 10_000 })
        {
            for (int column = Frozen; column < Widths.Length; column++)
            {
                var cell = TableLayout.Place(Widths, Frozen, offset, column);
                cell.ClipFromLeft.Should().BeInRange(0, cell.Width,
                    $"offset {offset}, column {column} must produce a clip inside the cell");
            }
        }
    }

    [Fact]
    public void NoScrollingCellEverPaintsIntoTheFrozenRegion()
    {
        var frozenEdge = TableLayout.FrozenWidth(Widths, Frozen);

        foreach (var offset in new double[] { 0, 17, 40, 99, 120 })
        {
            for (int column = Frozen; column < Widths.Length; column++)
            {
                var cell = TableLayout.Place(Widths, Frozen, offset, column);
                if (cell.IsHidden) continue;
                var visibleLeft = cell.X + cell.ClipFromLeft;
                visibleLeft.Should().BeGreaterThanOrEqualTo(frozenEdge - 0.0001,
                    $"offset {offset}, column {column} would bleed over the frozen columns");
            }
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Spanning headers — a path header sits across the value columns it owns
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void SpanningHeader_TakesTheCombinedWidthOfItsColumns()
    {
        var span = TableLayout.Place(Widths, Frozen, horizontalOffset: 0, column: 2, columnSpan: 3);

        span.X.Should().Be(320);
        span.Width.Should().Be(300);
    }

    [Fact]
    public void SpanningHeader_ScrollsAndClipsLikeAnyOtherCell()
    {
        var span = TableLayout.Place(Widths, Frozen, horizontalOffset: 40, column: 2, columnSpan: 3);

        span.X.Should().Be(280);
        span.ClipFromLeft.Should().Be(40);
        span.VisibleWidth.Should().Be(260);
    }

    [Fact]
    public void SpanningHeader_StartingInTheFrozenRegion_IsFrozen()
    {
        var span = TableLayout.Place(Widths, Frozen, horizontalOffset: 80, column: 0, columnSpan: 2);

        span.X.Should().Be(0, "a span anchored in the frozen region moves with it, which is to say not at all");
        span.NeedsClip.Should().BeFalse();
    }

    [Fact]
    public void Span_ClampsAtTheLastColumn()
    {
        var span = TableLayout.Place(Widths, Frozen, horizontalOffset: 0, column: 4, columnSpan: 99);

        span.Width.Should().Be(100, "there is only one column left to span");
    }

    // ---------------------------------------------------------------------------------------------
    // Guards
    // ---------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    public void Place_RejectsAColumnOutsideTheTable(int column)
    {
        var act = () => TableLayout.Place(Widths, Frozen, 0, column);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Place_RejectsAZeroSpan()
    {
        var act = () => TableLayout.Place(Widths, Frozen, 0, 2, columnSpan: 0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void WithNothingFrozen_TheClipBoundaryIsThePanelsLeftEdge()
    {
        double[] one = [150];

        var cell = TableLayout.Place(one, frozenColumnCount: 0, horizontalOffset: 25, column: 0);

        cell.X.Should().Be(-25);
        // WinUI panels do not clip their children, so without this the scrolled-off part would
        // paint outside the panel. A frozen edge of 0 is still an edge worth clipping at.
        cell.ClipFromLeft.Should().Be(25);
        cell.VisibleWidth.Should().Be(125);
    }
}

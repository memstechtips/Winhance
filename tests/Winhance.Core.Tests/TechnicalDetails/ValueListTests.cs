using FluentAssertions;
using Winhance.Core.Features.Common.TechnicalDetails;
using Xunit;

namespace Winhance.Core.Tests.TechnicalDetails;

public class ValueListTests
{
    [Fact]
    public void Records_WithSeparateButEqualLists_AreEqual()
    {
        var a = new MatrixOption { Label = "On", Cells = new List<MatrixCell> { new("1"), MatrixCell.Check } };
        var b = new MatrixOption { Label = "On", Cells = [new("1"), MatrixCell.Check] };

        b.Should().Be(a);
        b.GetHashCode().Should().Be(a.GetHashCode());
        (b with { Cells = [new("0")] }).Should().NotBe(a);
    }

    [Fact]
    public void PositionalListParameter_ComparesByContent()
    {
        var a = new MatrixOptionLinks("On", new List<MatrixChip> { new("x", "y") });
        var b = new MatrixOptionLinks("On", [new("x", "y")]);

        b.Should().Be(a);
    }

    [Fact]
    public void From_CopiesTheSource_SoLaterMutationDoesNotLeakIn()
    {
        var source = new List<MatrixCell> { new("1") };
        var option = new MatrixOption { Label = "On", Cells = source };

        source.Add(new("2"));

        option.Cells.Should().HaveCount(1);
    }
}

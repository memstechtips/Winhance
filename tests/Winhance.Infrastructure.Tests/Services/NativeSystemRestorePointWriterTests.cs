using FluentAssertions;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

// Only the truncation is covered: CreateRestorePoint writes a real restore point and a registry
// value on whatever machine runs the suite.
public class NativeSystemRestorePointWriterTests
{
    private const int MaxLength = NativeSystemRestorePointWriter.MaxDescriptionLength;

    [Theory]
    [InlineData(20, 20)]
    [InlineData(MaxLength, MaxLength)]
    [InlineData(MaxLength + 40, MaxLength)]
    public void TruncateDescription_AnyLength_ReturnsThePrefixThatFitsTheBuffer(int length, int expectedLength)
    {
        // The last character differs from the rest, so keeping the wrong end would not still match.
        var description = new string('x', length - 1) + "Z";

        var truncated = NativeSystemRestorePointWriter.TruncateDescription(description);

        truncated.Should().Be(description[..expectedLength]);
    }
}

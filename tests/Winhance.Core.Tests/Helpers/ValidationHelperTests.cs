using FluentAssertions;
using Winhance.Core.Features.Common.Helpers;
using Xunit;

namespace Winhance.Core.Tests.Helpers;

public class ValidationHelperTests
{
    [Fact]
    public void NotNull_WithNonNullValue_DoesNotThrow()
    {
        var action = () => ValidationHelper.NotNull("hello", "param");
        action.Should().NotThrow();
    }

    [Fact]
    public void NotNull_WithNullValue_ThrowsArgumentNullException()
    {
        var action = () => ValidationHelper.NotNull(null!, "myParam");
        action.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("myParam");
    }

    [Fact]
    public void NotNull_WithObject_DoesNotThrow()
    {
        var action = () => ValidationHelper.NotNull(new object(), "param");
        action.Should().NotThrow();
    }

    [Fact]
    public void NotNullOrEmpty_WithValidString_DoesNotThrow()
    {
        var action = () => ValidationHelper.NotNullOrEmpty("valid", "param");
        action.Should().NotThrow();
    }

    [Fact]
    public void NotNullOrEmpty_WithNull_ThrowsArgumentNullException()
    {
        var action = () => ValidationHelper.NotNullOrEmpty(null!, "myParam");
        action.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("myParam");
    }

    [Fact]
    public void NotNullOrEmpty_WithEmptyString_ThrowsArgumentException()
    {
        var action = () => ValidationHelper.NotNullOrEmpty("", "myParam");
        action.Should().Throw<ArgumentException>()
            .WithMessage("Value cannot be empty.*")
            .And.ParamName.Should().Be("myParam");
    }

    [Fact]
    public void NotNullOrEmpty_WithWhitespace_DoesNotThrow()
    {
        // Note: IsNullOrEmpty returns false for whitespace-only strings
        var action = () => ValidationHelper.NotNullOrEmpty("  ", "param");
        action.Should().NotThrow();
    }

    [Fact]
    public void NotNullOrEmpty_WithSingleCharacter_DoesNotThrow()
    {
        var action = () => ValidationHelper.NotNullOrEmpty("x", "param");
        action.Should().NotThrow();
    }
}

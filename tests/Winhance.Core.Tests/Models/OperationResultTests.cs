using FluentAssertions;
using Winhance.Core.Features.Common.Models;
using Xunit;

namespace Winhance.Core.Tests.Models;

public class OperationResultTests
{
    [Fact]
    public void Succeeded_ReturnsSuccessResult()
    {
        var result = OperationResult.Succeeded();

        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.Exception.Should().BeNull();
    }

    [Fact]
    public void Failed_WithMessage_ReturnsFailureResult()
    {
        var result = OperationResult.Failed("Something went wrong");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Something went wrong");
        result.Exception.Should().BeNull();
    }

    [Fact]
    public void Failed_WithMessageAndException_ReturnsFailureWithException()
    {
        var ex = new InvalidOperationException("test");
        var result = OperationResult.Failed("Error occurred", ex);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Error occurred");
        result.Exception.Should().BeSameAs(ex);
    }
}

public class OperationResultGenericTests
{
    [Fact]
    public void Succeeded_WithValue_ReturnsSuccessWithResult()
    {
        var result = OperationResult<int>.Succeeded(42);

        result.Success.Should().BeTrue();
        result.Result.Should().Be(42);
        result.ErrorMessage.Should().BeNull();
        result.Exception.Should().BeNull();
        result.RequiresConfirmation.Should().BeFalse();
        result.InfoMessage.Should().BeNull();
    }

    [Fact]
    public void Succeeded_WithReferenceType_ReturnsSuccessWithResult()
    {
        var data = new List<string> { "a", "b" };
        var result = OperationResult<List<string>>.Succeeded(data);

        result.Success.Should().BeTrue();
        result.Result.Should().BeSameAs(data);
    }

    [Fact]
    public void Failed_WithMessage_ReturnsFailure()
    {
        var result = OperationResult<string>.Failed("not found");

        result.Success.Should().BeFalse();
        result.Result.Should().BeNull();
        result.ErrorMessage.Should().Be("not found");
        result.Exception.Should().BeNull();
    }

    [Fact]
    public void Failed_WithMessageAndException_ReturnsFailureWithException()
    {
        var ex = new IOException("disk full");
        var result = OperationResult<byte[]>.Failed("write failed", ex);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("write failed");
        result.Exception.Should().BeSameAs(ex);
    }

    [Fact]
    public void Cancelled_WithDefaultMessage_ReturnsCancelledResult()
    {
        var result = OperationResult<string>.Cancelled();

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Operation was cancelled");
    }

    [Fact]
    public void Cancelled_WithCustomMessage_ReturnsCancelledResult()
    {
        var result = OperationResult<int>.Cancelled("User cancelled");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("User cancelled");
    }

    [Fact]
    public void ConfirmationRequired_ReturnsConfirmationResult()
    {
        var result = OperationResult<bool>.ConfirmationRequired("Are you sure?");

        result.Success.Should().BeFalse();
        result.RequiresConfirmation.Should().BeTrue();
        result.ErrorMessage.Should().Be("Are you sure?");
    }

    [Fact]
    public void DeferredSuccess_ReturnsSuccessWithInfoMessage()
    {
        var result = OperationResult<string>.DeferredSuccess("data", "Will complete later");

        result.Success.Should().BeTrue();
        result.Result.Should().Be("data");
        result.InfoMessage.Should().Be("Will complete later");
    }
}

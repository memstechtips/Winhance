using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Services;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class DispatcherServiceTests
{
    private readonly Mock<ILogService> _log = new();

    [Fact]
    public void Logged_LogsTheExceptionThenRethrowsIt()
    {
        var thrown = new InvalidOperationException("boom");
        var callback = new DispatcherService(_log.Object).Logged(() => throw thrown);

        callback.Invoking(c => c()).Should().Throw<InvalidOperationException>().Which.Should().BeSameAs(thrown);
        _log.Verify(l => l.LogError(
            It.Is<string>(m => m.Contains(typeof(InvalidOperationException).FullName!, StringComparison.Ordinal)),
            thrown, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void Logged_RunsTheCallbackAndLogsNothingWhenItSucceeds()
    {
        var ran = false;

        new DispatcherService(_log.Object).Logged(() => ran = true)();

        ran.Should().BeTrue();
        _log.VerifyNoOtherCalls();
    }
}

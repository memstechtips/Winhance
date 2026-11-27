using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Xunit;

namespace Winhance.Core.Tests.Helpers;

public class DialogServiceExtensionsTests
{
    private readonly Mock<IDialogService> _dialogService = new();
    private readonly Mock<ILogService> _logService = new();

    [Fact]
    public async Task ASuccessfulActionShowsNothing()
    {
        var ran = false;

        var succeeded = await _dialogService.Object.RunOrShowErrorAsync(_logService.Object,
            () => { ran = true; return Task.CompletedTask; }, _ => "unused");

        succeeded.Should().BeTrue();
        ran.Should().BeTrue();
        _dialogService.Verify(d => d.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task AFailureIsLoggedAgainstTheCallerAndShown()
    {
        var failure = new InvalidOperationException("disk gone");

        var succeeded = await _dialogService.Object.RunOrShowErrorAsync(_logService.Object,
            () => Task.FromException(failure), ex => $"Could not delete: {ex.Message}");

        succeeded.Should().BeFalse();
        _logService.Verify(l => l.LogError(
            It.Is<string>(m => m.Contains(nameof(AFailureIsLoggedAgainstTheCallerAndShown)) && m.Contains("disk gone")),
            failure,
            It.Is<string>(path => path.EndsWith("DialogServiceExtensionsTests.cs", StringComparison.Ordinal))), Times.Once);
        _dialogService.Verify(d => d.ShowErrorAsync("Could not delete: disk gone", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ACancelledActionIsNotAnError()
    {
        var succeeded = await _dialogService.Object.RunOrShowErrorAsync(_logService.Object,
            () => Task.FromCanceled(new CancellationToken(canceled: true)), _ => "unused");

        succeeded.Should().BeFalse();
        _dialogService.Verify(d => d.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _logService.Verify(l => l.LogError(It.IsAny<string>(), It.IsAny<Exception>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ASynchronousActionThatThrowsIsCaughtToo()
    {
        Action deleteLockedFile = () => throw new IOException("locked");

        var succeeded = await _dialogService.Object.RunOrShowErrorAsync(_logService.Object,
            deleteLockedFile, ex => ex.Message);

        succeeded.Should().BeFalse();
        _logService.Verify(l => l.LogError(
            It.Is<string>(m => m.Contains(nameof(ASynchronousActionThatThrowsIsCaughtToo))),
            It.IsAny<IOException>(),
            It.Is<string>(path => path.EndsWith("DialogServiceExtensionsTests.cs", StringComparison.Ordinal))), Times.Once);
        _dialogService.Verify(d => d.ShowErrorAsync("locked", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }
}

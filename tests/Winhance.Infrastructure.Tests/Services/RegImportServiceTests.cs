using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class RegImportServiceTests
{
    private readonly Mock<IInteractiveUserService> _interactiveUser = new();
    private readonly Mock<IFileSystemService> _fileSystem = new();
    private readonly Mock<IProcessExecutor> _processExecutor = new();
    private readonly Mock<ILogService> _log = new();
    private readonly RegImportService _sut;

    public RegImportServiceTests()
    {
        // CombinePath joins parts with a backslash (good enough for asserting the temp path is built/used).
        _fileSystem.Setup(f => f.CombinePath(It.IsAny<string[]>())).Returns((string[] parts) => string.Join("\\", parts));
        _fileSystem.Setup(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _fileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        _sut = new RegImportService(_interactiveUser.Object, _fileSystem.Object, _processExecutor.Object, _log.Object);
    }

    [Fact]
    public async Task RunRegImportAsync_EmptyContent_DoesNothing()
    {
        await _sut.RunRegImportAsync("");

        _fileSystem.Verify(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _processExecutor.Verify(p => p.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunRegImportAsync_NonOts_WritesTempFileAndImportsViaCmd()
    {
        _interactiveUser.Setup(i => i.IsOtsElevation).Returns(false);
        _fileSystem.Setup(f => f.GetTempPath()).Returns(@"C:\Temp");
        _processExecutor
            .Setup(p => p.ExecuteAsync("cmd.exe", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessExecutionResult { ExitCode = 0 });

        await _sut.RunRegImportAsync("REGDATA");

        _fileSystem.Verify(f => f.WriteAllTextAsync(It.IsAny<string>(), "REGDATA", It.IsAny<CancellationToken>()), Times.Once);
        _processExecutor.Verify(p => p.ExecuteAsync("cmd.exe", It.Is<string>(s => s.Contains("reg import")), It.IsAny<CancellationToken>()), Times.Once);
        _fileSystem.Verify(f => f.DeleteFile(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task RunRegImportAsync_Ots_RunsRegImportAsInteractiveUser()
    {
        _interactiveUser.Setup(i => i.IsOtsElevation).Returns(true);
        _interactiveUser.Setup(i => i.HasInteractiveUserToken).Returns(true);
        _interactiveUser.Setup(i => i.GetInteractiveUserFolderPath(Environment.SpecialFolder.LocalApplicationData))
            .Returns(@"C:\Users\Bob\AppData\Local");
        _interactiveUser
            .Setup(i => i.RunProcessAsInteractiveUserAsync(
                "reg.exe", It.IsAny<string>(),
                It.IsAny<Action<string>?>(), It.IsAny<Action<string>?>(),
                It.IsAny<CancellationToken>(), It.IsAny<int>(), It.IsAny<Action<string>?>()))
            .ReturnsAsync(new InteractiveProcessResult(0, "", ""));

        await _sut.RunRegImportAsync("REGDATA");

        _fileSystem.Verify(f => f.CreateDirectory(It.IsAny<string>()), Times.Once);
        _interactiveUser.Verify(i => i.RunProcessAsInteractiveUserAsync(
            "reg.exe", It.Is<string>(s => s.Contains("import")),
            It.IsAny<Action<string>?>(), It.IsAny<Action<string>?>(),
            It.IsAny<CancellationToken>(), It.IsAny<int>(), It.IsAny<Action<string>?>()), Times.Once);
        _processExecutor.Verify(p => p.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _fileSystem.Verify(f => f.DeleteFile(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task RunRegImportAsync_WhenWriteThrows_RethrowsAndStillDeletesTempFile()
    {
        _interactiveUser.Setup(i => i.IsOtsElevation).Returns(false);
        _fileSystem.Setup(f => f.GetTempPath()).Returns(@"C:\Temp");
        _fileSystem
            .Setup(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("disk full"));

        var act = async () => await _sut.RunRegImportAsync("REGDATA");

        await act.Should().ThrowAsync<IOException>();
        _fileSystem.Verify(f => f.DeleteFile(It.IsAny<string>()), Times.Once);
    }
}

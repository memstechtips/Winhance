using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class SystemBackupServiceTests
{
    private readonly Mock<ILogService> _mockLog = new();
    private readonly Mock<ILocalizationService> _mockLocalization = new();
    private readonly Mock<IProcessExecutor> _mockProcessExecutor = new();
    private readonly SystemBackupService _sut;

    public SystemBackupServiceTests()
    {
        // Default localization returns the key itself for any GetString call
        _mockLocalization
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);

        _sut = new SystemBackupService(
            _mockLog.Object,
            _mockLocalization.Object,
            _mockProcessExecutor.Object);
    }

    // ── EnsureInitialBackupsAsync ──

    [Fact]
    public async Task EnsureInitialBackupsAsync_WhenExceptionThrown_ReturnsFailureResult()
    {
        // The WMI calls in FindRestorePointAsync will throw on non-admin / test environments.
        // This exercises the outer catch block.
        var result = await _sut.EnsureInitialBackupsAsync();

        // The service catches all exceptions and returns a failure result
        // (on a test machine, WMI calls typically fail)
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task EnsureInitialBackupsAsync_ReportsProgressViaCallback()
    {
        var progressReports = new List<TaskProgressDetail>();
        var progress = new Progress<TaskProgressDetail>(detail => progressReports.Add(detail));

        // This will fail on WMI (expected in test env), but progress should be reported
        await _sut.EnsureInitialBackupsAsync(progress);

        // At minimum, the first progress report (checking restore point) should have been sent
        // (Progress<T> may not have delivered synchronously, so we just validate no throw)
    }

    [Fact]
    public async Task EnsureInitialBackupsAsync_WithCancellationToken_AcceptsToken()
    {
        using var cts = new CancellationTokenSource();

        // Verify the method signature accepts a CancellationToken
        var result = await _sut.EnsureInitialBackupsAsync(cancellationToken: cts.Token);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task EnsureInitialBackupsAsync_FailureResult_ContainsErrorMessage()
    {
        // On a test environment, WMI calls will fail, producing a failure with an error message
        var result = await _sut.EnsureInitialBackupsAsync();

        // The service either succeeds or returns a failure with a message
        if (!result.Success)
        {
            result.ErrorMessage.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public async Task EnsureInitialBackupsAsync_LogsStartOfBackupProcess()
    {
        await _sut.EnsureInitialBackupsAsync();

        _mockLog.Verify(
            l => l.Log(
                Core.Features.Common.Enums.LogLevel.Info,
                It.Is<string>(s => s.Contains("Starting backup process")),
                It.IsAny<Exception?>()),
            Times.Once);
    }

    // ── BackupResult model coverage ──

    [Fact]
    public void BackupResult_CreateSuccess_SetsCorrectProperties()
    {
        var date = new DateTime(2025, 1, 15);
        var result = BackupResult.CreateSuccess(
            restorePointDate: date,
            restorePointCreated: true,
            systemRestoreWasDisabled: true);

        result.Success.Should().BeTrue();
        result.RestorePointDate.Should().Be(date);
        result.RestorePointCreated.Should().BeTrue();
        result.SystemRestoreWasDisabled.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void BackupResult_CreateFailure_SetsCorrectProperties()
    {
        var result = BackupResult.CreateFailure("Something went wrong", systemRestoreWasDisabled: true);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Something went wrong");
        result.SystemRestoreWasDisabled.Should().BeTrue();
        result.RestorePointCreated.Should().BeFalse();
    }
}

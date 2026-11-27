using System.Management;
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
    private readonly Mock<ISystemRestoreService> _mockSystemRestore = new();
    private readonly FakeWmiApi _wmiApi = new();
    // Defaults to failure, matching what the real native writer does without admin / the System
    // Restore service - so unconfigured tests short-circuit fast instead of running the full
    // 10-attempt/3s verification retry loop.
    private readonly FakeSystemRestorePointWriter _restorePointWriter = new();
    private readonly SystemBackupService _sut;

    public SystemBackupServiceTests()
    {
        _mockLocalization
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);
        _mockLocalization
            .Setup(l => l.GetString(It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns((string key, object[] args) => $"{key}:{string.Join(",", args)}");

        _sut = new SystemBackupService(
            _mockLog.Object,
            _mockLocalization.Object,
            _mockProcessExecutor.Object,
            _mockSystemRestore.Object,
            _wmiApi,
            _restorePointWriter);
    }

    [Fact]
    public async Task CreateRestorePointAsync_NativeWriteFails_ReturnsFailureResult()
    {
        // FakeSystemRestorePointWriter defaults to failure - matching what the real native call
        // does without admin / the System Restore service - so this returns a failure result via
        // the normal early-return path, not an exception.
        var result = await _sut.CreateRestorePointAsync();

        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
    }

    [Theory]
    [InlineData(1058, "RestorePoint_Error_ServiceDisabled")]
    [InlineData(112, "RestorePoint_Error_DiskFull")]
    [InlineData(10, "RestorePoint_Error_SafeMode")]
    [InlineData(1460, "RestorePoint_Error_Timeout")]
    [InlineData(1359, "RestorePoint_Error_Internal")]
    [InlineData(-2147212542, "RestorePoint_Error_ShadowCopy")]
    [InlineData(-2147212532, "RestorePoint_Error_WindowsCode:0x8004230C")]
    [InlineData(-1, "RestorePoint_Error_WindowsCode:0xFFFFFFFF")]
    public async Task CreateRestorePointAsync_NativeWriteFails_ReturnsALocalizedReasonForTheStatus(int status, string expected)
    {
        _restorePointWriter.StatusCode = status;

        var result = await _sut.CreateRestorePointAsync();

        result.ErrorMessage.Should().Be(expected);
    }

    [Fact]
    public async Task CreateRestorePointAsync_NativeWriteFails_LogsTheFailureOnce()
    {
        _restorePointWriter.StatusCode = -2147212542;

        await _sut.CreateRestorePointAsync();

        _mockLog.Verify(
            l => l.Log(Core.Features.Common.Enums.LogLevel.Error, It.Is<string>(m => m.StartsWith("Failed to create restore point")), It.IsAny<Exception>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateRestorePointAsync_ReportsProgressViaCallback()
    {
        var progressReports = new List<TaskProgressDetail>();
        var progress = new Progress<TaskProgressDetail>(detail => progressReports.Add(detail));

        await _sut.CreateRestorePointAsync(progress: progress);

        // Progress<T> may not have delivered synchronously, so this only validates no throw.
    }

    [Fact]
    public async Task CreateRestorePointAsync_WithCancellationToken_AcceptsToken()
    {
        using var cts = new CancellationTokenSource();

        var result = await _sut.CreateRestorePointAsync(cancellationToken: cts.Token);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateRestorePointAsync_CancelledBeforeTheWrite_ThrowsAndWritesNothing()
    {
        _mockSystemRestore.Setup(s => s.IsEnabledForC()).Returns(true);
        _restorePointWriter.Success = true;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => _sut.CreateRestorePointAsync(cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _restorePointWriter.LastDescription.Should().BeNull();
    }

    [Fact]
    public async Task CreateRestorePointAsync_CancelledBeforeItStarts_LeavesProtectionOff()
    {
        _mockSystemRestore.Setup(s => s.IsEnabledForC()).Returns(false);
        _restorePointWriter.Success = true;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => _sut.CreateRestorePointAsync(cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _wmiApi.ClassInvocations.Should().BeEmpty();
        _restorePointWriter.LastDescription.Should().BeNull();
    }

    [Fact]
    public async Task CreateRestorePointAsync_CancelledWhileTurningProtectionOn_TurnsItBackOff()
    {
        _mockSystemRestore.Setup(s => s.IsEnabledForC()).Returns(false);
        _restorePointWriter.Success = true;
        using var cts = new CancellationTokenSource();
        _mockProcessExecutor
            .Setup(p => p.ExecuteAsync("vssadmin", It.Is<string>(a => a.Contains("MaxSize=20GB")), It.IsAny<CancellationToken>()))
            .Callback(() => cts.Cancel())
            .ReturnsAsync(new ProcessExecutionResult { ExitCode = 0 });

        var act = () => _sut.CreateRestorePointAsync(cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _restorePointWriter.LastDescription.Should().BeNull();
        _wmiApi.ClassInvocations.Select(i => i.Method).Should().Equal("Enable", "Disable");
    }

    [Fact]
    public async Task CreateRestorePointAsync_CancelledWhenProtectionWasAlreadyOn_LeavesItOn()
    {
        _mockSystemRestore.Setup(s => s.IsEnabledForC()).Returns(true);
        _restorePointWriter.Success = true;
        using var cts = new CancellationTokenSource();
        _mockProcessExecutor
            .Setup(p => p.ExecuteAsync("vssadmin", It.Is<string>(a => a.StartsWith("list shadowstorage")), It.IsAny<CancellationToken>()))
            .Callback(() => cts.Cancel())
            .ReturnsAsync(new ProcessExecutionResult { ExitCode = 1 });

        var act = () => _sut.CreateRestorePointAsync(cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _restorePointWriter.LastDescription.Should().BeNull();
        _wmiApi.ClassInvocations.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateRestorePointAsync_CancelledDuringTheStorageCheckAfterTurningOn_TurnsItBackOff()
    {
        _mockSystemRestore.Setup(s => s.IsEnabledForC()).Returns(false);
        _restorePointWriter.Success = true;
        using var cts = new CancellationTokenSource();
        _mockProcessExecutor
            .Setup(p => p.ExecuteAsync("vssadmin", It.Is<string>(a => a.StartsWith("list shadowstorage")), It.IsAny<CancellationToken>()))
            .Callback(() => cts.Cancel())
            .ReturnsAsync(new ProcessExecutionResult { ExitCode = 1 });

        var act = () => _sut.CreateRestorePointAsync(cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _restorePointWriter.LastDescription.Should().BeNull();
        _restorePointWriter.Removed.Should().BeEmpty();
        _wmiApi.ClassInvocations.Select(i => i.Method).Should().Equal("Enable", "Disable");
    }

    [Fact]
    public async Task CreateRestorePointAsync_CancelledDuringTheWriteAfterTurningOn_RemovesThePointAndTurnsItBackOff()
    {
        _mockSystemRestore.Setup(s => s.IsEnabledForC()).Returns(false);
        _restorePointWriter.Success = true;
        using var cts = new CancellationTokenSource();
        _restorePointWriter.OnCreate = cts.Cancel;

        var act = () => _sut.CreateRestorePointAsync(cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _restorePointWriter.Removed.Should().Equal(42u);
        _wmiApi.ClassInvocations.Select(i => i.Method).Should().Equal("Enable", "Disable");
    }

    [Fact]
    public async Task CreateRestorePointAsync_CancelledDuringTheWriteWithProtectionOn_RemovesThePointAndLeavesItOn()
    {
        _mockSystemRestore.Setup(s => s.IsEnabledForC()).Returns(true);
        _restorePointWriter.Success = true;
        using var cts = new CancellationTokenSource();
        _restorePointWriter.OnCreate = cts.Cancel;

        var act = () => _sut.CreateRestorePointAsync(cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _restorePointWriter.Removed.Should().Equal(42u);
        _wmiApi.ClassInvocations.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateRestorePointAsync_CancelledDuringTheWriteWhenThePointMayNotBeOurs_RemovesNothing()
    {
        _mockSystemRestore.Setup(s => s.IsEnabledForC()).Returns(false);
        _restorePointWriter.Success = true;
        _restorePointWriter.SequenceNumber = null;
        using var cts = new CancellationTokenSource();
        _restorePointWriter.OnCreate = cts.Cancel;

        var act = () => _sut.CreateRestorePointAsync(cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _restorePointWriter.Removed.Should().BeEmpty();
        _wmiApi.ClassInvocations.Select(i => i.Method).Should().Equal("Enable", "Disable");
    }

    [Fact]
    public async Task CreateRestorePointAsync_CancelledWhileVerifying_RemovesThePointAndTurnsItBackOff()
    {
        _mockSystemRestore.Setup(s => s.IsEnabledForC()).Returns(false);
        _restorePointWriter.Success = true;
        using var cts = new CancellationTokenSource();
        _restorePointWriter.OnCreate = () => cts.CancelAfter(TimeSpan.FromMilliseconds(200));

        var act = () => _sut.CreateRestorePointAsync(cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _restorePointWriter.Removed.Should().Equal(42u);
        _wmiApi.ClassInvocations.Select(i => i.Method).Should().Equal("Enable", "Disable");
    }

    [Fact]
    public async Task CreateRestorePointAsync_CancelledBeforeWindowsRegistersThePoint_RemovesItOnceItIs()
    {
        _mockSystemRestore.Setup(s => s.IsEnabledForC()).Returns(false);
        _restorePointWriter.Success = true;
        _restorePointWriter.NotFoundRemovals = 2;
        using var cts = new CancellationTokenSource();
        _restorePointWriter.OnCreate = cts.Cancel;

        var act = () => _sut.CreateRestorePointAsync(cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _restorePointWriter.RemoveAttempts.Should().Be(3);
        _restorePointWriter.Removed.Should().Equal(42u);
        _wmiApi.ClassInvocations.Select(i => i.Method).Should().Equal("Enable", "Disable");
    }

    [Fact]
    public async Task CreateRestorePointAsync_CancelledDuringTheQueryThatFindsThePoint_RemovesItAndThrows()
    {
        _mockSystemRestore.Setup(s => s.IsEnabledForC()).Returns(true);
        _restorePointWriter.Success = true;
        _wmiApi.For("SystemRestore").Add(new FakeWmiInstance
        {
            ["CreationTime"] = ManagementDateTimeConverter.ToDmtfDateTime(DateTime.Now),
        });
        using var cts = new CancellationTokenSource();
        _wmiApi.OnQuery = cts.Cancel;

        var act = () => _sut.CreateRestorePointAsync(cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _restorePointWriter.Removed.Should().Equal(42u);
    }

    [Fact]
    public async Task CreateRestorePointAsync_FailsAfterTurningProtectionOn_SaysSoAndLeavesTheChoiceToTheUser()
    {
        _mockSystemRestore.Setup(s => s.IsEnabledForC()).Returns(false);

        var result = await _sut.CreateRestorePointAsync();

        result.Success.Should().BeFalse();
        result.TurnedProtectionOn.Should().BeTrue();
        _wmiApi.ClassInvocations.Select(i => i.Method).Should().Equal("Enable");
    }

    [Fact]
    public async Task CreateRestorePointAsync_FailsWithProtectionAlreadyOn_DoesNotClaimToHaveTurnedItOn()
    {
        _mockSystemRestore.Setup(s => s.IsEnabledForC()).Returns(true);

        var result = await _sut.CreateRestorePointAsync();

        result.Success.Should().BeFalse();
        result.TurnedProtectionOn.Should().BeFalse();
        _wmiApi.ClassInvocations.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateRestorePointAsync_FailureResult_ContainsErrorMessage()
    {
        var result = await _sut.CreateRestorePointAsync();

        if (!result.Success)
        {
            result.ErrorMessage.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public async Task CreateRestorePointAsync_LogsStartOfProcess()
    {
        await _sut.CreateRestorePointAsync();

        _mockLog.Verify(
            l => l.Log(
                Core.Features.Common.Enums.LogLevel.Info,
                It.Is<string>(s => s.Contains("Creating restore point")),
                It.IsAny<Exception?>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateRestorePointAsync_WithCustomName_UsesProvidedName()
    {
        var customName = "My Custom Restore Point";
        _mockSystemRestore.Setup(s => s.IsEnabledForC()).Returns(true);
        _restorePointWriter.Success = true;
        // Found on the first verification attempt (fake ignores the WHERE condition), so this
        // pays exactly one VerificationRetryDelay (3s), not the full 10-attempt loop.
        _wmiApi.For("SystemRestore").Add(new FakeWmiInstance
        {
            ["CreationTime"] = ManagementDateTimeConverter.ToDmtfDateTime(DateTime.Now),
        });

        var result = await _sut.CreateRestorePointAsync(name: customName);

        result.Success.Should().BeTrue();
        _restorePointWriter.LastDescription.Should().Be(customName);
        // Not Times.Once: the name legitimately appears in more than one log line across the
        // Creating/Querying/Found sequence - see FindRestorePointAsync above.
        _mockLog.Verify(
            l => l.Log(
                Core.Features.Common.Enums.LogLevel.Info,
                It.Is<string>(s => s.Contains(customName)),
                It.IsAny<Exception?>(), It.IsAny<string>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task FindRestorePointAsync_MatchFound_ReturnsCreationDate()
    {
        // Round-trip through the same converter FindRestorePointAsync uses (ToDateTime returns
        // LOCAL time), rather than hand-formatting a DMTF string and guessing its offset handling.
        var expected = new DateTime(2026, 8, 31, 10, 0, 0);
        _wmiApi.For("SystemRestore").Add(new FakeWmiInstance
        {
            ["CreationTime"] = ManagementDateTimeConverter.ToDmtfDateTime(expected),
        });

        var found = await _sut.FindRestorePointAsync("My Custom Restore Point");

        found.Should().BeCloseTo(expected, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task FindRestorePointAsync_NoMatch_ReturnsNull()
    {
        var found = await _sut.FindRestorePointAsync("Nothing matches this");

        found.Should().BeNull();
    }

    [Fact]
    public async Task EnableSystemRestoreAsync_InvokesEnableWithTheSystemDrive()
    {
        var enabled = await _sut.EnableSystemRestoreAsync();

        enabled.Should().BeTrue();
        _wmiApi.ClassInvocations.Should().ContainSingle(i => i.ClassName == "SystemRestore" && i.Method == "Enable");
        _wmiApi.ClassInvocations.Single().Parameters.Should().ContainKey("Drive");
    }

    [Fact]
    public void BackupResult_CreateSuccess_SetsCorrectProperties()
    {
        var date = new DateTime(2025, 1, 15);
        var result = BackupResult.CreateSuccess(
            restorePointDate: date,
            restorePointCreated: true);

        result.Success.Should().BeTrue();
        result.RestorePointDate.Should().Be(date);
        result.RestorePointCreated.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void BackupResult_CreateFailure_SetsCorrectProperties()
    {
        var result = BackupResult.CreateFailure("Something went wrong");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Something went wrong");
        result.RestorePointCreated.Should().BeFalse();
    }
}

internal sealed class FakeSystemRestorePointWriter : ISystemRestorePointWriter
{
    public bool Success { get; set; }

    public int StatusCode { get; set; } = -1;

    public uint? SequenceNumber { get; set; } = 42;

    public Action? OnCreate { get; set; }

    public string? LastDescription { get; private set; }

    public int NotFoundRemovals { get; set; }

    public int RemoveAttempts { get; private set; }

    public List<uint> Removed { get; } = [];

    public (bool Success, int StatusCode, uint? SequenceNumber) CreateRestorePoint(string description)
    {
        LastDescription = description;
        OnCreate?.Invoke();
        return (Success, StatusCode, Success ? SequenceNumber : null);
    }

    public int RemoveRestorePoint(uint sequenceNumber)
    {
        if (++RemoveAttempts <= NotFoundRemovals)
        {
            return 2;
        }

        Removed.Add(sequenceNumber);
        return 0;
    }
}

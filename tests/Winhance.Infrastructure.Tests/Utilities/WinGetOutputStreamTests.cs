using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Utilities;
using Xunit;

namespace Winhance.Infrastructure.Tests.Utilities;

public class WinGetOutputStreamTests
{
    private readonly Mock<ITaskProgressService> _progress = new();
    private readonly Mock<ILocalizationService> _localization = new();
    private readonly Mock<ILogService> _log = new();
    private readonly List<TaskProgressDetail> _reported = [];

    public WinGetOutputStreamTests()
    {
        _progress.Setup(p => p.UpdateDetailedProgress(It.IsAny<TaskProgressDetail>())).Callback<TaskProgressDetail>(_reported.Add);
        _localization.Setup(l => l.GetString(It.IsAny<string>())).Returns((string key) => key);
        _localization.Setup(l => l.GetString(It.IsAny<string>(), It.IsAny<object[]>())).Returns((string key, object[] _) => key);
    }

    private WinGetOutputStream Stream(WinGetOperation operation) =>
        new(operation, "VLC", _progress.Object, _localization.Object, _log.Object);

    [Fact]
    public void HandleOutputLine_InstallSpinnerFragment_StillReachesTheTerminal()
    {
        Stream(WinGetOperation.Install).HandleOutputLine("-");

        _reported.Should().ContainSingle().Which.TerminalOutput.Should().Be("-");
    }

    [Fact]
    public void HandleOutputLine_InstallComplete_StillEndsAtOneHundred()
    {
        Stream(WinGetOperation.Install).HandleOutputLine("Successfully installed");

        _reported.Should().ContainSingle().Which.Progress.Should().Be(100);
    }

    [Fact]
    public void HandleOutputLine_RegistrationDeferredKey_MarksTheInstallDeferred()
    {
        var stream = Stream(WinGetOperation.Install);

        stream.HandleOutputLine("Successfully installed");
        stream.HandleOutputLine("InstallFlowRegistrationDeferred");

        stream.RegistrationDeferred.Should().BeTrue();
    }

    [Fact]
    public void HandleOutputLine_PlainSuccess_IsNotDeferred()
    {
        var stream = Stream(WinGetOperation.Install);

        stream.HandleOutputLine("Successfully installed");

        stream.RegistrationDeferred.Should().BeFalse();
    }
}

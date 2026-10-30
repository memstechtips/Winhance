using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Utilities;
using Xunit;

namespace Winhance.Infrastructure.Tests.Utilities;

public class WinGetOutputStreamTests
{
    private const string BootstrapStatus = "Progress_WinGet_Installing";

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

    [Theory]
    [InlineData("ReportIdentityFound App Installer [Microsoft.AppInstaller] ShowVersion 1.27", 15)]
    [InlineData("Downloading 50%", 42)]
    [InlineData("Installing 40%", 80)]
    [InlineData("Successfully installed", 95)]
    public void HandleOutputLine_Bootstrap_ReportsItsOwnPercentRangeAndOneFixedStatus(string line, int expectedPercent)
    {
        Stream(WinGetOperation.Bootstrap).HandleOutputLine(line);

        var detail = _reported.Should().ContainSingle().Subject;
        detail.Progress.Should().Be(expectedPercent);
        detail.StatusText.Should().Be(BootstrapStatus);
    }

    [Fact]
    public void HandleOutputLine_BootstrapPhaseWithNoPlaceOnTheBar_ReportsNothingButStillLogsTheLine()
    {
        Stream(WinGetOperation.Bootstrap).HandleOutputLine("No applicable upgrade");

        _reported.Should().BeEmpty();
        _log.Verify(l => l.LogInformation("No applicable upgrade", It.IsAny<string>()), Times.Once);
    }

    [Theory]
    [InlineData("-")]
    [InlineData("\\")]
    [InlineData("|")]
    [InlineData("/")]
    public void HandleOutputLine_BootstrapSpinnerFragment_ReachesNeitherTheTerminalNorTheLog(string line)
    {
        Stream(WinGetOperation.Bootstrap).HandleOutputLine(line);

        _reported.Should().BeEmpty();
        _log.Verify(l => l.LogInformation(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void HandleOutputLine_BootstrapPlainLine_GoesToTheTerminalAndTheLog()
    {
        Stream(WinGetOperation.Bootstrap).HandleOutputLine("The source agreements were accepted");

        _reported.Should().ContainSingle().Which.TerminalOutput.Should().Be("The source agreements were accepted");
        _log.Verify(l => l.LogInformation("The source agreements were accepted", It.IsAny<string>()), Times.Once);
    }

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
}

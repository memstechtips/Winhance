using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Services;
using Winhance.UI.Helpers;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class MemoryDiagnosticsTests
{
    private readonly Mock<ILogService> _log = new();

    private MemoryDiagnostics CreateDiagnostics(LaunchOptions launch) =>
        new(_log.Object, launch, new Mock<IApplicationModeService>().Object, new Mock<IConfigReviewModeService>().Object);

    [Fact]
    public void Log_WithoutDiagMemory_LogsNothing()
    {
        CreateDiagnostics(LaunchOptions.None).Log("startup");

        _log.Verify(l => l.LogInformation(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Log_WithDiagMemory_LogsTheMemoryLine()
    {
        CreateDiagnostics(LaunchOptions.None with { DiagMemory = true }).Log("startup");

        _log.Verify(l => l.LogInformation(It.Is<string>(m => m.StartsWith("[mem-gc] startup")), It.IsAny<string>()), Times.Once);
    }
}

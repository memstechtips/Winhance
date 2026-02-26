using FluentAssertions;
using Winhance.Core.Features.Common.Services;
using Xunit;

namespace Winhance.Core.Tests.Services;

public class LogServiceTests
{
    [Fact]
    public void GetLogPath_ReturnsNonEmptyPath()
    {
        using var service = new LogService();

        var path = service.GetLogPath();

        path.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetLogPath_ContainsWinhanceDirectory()
    {
        using var service = new LogService();

        var path = service.GetLogPath();

        path.Should().Contain("Winhance");
        path.Should().Contain("Logs");
        path.Should().EndWith(".log");
    }

    [Fact]
    public void Log_WithDifferentLevels_DoesNotThrow()
    {
        using var service = new LogService();

        // Without calling StartLog(), these should still not throw
        // (they just won't write to the file)
        var action = () =>
        {
            service.Log(Winhance.Core.Features.Common.Enums.LogLevel.Info, "info message");
            service.Log(Winhance.Core.Features.Common.Enums.LogLevel.Warning, "warning message");
            service.Log(Winhance.Core.Features.Common.Enums.LogLevel.Error, "error message");
            service.Log(Winhance.Core.Features.Common.Enums.LogLevel.Debug, "debug message");
            service.Log(Winhance.Core.Features.Common.Enums.LogLevel.Success, "success message");
        };

        action.Should().NotThrow();
    }

    [Fact]
    public void Log_WithException_DoesNotThrow()
    {
        using var service = new LogService();

        var ex = new InvalidOperationException("test error");
        var action = () => service.Log(
            Winhance.Core.Features.Common.Enums.LogLevel.Error, "error", ex);

        action.Should().NotThrow();
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        var service = new LogService();

        var action = () =>
        {
            service.Dispose();
            service.Dispose();
        };

        action.Should().NotThrow();
    }

    [Fact]
    public void SetInteractiveUserService_DoesNotThrow()
    {
        using var service = new LogService();
        var mockService = new Moq.Mock<Winhance.Core.Features.Common.Interfaces.IInteractiveUserService>();

        var action = () => service.SetInteractiveUserService(mockService.Object);

        action.Should().NotThrow();
    }
}

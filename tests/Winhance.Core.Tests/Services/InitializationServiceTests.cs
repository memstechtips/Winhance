using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Services;
using Xunit;

namespace Winhance.Core.Tests.Services;

public class InitializationServiceTests
{
    private readonly Mock<ILogService> _mockLog = new();
    private readonly InitializationService _service;

    public InitializationServiceTests()
    {
        _service = new InitializationService(_mockLog.Object);
    }

    [Fact]
    public void CompleteFeatureInitialization_NonExistentFeature_DoesNotThrow()
    {
        var action = () => _service.CompleteFeatureInitialization("NonExistent");
        action.Should().NotThrow();
    }

    [Fact]
    public void StartFeatureInitialization_LogsMessage()
    {
        _service.StartFeatureInitialization("TestFeature");

        _mockLog.Verify(l => l.Log(
            Winhance.Core.Features.Common.Enums.LogLevel.Info,
            It.Is<string>(s => s.Contains("TestFeature") && s.Contains("Started")),
            null, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void CompleteFeatureInitialization_LogsMessage()
    {
        _service.StartFeatureInitialization("TestFeature");
        _service.CompleteFeatureInitialization("TestFeature");

        _mockLog.Verify(l => l.Log(
            Winhance.Core.Features.Common.Enums.LogLevel.Info,
            It.Is<string>(s => s.Contains("TestFeature") && s.Contains("Completed")),
            null, It.IsAny<string>()), Times.Once);
    }
}

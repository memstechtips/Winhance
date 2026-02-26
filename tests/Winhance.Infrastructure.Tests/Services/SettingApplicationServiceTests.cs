using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.Settings;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class SettingApplicationServiceTests
{
    private readonly Mock<IDomainServiceRouter> _mockRouter = new();
    private readonly Mock<ILogService> _mockLog = new();
    private readonly Mock<IGlobalSettingsRegistry> _mockRegistry = new();
    private readonly Mock<IEventBus> _mockEventBus = new();
    private readonly Mock<IRecommendedSettingsApplier> _mockRecommended = new();
    private readonly Mock<IProcessRestartManager> _mockRestart = new();
    private readonly Mock<ISettingDependencyResolver> _mockDepResolver = new();
    private readonly Mock<IWindowsCompatibilityFilter> _mockCompatFilter = new();
    private readonly Mock<ISettingOperationExecutor> _mockExecutor = new();
    private readonly SettingApplicationService _service;

    public SettingApplicationServiceTests()
    {
        _mockExecutor
            .Setup(e => e.ApplySettingOperationsAsync(
                It.IsAny<SettingDefinition>(), It.IsAny<bool>(), It.IsAny<object?>()))
            .ReturnsAsync(OperationResult.Succeeded());

        _service = new SettingApplicationService(
            _mockRouter.Object, _mockLog.Object, _mockRegistry.Object,
            _mockEventBus.Object, _mockRecommended.Object, _mockRestart.Object,
            _mockDepResolver.Object, _mockCompatFilter.Object, _mockExecutor.Object);
    }

    private static SettingDefinition CreateSetting(string id) => new()
    {
        Id = id,
        Name = $"Setting {id}",
        Description = $"Description for {id}",
    };

    private void SetupDomainServiceWithSetting(string settingId, IDomainService? domainService = null)
    {
        var setting = CreateSetting(settingId);
        var mockDomain = domainService != null ? null : new Mock<IDomainService>();

        if (mockDomain != null)
        {
            mockDomain.Setup(d => d.GetSettingsAsync())
                .ReturnsAsync(new[] { setting });
            mockDomain.Setup(d => d.DomainName).Returns("TestDomain");
            _mockRouter.Setup(r => r.GetDomainService(settingId))
                .Returns(mockDomain.Object);
        }
        else
        {
            _mockRouter.Setup(r => r.GetDomainService(settingId))
                .Returns(domainService!);
        }
    }

    [Fact]
    public async Task ApplySettingAsync_ValidSetting_ReturnsSuccess()
    {
        SetupDomainServiceWithSetting("test-setting");

        var result = await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = "test-setting",
            Enable = true,
        });

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ApplySettingAsync_ValidSetting_PublishesEvent()
    {
        SetupDomainServiceWithSetting("test-setting");

        await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = "test-setting",
            Enable = true,
        });

        _mockEventBus.Verify(e => e.Publish(It.Is<SettingAppliedEvent>(
            evt => evt.SettingId == "test-setting")), Times.Once);
    }

    [Fact]
    public async Task ApplySettingAsync_ValidSetting_CallsOperationExecutor()
    {
        SetupDomainServiceWithSetting("test-setting");

        await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = "test-setting",
            Enable = true,
        });

        _mockExecutor.Verify(e => e.ApplySettingOperationsAsync(
            It.Is<SettingDefinition>(s => s.Id == "test-setting"),
            true,
            null), Times.Once);
    }

    [Fact]
    public async Task ApplySettingAsync_SettingNotFound_ThrowsArgumentException()
    {
        var mockDomain = new Mock<IDomainService>();
        mockDomain.Setup(d => d.GetSettingsAsync())
            .ReturnsAsync(Array.Empty<SettingDefinition>());
        mockDomain.Setup(d => d.DomainName).Returns("TestDomain");
        _mockRouter.Setup(r => r.GetDomainService("missing"))
            .Returns(mockDomain.Object);

        var action = () => _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = "missing",
            Enable = true,
        });

        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*missing*not found*");
    }

    [Fact]
    public async Task ApplySettingAsync_WithCommandString_ExecutesCommand()
    {
        var mockDomain = new Mock<IDomainService>();
        var mockCommand = mockDomain.As<IActionCommandProvider>();
        mockDomain.Setup(d => d.GetSettingsAsync())
            .ReturnsAsync(new[] { CreateSetting("cmd-setting") });
        mockDomain.Setup(d => d.DomainName).Returns("TestDomain");
        mockCommand.Setup(c => c.SupportedCommands).Returns(new HashSet<string> { "do-action" });
        _mockRouter.Setup(r => r.GetDomainService("cmd-setting"))
            .Returns(mockDomain.Object);

        var result = await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = "cmd-setting",
            Enable = true,
            CommandString = "do-action",
        });

        result.Success.Should().BeTrue();
        mockCommand.Verify(c => c.ExecuteCommandAsync("do-action"), Times.Once);
    }

    [Fact]
    public async Task ApplySettingAsync_RegistersSettingInGlobalRegistry()
    {
        SetupDomainServiceWithSetting("test-setting");

        await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = "test-setting",
            Enable = true,
        });

        _mockRegistry.Verify(r => r.RegisterSetting("TestDomain",
            It.Is<SettingDefinition>(s => s.Id == "test-setting")), Times.Once);
    }

    [Fact]
    public async Task ApplySettingAsync_HandlesDependencies()
    {
        SetupDomainServiceWithSetting("test-setting");

        await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = "test-setting",
            Enable = true,
        });

        _mockDepResolver.Verify(d => d.HandleDependenciesAsync(
            "test-setting",
            It.IsAny<IEnumerable<SettingDefinition>>(),
            true,
            null,
            _service), Times.Once);
    }

    [Fact]
    public async Task ApplySettingAsync_SkipValuePrerequisites_SkipsDependencies()
    {
        SetupDomainServiceWithSetting("test-setting");

        await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = "test-setting",
            Enable = true,
            SkipValuePrerequisites = true,
        });

        _mockDepResolver.Verify(d => d.HandleDependenciesAsync(
            It.IsAny<string>(),
            It.IsAny<IEnumerable<SettingDefinition>>(),
            It.IsAny<bool>(),
            It.IsAny<object?>(),
            It.IsAny<ISettingApplicationService>()), Times.Never);
    }

    [Fact]
    public async Task ApplyRecommendedSettingsForDomainAsync_DelegatesToApplier()
    {
        await _service.ApplyRecommendedSettingsForDomainAsync("test-id");

        _mockRecommended.Verify(r => r.ApplyRecommendedSettingsForDomainAsync(
            "test-id", _service), Times.Once);
    }

    // ---------------------------------------------------------------
    // BP-1: Failure propagation from OperationExecutor
    // ---------------------------------------------------------------

    [Fact]
    public async Task ApplySettingAsync_ExecutorFails_PropagatesFailedResult()
    {
        SetupDomainServiceWithSetting("fail-setting");
        _mockExecutor
            .Setup(e => e.ApplySettingOperationsAsync(
                It.Is<SettingDefinition>(s => s.Id == "fail-setting"),
                It.IsAny<bool>(), It.IsAny<object?>()))
            .ReturnsAsync(OperationResult.Failed("Registry write denied"));

        var result = await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = "fail-setting",
            Enable = true,
        });

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Registry write denied");
    }

    [Fact]
    public async Task ApplySettingAsync_ExecutorFails_StillPublishesEvent()
    {
        SetupDomainServiceWithSetting("fail-event");
        _mockExecutor
            .Setup(e => e.ApplySettingOperationsAsync(
                It.Is<SettingDefinition>(s => s.Id == "fail-event"),
                It.IsAny<bool>(), It.IsAny<object?>()))
            .ReturnsAsync(OperationResult.Failed("Some failure"));

        await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = "fail-event",
            Enable = true,
        });

        _mockEventBus.Verify(e => e.Publish(It.Is<SettingAppliedEvent>(
            evt => evt.SettingId == "fail-event")), Times.Once);
    }

    [Fact]
    public async Task ApplySettingAsync_ExecutorSucceeds_ReturnsSuccess()
    {
        SetupDomainServiceWithSetting("ok-setting");
        _mockExecutor
            .Setup(e => e.ApplySettingOperationsAsync(
                It.Is<SettingDefinition>(s => s.Id == "ok-setting"),
                It.IsAny<bool>(), It.IsAny<object?>()))
            .ReturnsAsync(OperationResult.Succeeded());

        var result = await _service.ApplySettingAsync(new ApplySettingRequest
        {
            SettingId = "ok-setting",
            Enable = true,
        });

        result.Success.Should().BeTrue();
    }
}

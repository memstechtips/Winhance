using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Services;
using Xunit;

namespace Winhance.Core.Tests.Services;

public class DependencyManagerTests
{
    private readonly Mock<ILogService> _mockLog = new();
    private readonly Mock<IGlobalSettingsRegistry> _mockRegistry = new();
    private readonly Mock<ISettingApplicationService> _mockSettingApp = new();
    private readonly Mock<ISystemSettingsDiscoveryService> _mockDiscovery = new();
    private readonly DependencyManager _manager;

    public DependencyManagerTests()
    {
        _manager = new DependencyManager(_mockLog.Object, _mockRegistry.Object);
    }

    private static SettingDefinition CreateSetting(
        string id,
        IReadOnlyList<SettingDependency>? dependencies = null) => new()
    {
        Id = id,
        Name = $"Setting {id}",
        Description = $"Description for {id}",
        Dependencies = dependencies ?? Array.Empty<SettingDependency>(),
    };

    [Fact]
    public async Task HandleSettingEnabled_NoDependencies_ReturnsTrue()
    {
        var settings = new[] { CreateSetting("s1") };

        var result = await _manager.HandleSettingEnabledAsync(
            "s1", settings, _mockSettingApp.Object, _mockDiscovery.Object);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task HandleSettingEnabled_SettingNotFound_ReturnsTrue()
    {
        var settings = Array.Empty<SettingDefinition>();
        _mockRegistry.Setup(r => r.GetSetting(It.IsAny<string>(), null)).Returns((ISettingItem?)null);

        var result = await _manager.HandleSettingEnabledAsync(
            "nonexistent", settings, _mockSettingApp.Object, _mockDiscovery.Object);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task HandleSettingEnabled_DependencyNotFound_ReturnsFalse()
    {
        var dep = new SettingDependency
        {
            DependentSettingId = "s1",
            RequiredSettingId = "missing-dep",
            DependencyType = SettingDependencyType.RequiresEnabled,
        };
        var settings = new ISettingItem[] { CreateSetting("s1", new[] { dep }) };

        // FindSetting for "missing-dep" returns null from both local and registry
        _mockRegistry.Setup(r => r.GetSetting("missing-dep", null)).Returns((ISettingItem?)null);

        var result = await _manager.HandleSettingEnabledAsync(
            "s1", settings, _mockSettingApp.Object, _mockDiscovery.Object);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task HandleSettingDisabled_NoDependents_DoesNotApplyAnything()
    {
        var settings = new ISettingItem[] { CreateSetting("s1") };

        await _manager.HandleSettingDisabledAsync(
            "s1", settings, _mockSettingApp.Object, _mockDiscovery.Object);

        _mockSettingApp.Verify(
            s => s.ApplySettingAsync(It.IsAny<ApplySettingRequest>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleSettingDisabled_WithEnabledDependent_DisablesDependent()
    {
        var dep = new SettingDependency
        {
            DependentSettingId = "child",
            RequiredSettingId = "parent",
            DependencyType = SettingDependencyType.RequiresEnabled,
        };
        var parent = CreateSetting("parent");
        var child = CreateSetting("child", new[] { dep });
        var settings = new ISettingItem[] { parent, child };

        // GetSettingStateAsync for "child": looks up in registry, then calls discovery
        _mockRegistry.Setup(r => r.GetSetting("child", null)).Returns(child);
        _mockDiscovery.Setup(d => d.GetSettingStatesAsync(It.IsAny<IEnumerable<SettingDefinition>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["child"] = new SettingStateResult { Success = true, IsEnabled = true },
            });
        _mockSettingApp.Setup(s => s.ApplySettingAsync(It.IsAny<ApplySettingRequest>()))
            .ReturnsAsync(OperationResult.Succeeded());

        await _manager.HandleSettingDisabledAsync(
            "parent", settings, _mockSettingApp.Object, _mockDiscovery.Object);

        _mockSettingApp.Verify(s => s.ApplySettingAsync(
            It.Is<ApplySettingRequest>(r => r.SettingId == "child" && r.Enable == false)),
            Times.Once);
    }

    [Fact]
    public async Task HandleSettingDisabled_DependentAlreadyDisabled_DoesNotDisableAgain()
    {
        var dep = new SettingDependency
        {
            DependentSettingId = "child",
            RequiredSettingId = "parent",
            DependencyType = SettingDependencyType.RequiresEnabled,
        };
        var child = CreateSetting("child", new[] { dep });
        var settings = new ISettingItem[]
        {
            CreateSetting("parent"),
            child,
        };

        _mockRegistry.Setup(r => r.GetSetting("child", null)).Returns(child);
        _mockDiscovery.Setup(d => d.GetSettingStatesAsync(It.IsAny<IEnumerable<SettingDefinition>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["child"] = new SettingStateResult { Success = true, IsEnabled = false },
            });

        await _manager.HandleSettingDisabledAsync(
            "parent", settings, _mockSettingApp.Object, _mockDiscovery.Object);

        _mockSettingApp.Verify(
            s => s.ApplySettingAsync(It.IsAny<ApplySettingRequest>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleSettingValueChanged_NoSpecificValueDependents_DoesNothing()
    {
        var settings = new ISettingItem[] { CreateSetting("s1") };

        await _manager.HandleSettingValueChangedAsync(
            "s1", settings, _mockSettingApp.Object, _mockDiscovery.Object);

        _mockSettingApp.Verify(
            s => s.ApplySettingAsync(It.IsAny<ApplySettingRequest>()),
            Times.Never);
    }
}

using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Services;
using Xunit;

namespace Winhance.Core.Tests.Services;

public class GlobalSettingsRegistryTests
{
    private readonly Mock<ILogService> _mockLog = new();
    private readonly GlobalSettingsRegistry _registry;

    public GlobalSettingsRegistryTests()
    {
        _registry = new GlobalSettingsRegistry(_mockLog.Object);
    }

    private static SettingDefinition CreateSetting(string id) => new()
    {
        Id = id,
        Name = $"Setting {id}",
        Description = $"Description for {id}",
    };

    [Fact]
    public void RegisterSettings_WithValidModule_StoresSettings()
    {
        var settings = new[] { CreateSetting("s1"), CreateSetting("s2") };

        _registry.RegisterSettings("TestModule", settings);

        _registry.GetAllSettings().Should().HaveCount(2);
    }

    [Fact]
    public void RegisterSettings_WithNullModuleName_LogsWarningAndReturns()
    {
        _registry.RegisterSettings(null!, new[] { CreateSetting("s1") });

        _registry.GetAllSettings().Should().BeEmpty();
        _mockLog.Verify(l => l.Log(
            Winhance.Core.Features.Common.Enums.LogLevel.Warning,
            It.Is<string>(s => s.Contains("null or empty")),
            null), Times.Once);
    }

    [Fact]
    public void RegisterSettings_WithEmptyModuleName_LogsWarningAndReturns()
    {
        _registry.RegisterSettings("", new[] { CreateSetting("s1") });

        _registry.GetAllSettings().Should().BeEmpty();
    }

    [Fact]
    public void RegisterSettings_ReplacesExistingModule()
    {
        _registry.RegisterSettings("Module", new[] { CreateSetting("old") });
        _registry.RegisterSettings("Module", new[] { CreateSetting("new1"), CreateSetting("new2") });

        var all = _registry.GetAllSettings().ToList();
        all.Should().HaveCount(2);
        all.Should().Contain(s => s.Id == "new1");
        all.Should().Contain(s => s.Id == "new2");
    }

    [Fact]
    public void RegisterSettings_WithNullSettings_StoresEmptyList()
    {
        _registry.RegisterSettings("Module", null!);

        _registry.GetAllSettings().Should().BeEmpty();
    }

    [Fact]
    public void GetSetting_ByIdWithinModule_ReturnsSetting()
    {
        _registry.RegisterSettings("Module", new[] { CreateSetting("target") });

        var result = _registry.GetSetting("target", "Module");

        result.Should().NotBeNull();
        result!.Id.Should().Be("target");
    }

    [Fact]
    public void GetSetting_ByIdAcrossAllModules_ReturnsSetting()
    {
        _registry.RegisterSettings("Module", new[] { CreateSetting("target") });

        var result = _registry.GetSetting("target");

        result.Should().NotBeNull();
        result!.Id.Should().Be("target");
    }

    [Fact]
    public void GetSetting_NotFound_ReturnsNull()
    {
        _registry.RegisterSettings("Module", new[] { CreateSetting("other") });

        _registry.GetSetting("nonexistent").Should().BeNull();
    }

    [Fact]
    public void GetSetting_WithNullId_ReturnsNull()
    {
        _registry.GetSetting(null!).Should().BeNull();
    }

    [Fact]
    public void GetSetting_WithEmptyId_ReturnsNull()
    {
        _registry.GetSetting("").Should().BeNull();
    }

    [Fact]
    public void GetSetting_WrongModule_ReturnsNull()
    {
        _registry.RegisterSettings("Module1", new[] { CreateSetting("s1") });

        _registry.GetSetting("s1", "Module2").Should().BeNull();
    }

    [Fact]
    public void RegisterSetting_SingleSetting_AddsToModule()
    {
        _registry.RegisterSetting("Module", CreateSetting("s1"));

        _registry.GetSetting("s1", "Module").Should().NotBeNull();
    }

    [Fact]
    public void RegisterSetting_DuplicateId_DoesNotAddAgain()
    {
        _registry.RegisterSetting("Module", CreateSetting("s1"));
        _registry.RegisterSetting("Module", CreateSetting("s1"));

        _registry.GetAllSettings().Should().HaveCount(1);
    }

    [Fact]
    public void RegisterSetting_DifferentIds_AddsAll()
    {
        _registry.RegisterSetting("Module", CreateSetting("s1"));
        _registry.RegisterSetting("Module", CreateSetting("s2"));

        _registry.GetAllSettings().Should().HaveCount(2);
    }

    [Fact]
    public void RegisterSetting_NullModuleName_LogsWarning()
    {
        _registry.RegisterSetting(null!, CreateSetting("s1"));

        _registry.GetAllSettings().Should().BeEmpty();
    }

    [Fact]
    public void RegisterSetting_NullSetting_LogsWarning()
    {
        _registry.RegisterSetting("Module", null!);

        _registry.GetAllSettings().Should().BeEmpty();
    }

    [Fact]
    public void GetAllSettings_MultipleModules_ReturnsFlattened()
    {
        _registry.RegisterSettings("M1", new[] { CreateSetting("a"), CreateSetting("b") });
        _registry.RegisterSettings("M2", new[] { CreateSetting("c") });

        _registry.GetAllSettings().Should().HaveCount(3);
    }

    [Fact]
    public void GetAllSettings_Empty_ReturnsEmpty()
    {
        _registry.GetAllSettings().Should().BeEmpty();
    }
}

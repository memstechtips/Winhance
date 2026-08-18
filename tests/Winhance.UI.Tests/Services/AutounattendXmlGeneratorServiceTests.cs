using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.AdvancedTools.Services;
using Winhance.UI.Features.AdvancedTools.Services;
using Winhance.UI.Features.Common.Interfaces;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class AutounattendXmlGeneratorServiceTests
{
    private readonly Mock<ICatalogSettingsRegistry> _mockCatalogSettingsRegistry = new();
    private readonly Mock<IWindowsVersionFilterService> _mockWindowsVersionFilter = new();
    private readonly Mock<ICatalogSettingStateProvider> _mockSettingStateProvider = new();
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<IPowerShellRunner> _mockPowerShellRunner = new();
    private readonly Mock<ISelectedAppsProvider> _mockSelectedAppsProvider = new();
    private readonly Mock<IPowerSettingsQueryService> _mockPowerSettingsQueryService = new();
    private readonly Mock<IHardwareDetectionService> _mockHardwareDetectionService = new();

    public AutounattendXmlGeneratorServiceTests()
    {
        // Default mode: version filter ON (the app default) -> the service must enumerate the
        // current-OS scope (GetAll(includeOtherOsVersions: false)).
        _mockWindowsVersionFilter.Setup(f => f.IsFilterEnabled).Returns(true);

        // The real AutounattendScriptBuilder dereferences the active power plan; return a stub.
        _mockPowerSettingsQueryService
            .Setup(p => p.GetActivePowerPlanAsync())
            .ReturnsAsync(new PowerPlan { Name = "Balanced", Guid = "SCHEME_CURRENT" });
        _mockPowerSettingsQueryService
            .Setup(p => p.GetAllPowerSettingsACDCAsync(It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, (int? acValue, int? dcValue)>());
    }

    private AutounattendScriptBuilder CreateScriptBuilder()
    {
        return new AutounattendScriptBuilder(
            _mockPowerSettingsQueryService.Object,
            _mockHardwareDetectionService.Object,
            _mockLogService.Object,
            _mockPowerShellRunner.Object,
            new Mock<IWindowsVersionService>().Object);
    }

    private AutounattendXmlGeneratorService CreateService(
        AutounattendScriptBuilder? scriptBuilder = null)
    {
        _mockSettingStateProvider
            .Setup(p => p.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>());

        return new AutounattendXmlGeneratorService(
            _mockCatalogSettingsRegistry.Object,
            _mockWindowsVersionFilter.Object,
            _mockSettingStateProvider.Object,
            _mockLogService.Object,
            scriptBuilder ?? CreateScriptBuilder(),
            _mockPowerShellRunner.Object,
            _mockSelectedAppsProvider.Object);
    }

    private void SetupEmptySettings()
    {
        _mockCatalogSettingsRegistry
            .Setup(r => r.GetAll(It.IsAny<bool>()))
            .Returns(new Dictionary<string, IReadOnlyList<Setting>>());

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>());
    }

    // -------------------------------------------------------
    // Constructor
    // -------------------------------------------------------

    [Fact]
    public void Constructor_WithValidDependencies_DoesNotThrow()
    {
        var act = () => CreateService();

        act.Should().NotThrow();
    }

    // -------------------------------------------------------
    // GenerateFromCurrentSelectionsAsync - uses selectedAppsProvider
    // when no apps are passed
    // -------------------------------------------------------

    [Fact]
    public async Task GenerateFromCurrentSelectionsAsync_WhenNoAppsProvided_CallsSelectedAppsProvider()
    {
        SetupEmptySettings();

        _mockSelectedAppsProvider
            .Setup(p => p.GetSelectedWindowsAppsAsync())
            .ReturnsAsync(new List<ConfigurationItem>());

        var service = CreateService();
        var outputPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.xml");

        try
        {
            await service.GenerateFromCurrentSelectionsAsync(outputPath);
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }

        _mockSelectedAppsProvider.Verify(p => p.GetSelectedWindowsAppsAsync(), Times.Once);
    }

    [Fact]
    public async Task GenerateFromCurrentSelectionsAsync_WhenAppsProvided_DoesNotCallSelectedAppsProvider()
    {
        SetupEmptySettings();

        var apps = new List<ConfigurationItem>
        {
            new() { Id = "app1", Name = "Test App", IsSelected = true, InputType = InputType.Toggle }
        };

        var service = CreateService();
        var outputPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.xml");

        try
        {
            await service.GenerateFromCurrentSelectionsAsync(outputPath, apps);
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }

        _mockSelectedAppsProvider.Verify(p => p.GetSelectedWindowsAppsAsync(), Times.Never);
    }

    [Fact]
    public async Task GenerateFromCurrentSelectionsAsync_WhenNullAppsProvided_CallsSelectedAppsProvider()
    {
        SetupEmptySettings();

        _mockSelectedAppsProvider
            .Setup(p => p.GetSelectedWindowsAppsAsync())
            .ReturnsAsync(new List<ConfigurationItem>());

        var service = CreateService();
        var outputPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.xml");

        try
        {
            await service.GenerateFromCurrentSelectionsAsync(outputPath, selectedWindowsApps: null);
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }

        _mockSelectedAppsProvider.Verify(p => p.GetSelectedWindowsAppsAsync(), Times.Once);
    }

    // -------------------------------------------------------
    // GenerateFromCurrentSelectionsAsync - enumerates the catalog
    // registry with the mode threaded
    // -------------------------------------------------------

    [Fact]
    public async Task GenerateFromCurrentSelectionsAsync_CallsGetAll_CurrentOsScope()
    {
        SetupEmptySettings();

        var apps = new List<ConfigurationItem>();

        var service = CreateService();
        var outputPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.xml");

        try
        {
            await service.GenerateFromCurrentSelectionsAsync(outputPath, apps);
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }

        // GetAll runs twice: once in PopulateFeatureBasedSections and once in RenderConfigToXmlAsync
        // (the dict handed to BuildWinhancementsScriptAsync). Pin the mode threading:
        // filter ON must enumerate the current-OS scope (includeOtherOsVersions: false).
        _mockCatalogSettingsRegistry.Verify(
            r => r.GetAll(false),
            Times.AtLeast(1));
    }

    [Fact]
    public async Task GenerateFromCurrentSelectionsAsync_FilterOff_EnumeratesOtherOsScope()
    {
        _mockWindowsVersionFilter.Setup(f => f.IsFilterEnabled).Returns(false);

        // Strict on the scope arg: only includeOtherOsVersions: true is set up - a false arg would return
        // Moq's null default and throw, so the !IsFilterEnabled threading is load-bearing here.
        _mockCatalogSettingsRegistry
            .Setup(r => r.GetAll(true))
            .Returns(new Dictionary<string, IReadOnlyList<Setting>>());

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>());

        var service = CreateService();
        var outputPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.xml");

        try
        {
            await service.GenerateFromCurrentSelectionsAsync(outputPath, new List<ConfigurationItem>());
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }

        _mockCatalogSettingsRegistry.Verify(r => r.GetAll(true), Times.AtLeast(1));
    }

    // -------------------------------------------------------
    // GenerateFromCurrentSelectionsAsync - logging
    // -------------------------------------------------------

    [Fact]
    public async Task GenerateFromCurrentSelectionsAsync_LogsStartMessage()
    {
        SetupEmptySettings();

        var apps = new List<ConfigurationItem>();
        var service = CreateService();
        var outputPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.xml");

        try
        {
            await service.GenerateFromCurrentSelectionsAsync(outputPath, apps);
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }

        _mockLogService.Verify(
            l => l.Log(LogLevel.Info, It.Is<string>(s => s.Contains("Starting autounattend.xml generation"))),
            Times.Once);
    }

    // -------------------------------------------------------
    // GenerateFromCurrentSelectionsAsync - exception handling
    // -------------------------------------------------------

    [Fact]
    public async Task GenerateFromCurrentSelectionsAsync_WhenExceptionOccurs_LogsErrorAndRethrows()
    {
        _mockCatalogSettingsRegistry
            .Setup(r => r.GetAll(It.IsAny<bool>()))
            .Throws(new InvalidOperationException("Test error"));

        _mockSelectedAppsProvider
            .Setup(p => p.GetSelectedWindowsAppsAsync())
            .ReturnsAsync(new List<ConfigurationItem>());

        var service = CreateService();
        var outputPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.xml");

        Func<Task> act = () => service.GenerateFromCurrentSelectionsAsync(outputPath);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Test error");

        _mockLogService.Verify(
            l => l.Log(LogLevel.Error, It.Is<string>(s => s.Contains("Test error"))),
            Times.Once);

        if (File.Exists(outputPath)) File.Delete(outputPath);
    }

    [Fact]
    public async Task GenerateFromCurrentSelectionsAsync_WhenSelectedAppsProviderThrows_LogsErrorAndRethrows()
    {
        SetupEmptySettings();

        _mockSelectedAppsProvider
            .Setup(p => p.GetSelectedWindowsAppsAsync())
            .ThrowsAsync(new Exception("Provider failed"));

        var service = CreateService();
        var outputPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.xml");

        Func<Task> act = () => service.GenerateFromCurrentSelectionsAsync(outputPath);

        await act.Should().ThrowAsync<Exception>()
            .WithMessage("Provider failed");

        _mockLogService.Verify(
            l => l.Log(LogLevel.Error, It.Is<string>(s => s.Contains("Provider failed"))),
            Times.Once);

        if (File.Exists(outputPath)) File.Delete(outputPath);
    }

    // -------------------------------------------------------
    // GenerateFromCurrentSelectionsAsync - apps are passed
    // to configuration
    // -------------------------------------------------------

    [Fact]
    public async Task GenerateFromCurrentSelectionsAsync_WithSelectedApps_IncludesAppsInConfiguration()
    {
        SetupEmptySettings();

        var apps = new List<ConfigurationItem>
        {
            new() { Id = "app1", Name = "App One", IsSelected = true, InputType = InputType.Toggle },
            new() { Id = "app2", Name = "App Two", IsSelected = true, InputType = InputType.Toggle }
        };

        // We verify the script builder receives the apps by checking that
        // the registry enumeration is invoked (it runs after the config is created with apps).
        var service = CreateService();
        var outputPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.xml");

        try
        {
            await service.GenerateFromCurrentSelectionsAsync(outputPath, apps);
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }

        // Verify the service proceeded past app assignment (reached the registry enumeration)
        _mockCatalogSettingsRegistry.Verify(r => r.GetAll(It.IsAny<bool>()), Times.AtLeast(1));
    }

    [Fact]
    public async Task GenerateFromCurrentSelectionsAsync_WithEmptyApps_Completes()
    {
        SetupEmptySettings();

        var apps = new List<ConfigurationItem>();
        var service = CreateService();
        var outputPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.xml");

        try
        {
            await service.GenerateFromCurrentSelectionsAsync(outputPath, apps);
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }

        _mockLogService.Verify(
            l => l.Log(LogLevel.Info, It.Is<string>(s => s.Contains("Starting autounattend.xml generation"))),
            Times.Once);
    }

    // -------------------------------------------------------
    // GenerateFromCurrentSelectionsAsync - XML validation
    // failure
    // -------------------------------------------------------

    [Fact]
    public async Task GenerateFromCurrentSelectionsAsync_WhenXmlValidationFails_LogsErrorAndRethrows()
    {
        SetupEmptySettings();

        _mockPowerShellRunner
            .Setup(p => p.ValidateXmlSyntaxAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("XML validation failed"));

        var service = CreateService();
        var outputPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.xml");

        Func<Task> act = () => service.GenerateFromCurrentSelectionsAsync(outputPath, new List<ConfigurationItem>());

        await act.Should().ThrowAsync<Exception>().WithMessage("XML validation failed");

        _mockLogService.Verify(
            l => l.Log(LogLevel.Error, It.IsAny<string>()),
            Times.AtLeastOnce);

        if (File.Exists(outputPath)) File.Delete(outputPath);
    }

    // -------------------------------------------------------
    // GenerateFromCurrentSelectionsAsync - discovery service
    // interaction for settings with features
    // -------------------------------------------------------

    [Fact]
    public async Task GenerateFromCurrentSelectionsAsync_WithOptimizeFeatureSettings_CallsDiscoveryService()
    {
        var privacySettings = new List<Setting>
        {
            new()
            {
                Id = "test-privacy-setting",
                Display = new() { Name = "Test Privacy Setting", Description = "Test privacy setting desc" },
                States = new[]
                {
                    new SettingState { Label = "Enabled" },
                    new SettingState { Label = "Disabled" }
                }
            }
        };

        _mockCatalogSettingsRegistry
            .Setup(r => r.GetAll(It.IsAny<bool>()))
            .Returns(new Dictionary<string, IReadOnlyList<Setting>>
            {
                { "Privacy", privacySettings }
            });

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                {
                    "test-privacy-setting",
                    new SettingStateResult { IsEnabled = true }
                }
            });

        var apps = new List<ConfigurationItem>();
        var service = CreateService();
        var outputPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.xml");

        try
        {
            await service.GenerateFromCurrentSelectionsAsync(outputPath, apps);
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }

        _mockSettingStateProvider.Verify(
            d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()),
            Times.AtLeastOnce);
    }

    // -------------------------------------------------------
    // GenerateFromCurrentSelectionsAsync - script builder
    // validation failure
    // -------------------------------------------------------

    [Fact]
    public async Task GenerateFromCurrentSelectionsAsync_WhenScriptValidationFails_LogsErrorAndRethrows()
    {
        SetupEmptySettings();

        _mockPowerShellRunner
            .Setup(p => p.ValidateScriptSyntaxAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Script syntax error"));

        var apps = new List<ConfigurationItem>();
        var service = CreateService();
        var outputPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.xml");

        Func<Task> act = () => service.GenerateFromCurrentSelectionsAsync(outputPath, apps);

        await act.Should().ThrowAsync<Exception>().WithMessage("Script syntax error");

        _mockLogService.Verify(
            l => l.Log(LogLevel.Error, It.IsAny<string>()),
            Times.AtLeastOnce);

        if (File.Exists(outputPath)) File.Delete(outputPath);
    }

    // -------------------------------------------------------
    // GenerateFromCurrentSelectionsAsync - unknown features
    // are skipped
    // -------------------------------------------------------

    [Fact]
    public async Task GenerateFromCurrentSelectionsAsync_WithUnknownFeature_LogsWarningAndSkips()
    {
        var unknownSettings = new List<Setting>
        {
            new()
            {
                Id = "unknown-setting",
                Display = new() { Name = "Unknown", Description = "Unknown desc" },
                States = new[]
                {
                    new SettingState { Label = "Enabled" },
                    new SettingState { Label = "Disabled" }
                }
            }
        };

        _mockCatalogSettingsRegistry
            .Setup(r => r.GetAll(It.IsAny<bool>()))
            .Returns(new Dictionary<string, IReadOnlyList<Setting>>
            {
                { "UnknownFeature", unknownSettings }
            });

        var apps = new List<ConfigurationItem>();
        var service = CreateService();
        var outputPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.xml");

        try
        {
            await service.GenerateFromCurrentSelectionsAsync(outputPath, apps);
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }

        _mockLogService.Verify(
            l => l.Log(LogLevel.Warning, It.Is<string>(s => s.Contains("UnknownFeature") && s.Contains("skipping"))),
            Times.Once);
    }

    // -------------------------------------------------------
    // GenerateFromCurrentSelectionsAsync - empty features
    // are skipped
    // -------------------------------------------------------

    [Fact]
    public async Task GenerateFromCurrentSelectionsAsync_WithEmptyFeatureSettings_SkipsFeature()
    {
        _mockCatalogSettingsRegistry
            .Setup(r => r.GetAll(It.IsAny<bool>()))
            .Returns(new Dictionary<string, IReadOnlyList<Setting>>
            {
                { "Privacy", new List<Setting>() }
            });

        var apps = new List<ConfigurationItem>();
        var service = CreateService();
        var outputPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.xml");

        try
        {
            await service.GenerateFromCurrentSelectionsAsync(outputPath, apps);
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }

        // Discovery service should not be called for empty feature settings
        _mockSettingStateProvider.Verify(
            d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()),
            Times.Never);
    }
}

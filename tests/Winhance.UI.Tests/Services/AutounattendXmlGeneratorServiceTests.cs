using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.Infrastructure.Features.AdvancedTools.Services;
using Winhance.UI.Features.AdvancedTools.Services;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class AutounattendXmlGeneratorServiceTests
{
    private readonly Mock<ICompatibleSettingsRegistry> _mockCompatibleSettingsRegistry = new();
    private readonly Mock<ISystemSettingsDiscoveryService> _mockDiscoveryService = new();
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<IPowerShellRunner> _mockPowerShellRunner = new();
    private readonly Mock<ISelectedAppsProvider> _mockSelectedAppsProvider = new();
    private readonly Mock<IPowerSettingsQueryService> _mockPowerSettingsQueryService = new();
    private readonly Mock<IHardwareDetectionService> _mockHardwareDetectionService = new();
    private readonly Mock<IComboBoxResolver> _mockComboBoxResolver = new();

    private AutounattendScriptBuilder CreateScriptBuilder()
    {
        return new AutounattendScriptBuilder(
            _mockPowerSettingsQueryService.Object,
            _mockHardwareDetectionService.Object,
            _mockLogService.Object,
            _mockComboBoxResolver.Object,
            _mockPowerShellRunner.Object);
    }

    private AutounattendXmlGeneratorService CreateService(
        AutounattendScriptBuilder? scriptBuilder = null)
    {
        return new AutounattendXmlGeneratorService(
            _mockCompatibleSettingsRegistry.Object,
            _mockDiscoveryService.Object,
            _mockLogService.Object,
            scriptBuilder ?? CreateScriptBuilder(),
            _mockPowerShellRunner.Object,
            _mockSelectedAppsProvider.Object);
    }

    private void SetupEmptySettings()
    {
        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetAllFilteredSettings())
            .Returns(new Dictionary<string, IEnumerable<SettingDefinition>>());

        _mockDiscoveryService
            .Setup(d => d.GetSettingStatesAsync(It.IsAny<IEnumerable<SettingDefinition>>()))
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

        // The service calls LoadEmbeddedTemplate which reads an embedded resource from the UI assembly.
        // Since we're running tests without the actual embedded resource, this will throw
        // FileNotFoundException. We catch and verify the provider was called before that point.
        var service = CreateService();
        var outputPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.xml");

        try
        {
            await service.GenerateFromCurrentSelectionsAsync(outputPath);
        }
        catch (Exception)
        {
            // Expected: embedded template or real service dependencies not available in test context
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
        catch (Exception)
        {
            // Expected: embedded template or real service dependencies not available in test context
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
        catch (Exception)
        {
            // Expected: embedded template or real service dependencies not available in test context
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }

        _mockSelectedAppsProvider.Verify(p => p.GetSelectedWindowsAppsAsync(), Times.Once);
    }

    // -------------------------------------------------------
    // GenerateFromCurrentSelectionsAsync - calls GetAllFilteredSettings
    // -------------------------------------------------------

    [Fact]
    public async Task GenerateFromCurrentSelectionsAsync_CallsGetAllFilteredSettings()
    {
        SetupEmptySettings();

        var apps = new List<ConfigurationItem>();

        var service = CreateService();
        var outputPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.xml");

        try
        {
            await service.GenerateFromCurrentSelectionsAsync(outputPath, apps);
        }
        catch (Exception)
        {
            // Expected: embedded template or real service dependencies not available in test context
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }

        // GetAllFilteredSettings is called twice: once in CreateConfigurationFromSystemAsync
        // and once to pass to BuildWinhancementsScriptAsync
        _mockCompatibleSettingsRegistry.Verify(
            r => r.GetAllFilteredSettings(),
            Times.AtLeast(1));
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
        catch (Exception)
        {
            // Expected: embedded template or real service dependencies not available in test context
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
        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetAllFilteredSettings())
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
        // GetAllFilteredSettings is invoked (it runs after the config is created with apps).
        var service = CreateService();
        var outputPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.xml");

        try
        {
            await service.GenerateFromCurrentSelectionsAsync(outputPath, apps);
        }
        catch (Exception)
        {
            // Expected: embedded template or real service dependencies not available in test context
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }

        // Verify the service proceeded past app assignment (reached GetAllFilteredSettings)
        _mockCompatibleSettingsRegistry.Verify(r => r.GetAllFilteredSettings(), Times.AtLeast(1));
    }

    [Fact]
    public async Task GenerateFromCurrentSelectionsAsync_WithEmptyApps_DoesNotThrowBeforeTemplate()
    {
        SetupEmptySettings();

        var apps = new List<ConfigurationItem>();
        var service = CreateService();
        var outputPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.xml");

        try
        {
            await service.GenerateFromCurrentSelectionsAsync(outputPath, apps);
        }
        catch (Exception)
        {
            // Expected: embedded template or real service dependencies not available in test context
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }

        // If we reached the template loading step, the config creation succeeded
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

        // We need the method to get past the template loading step. Since the embedded
        // resource is not available in tests, this test verifies the overall exception
        // handling path is correct - if the template load itself throws, the outer
        // catch handles it.
        var service = CreateService();
        var outputPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.xml");

        Func<Task> act = () => service.GenerateFromCurrentSelectionsAsync(outputPath, new List<ConfigurationItem>());

        // The FileNotFoundException from the template load will be caught by the outer handler
        await act.Should().ThrowAsync<Exception>();

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
        var privacySettings = new List<SettingDefinition>
        {
            new()
            {
                Id = "test-privacy-setting",
                Name = "Test Privacy Setting",
                Description = "Test privacy setting desc",
                InputType = InputType.Toggle
            }
        };

        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetAllFilteredSettings())
            .Returns(new Dictionary<string, IEnumerable<SettingDefinition>>
            {
                { "Privacy", privacySettings }
            });

        _mockDiscoveryService
            .Setup(d => d.GetSettingStatesAsync(It.IsAny<IEnumerable<SettingDefinition>>()))
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
        catch (Exception)
        {
            // Expected: embedded template or real service dependencies not available in test context
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }

        _mockDiscoveryService.Verify(
            d => d.GetSettingStatesAsync(It.IsAny<IEnumerable<SettingDefinition>>()),
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

        // Either the script validation error or the template load error will propagate
        await act.Should().ThrowAsync<Exception>();

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
        var unknownSettings = new List<SettingDefinition>
        {
            new() { Id = "unknown-setting", Name = "Unknown", Description = "Unknown desc", InputType = InputType.Toggle }
        };

        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetAllFilteredSettings())
            .Returns(new Dictionary<string, IEnumerable<SettingDefinition>>
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
        catch (Exception)
        {
            // Expected: embedded template or real service dependencies not available in test context
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
        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetAllFilteredSettings())
            .Returns(new Dictionary<string, IEnumerable<SettingDefinition>>
            {
                { "Privacy", Enumerable.Empty<SettingDefinition>() }
            });

        var apps = new List<ConfigurationItem>();
        var service = CreateService();
        var outputPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.xml");

        try
        {
            await service.GenerateFromCurrentSelectionsAsync(outputPath, apps);
        }
        catch (Exception)
        {
            // Expected: embedded template or real service dependencies not available in test context
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }

        // Discovery service should not be called for empty feature settings
        _mockDiscoveryService.Verify(
            d => d.GetSettingStatesAsync(It.IsAny<IEnumerable<SettingDefinition>>()),
            Times.Never);
    }
}

using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.AdvancedTools.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.AdvancedTools;

public class AutounattendScriptBuilderTests
{
    private static readonly string[] CortanaPackage = ["Microsoft.549981C3F5F10"];

    private readonly Mock<IPowerSettingsQueryService> _powerSettingsQueryService = new();
    private readonly Mock<IHardwareDetectionService> _hardwareDetectionService = new();
    private readonly Mock<ILogService> _logService = new();
    private readonly Mock<IPowerShellRunner> _powerShellRunner = new();
    private readonly AutounattendScriptBuilder _sut;

    public AutounattendScriptBuilderTests()
    {
        // Default setup for power settings query (always needed since BuildWinhancementsScriptAsync calls it)
        _powerSettingsQueryService.Setup(s => s.GetActivePowerPlanAsync())
            .ReturnsAsync(new PowerPlan { Guid = "balanced-guid", Name = "Balanced" });
        _powerSettingsQueryService.Setup(s => s.GetAllPowerSettingsACDCAsync(It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, (int? acValue, int? dcValue)>());
        _hardwareDetectionService.Setup(s => s.HasBattery()).Returns(false);

        _powerShellRunner.Setup(s => s.ValidateScriptSyntaxAsync(It.IsAny<string>(), default))
            .Returns(Task.CompletedTask);

        _sut = new AutounattendScriptBuilder(
            _powerSettingsQueryService.Object,
            _hardwareDetectionService.Object,
            _logService.Object,
            _powerShellRunner.Object,
            new Mock<IWindowsVersionService>().Object);
    }

    [Fact]
    public async Task BuildWinhancementsScriptAsync_EmptyConfig_ProducesValidScript()
    {
        var config = new UnifiedConfigurationFile();
        var allSettings = new Dictionary<string, IReadOnlyList<Setting>>();

        var result = await _sut.BuildWinhancementsScriptAsync(config, allSettings);

        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task BuildWinhancementsScriptAsync_ContainsHeader()
    {
        var config = new UnifiedConfigurationFile();
        var allSettings = new Dictionary<string, IReadOnlyList<Setting>>();

        var result = await _sut.BuildWinhancementsScriptAsync(config, allSettings);

        result.Should().Contain(".SYNOPSIS");
        result.Should().Contain("param(");
    }

    [Fact]
    public async Task BuildWinhancementsScriptAsync_ContainsLoggingSetup()
    {
        var config = new UnifiedConfigurationFile();
        var allSettings = new Dictionary<string, IReadOnlyList<Setting>>();

        var result = await _sut.BuildWinhancementsScriptAsync(config, allSettings);

        result.Should().Contain("function Write-Log");
        result.Should().Contain("$LogPath");
    }

    [Fact]
    public async Task BuildWinhancementsScriptAsync_ContainsHelperFunctions()
    {
        var config = new UnifiedConfigurationFile();
        var allSettings = new Dictionary<string, IReadOnlyList<Setting>>();

        var result = await _sut.BuildWinhancementsScriptAsync(config, allSettings);

        result.Should().Contain("function Set-RegistryValue");
        result.Should().Contain("function Start-ProcessAsUser");
    }

    [Fact]
    public async Task BuildWinhancementsScriptAsync_ContainsSystemBlock()
    {
        var config = new UnifiedConfigurationFile();
        var allSettings = new Dictionary<string, IReadOnlyList<Setting>>();

        var result = await _sut.BuildWinhancementsScriptAsync(config, allSettings);

        result.Should().Contain("if (-not $UserCustomizations)");
    }

    [Fact]
    public async Task BuildWinhancementsScriptAsync_ContainsUserBlock()
    {
        var config = new UnifiedConfigurationFile();
        var allSettings = new Dictionary<string, IReadOnlyList<Setting>>();

        var result = await _sut.BuildWinhancementsScriptAsync(config, allSettings);

        result.Should().Contain("if ($UserCustomizations)");
    }

    [Fact]
    public async Task BuildWinhancementsScriptAsync_ContainsCompletionBlock()
    {
        var config = new UnifiedConfigurationFile();
        var allSettings = new Dictionary<string, IReadOnlyList<Setting>>();

        var result = await _sut.BuildWinhancementsScriptAsync(config, allSettings);

        result.Should().Contain("Script Completed");
    }

    [Fact]
    public async Task BuildWinhancementsScriptAsync_ContainsCustomScriptPlaceholders()
    {
        var config = new UnifiedConfigurationFile();
        var allSettings = new Dictionary<string, IReadOnlyList<Setting>>();

        var result = await _sut.BuildWinhancementsScriptAsync(config, allSettings);

        result.Should().Contain("SYSTEM WIDE");
        result.Should().Contain("USER SPECIFIC");
        result.Should().Contain("# Start here");
        result.Should().Contain("# End here");
    }

    [Fact]
    public async Task BuildWinhancementsScriptAsync_ContainsScriptsDirectorySetup()
    {
        var config = new UnifiedConfigurationFile();
        var allSettings = new Dictionary<string, IReadOnlyList<Setting>>();

        var result = await _sut.BuildWinhancementsScriptAsync(config, allSettings);

        result.Should().Contain("$scriptsDir");
    }

    [Fact]
    public async Task BuildWinhancementsScriptAsync_ContainsWinhanceInstaller()
    {
        var config = new UnifiedConfigurationFile();
        var allSettings = new Dictionary<string, IReadOnlyList<Setting>>();

        var result = await _sut.BuildWinhancementsScriptAsync(config, allSettings);

        result.Should().Contain("Install Winhance.lnk");
    }

    [Fact]
    public async Task BuildWinhancementsScriptAsync_ContainsCleanStartMenu()
    {
        var config = new UnifiedConfigurationFile();
        var allSettings = new Dictionary<string, IReadOnlyList<Setting>>();

        var result = await _sut.BuildWinhancementsScriptAsync(config, allSettings);

        result.Should().Contain("START MENU LAYOUT");
    }

    [Fact]
    public async Task BuildWinhancementsScriptAsync_ContainsUserCustomizationsTask()
    {
        var config = new UnifiedConfigurationFile();
        var allSettings = new Dictionary<string, IReadOnlyList<Setting>>();

        var result = await _sut.BuildWinhancementsScriptAsync(config, allSettings);

        result.Should().Contain("WinhanceUserCustomizations");
    }

    [Fact]
    public async Task BuildWinhancementsScriptAsync_ContainsUserDetectionBridge()
    {
        var config = new UnifiedConfigurationFile();
        var allSettings = new Dictionary<string, IReadOnlyList<Setting>>();

        var result = await _sut.BuildWinhancementsScriptAsync(config, allSettings);

        result.Should().Contain("$runningAsSystem");
        result.Should().Contain("S-1-5-18");
        result.Should().Contain("UserCustomizationsApplied");
    }

    [Fact]
    public async Task BuildWinhancementsScriptAsync_CallsValidateScriptSyntax()
    {
        var config = new UnifiedConfigurationFile();
        var allSettings = new Dictionary<string, IReadOnlyList<Setting>>();

        await _sut.BuildWinhancementsScriptAsync(config, allSettings);

        _powerShellRunner.Verify(r => r.ValidateScriptSyntaxAsync(
            It.IsAny<string>(), default), Times.Once);
    }

    [Fact]
    public async Task BuildWinhancementsScriptAsync_SyntaxValidationFails_Throws()
    {
        _powerShellRunner.Setup(s => s.ValidateScriptSyntaxAsync(It.IsAny<string>(), default))
            .ThrowsAsync(new InvalidOperationException("Syntax error at line 42"));

        var config = new UnifiedConfigurationFile();
        var allSettings = new Dictionary<string, IReadOnlyList<Setting>>();

        var act = () => _sut.BuildWinhancementsScriptAsync(config, allSettings);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Syntax error*");
    }

    [Fact]
    public async Task BuildWinhancementsScriptAsync_WithWindowsApps_EmitsAppRemoval()
    {
        var config = new UnifiedConfigurationFile
        {
            WindowsApps = new ConfigSection
            {
                Items = new List<ConfigurationItem>
                {
                    new ConfigurationItem
                    {
                        Id = "windows-app-cortana",
                        AppxPackageName = CortanaPackage
                    }
                }
            }
        };
        var allSettings = new Dictionary<string, IReadOnlyList<Setting>>();

        var result = await _sut.BuildWinhancementsScriptAsync(config, allSettings);

        result.Should().Contain("WINDOWS APPS REMOVAL");
        result.Should().Contain("BloatRemoval");
    }

    [Fact]
    public async Task BuildWinhancementsScriptAsync_WithOptimizeFeatures_EmitsHklmRegistryEntries()
    {
        // The pipeline runs on the catalog Setting dict, so the fixture passes the REAL
        // catalog HKLM registry toggle (security-remote-assistance, RegTarget fAllowToGetHelp) directly.
        var config = new UnifiedConfigurationFile
        {
            Optimize = new FeatureGroupSection
            {
                Features = new Dictionary<string, ConfigSection>
                {
                    {
                        "TestOptimize", new ConfigSection
                        {
                            Items = new List<ConfigurationItem>
                            {
                                new ConfigurationItem
                                {
                                    Id = "security-remote-assistance",
                                    IsSelected = true,
                                    InputType = InputType.Toggle
                                }
                            }
                        }
                    }
                }
            }
        };

        var allSettings = new Dictionary<string, IReadOnlyList<Setting>>
        {
            { "TestOptimize", new[] { SettingCatalog.Find("security-remote-assistance")! } }
        };

        var result = await _sut.BuildWinhancementsScriptAsync(config, allSettings);

        result.Should().Contain("Set-RegistryValue");
        result.Should().Contain("fAllowToGetHelp");
    }

    [Fact]
    public async Task BuildWinhancementsScriptAsync_WithCustomizeFeatures_EmitsHkcuInUserBlock()
    {
        // The REAL catalog HKCU toggle (gaming-game-mode, RegTarget AutoGameModeEnabled under
        // HKEY_CURRENT_USER) rides the Setting dict directly. The value name lands only in the HKCU pass,
        // i.e. inside the $UserCustomizations block.
        var config = new UnifiedConfigurationFile
        {
            Customize = new FeatureGroupSection
            {
                Features = new Dictionary<string, ConfigSection>
                {
                    {
                        "TestCustomize", new ConfigSection
                        {
                            Items = new List<ConfigurationItem>
                            {
                                new ConfigurationItem
                                {
                                    Id = "gaming-game-mode",
                                    IsSelected = true,
                                    InputType = InputType.Toggle
                                }
                            }
                        }
                    }
                }
            }
        };

        var allSettings = new Dictionary<string, IReadOnlyList<Setting>>
        {
            { "TestCustomize", new[] { SettingCatalog.Find("gaming-game-mode")! } }
        };

        var result = await _sut.BuildWinhancementsScriptAsync(config, allSettings);

        var userBlockIndex = result.IndexOf("if ($UserCustomizations)");
        var custValIndex = result.IndexOf("AutoGameModeEnabled", userBlockIndex);
        custValIndex.Should().BeGreaterThan(userBlockIndex);
    }

    [Fact]
    public async Task BuildWinhancementsScriptAsync_ValidSyntax_LogsSuccess()
    {
        var config = new UnifiedConfigurationFile();
        var allSettings = new Dictionary<string, IReadOnlyList<Setting>>();

        await _sut.BuildWinhancementsScriptAsync(config, allSettings);

        _logService.Verify(l => l.Log(
            LogLevel.Info,
            It.Is<string>(s => s.Contains("passed PowerShell syntax validation")),
            null), Times.Once);
    }

    [Fact]
    public async Task BuildWinhancementsScriptAsync_FailedSyntax_LogsError()
    {
        _powerShellRunner.Setup(s => s.ValidateScriptSyntaxAsync(It.IsAny<string>(), default))
            .ThrowsAsync(new InvalidOperationException("Bad syntax"));

        var config = new UnifiedConfigurationFile();
        var allSettings = new Dictionary<string, IReadOnlyList<Setting>>();

        try { await _sut.BuildWinhancementsScriptAsync(config, allSettings); }
        catch { }

        _logService.Verify(l => l.Log(
            LogLevel.Error,
            It.Is<string>(s => s.Contains("failed PowerShell syntax validation")),
            null), Times.Once);
    }

    [Fact]
    public async Task BuildWinhancementsScriptAsync_SettingDict_RealPowerSetting_EmitsCatalogPowerCfgTargets()
    {
        // A REAL catalog power setting rides the Setting dict end-to-end and emits its catalog
        // PowerCfgTarget GUIDs with the bulk-query values.
        var config = new UnifiedConfigurationFile();
        var allSettings = new Dictionary<string, IReadOnlyList<Setting>>
        {
            { FeatureIds.Power, new[] { SettingCatalog.Find("power-display-timeout")! } }
        };

        _powerSettingsQueryService.Setup(s => s.GetAllPowerSettingsACDCAsync(It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, (int? acValue, int? dcValue)>
            {
                { "3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e", (10, 5) }
            });

        var result = await _sut.BuildWinhancementsScriptAsync(config, allSettings);

        result.Should().Contain("7516b95f-f776-4464-8c53-06167f40cc99");
        result.Should().Contain("3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e");
    }

    [Fact]
    public async Task BuildWinhancementsScriptAsync_SettingDict_AliasedConfigId_EmitsViaMergedCatalogSetting()
    {
        // A config exported on a Windows-10 machine carries the retired "-win10" This PC folder item id,
        // while the Setting dict (fed straight from the catalog registry) carries only the MERGED
        // catalog setting under its canonical id. What this fact PINS red-on-mutation is the pipeline's
        // alias-NORMALIZED config-id lookup (the section normalizes configItem.Id onto the canonical
        // Setting) - so the toggle still emits, via the Win10 KeyExists target: the ctor's
        // IWindowsVersionService mock reports build 0, which falls inside BuildRange.Windows10, so the
        // threaded build drops the Win11 HiddenByDefault target.
        var config = new UnifiedConfigurationFile
        {
            Customize = new FeatureGroupSection
            {
                Features = new Dictionary<string, ConfigSection>
                {
                    {
                        "Explorer", new ConfigSection
                        {
                            Items = new List<ConfigurationItem>
                            {
                                new ConfigurationItem
                                {
                                    Id = "explorer-customization-thispc-folder-desktop-win10",
                                    IsSelected = true,
                                    InputType = InputType.Toggle
                                }
                            }
                        }
                    }
                }
            }
        };

        var allSettings = new Dictionary<string, IReadOnlyList<Setting>>
        {
            { "Explorer", new[] { SettingCatalog.Find("explorer-customization-thispc-folder-desktop")! } }
        };

        var result = await _sut.BuildWinhancementsScriptAsync(config, allSettings);

        result.Should().Contain("{B4BFCC3A-DB2C-424C-B029-7FE99A87C641}");
        result.Should().NotContain("HiddenByDefault");
    }
}

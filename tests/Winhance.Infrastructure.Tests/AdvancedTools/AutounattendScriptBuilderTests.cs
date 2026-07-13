using System.Text;
using FluentAssertions;
using Microsoft.Win32;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.AdvancedTools.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.AdvancedTools;

public class AutounattendScriptBuilderTests
{
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
        _hardwareDetectionService.Setup(s => s.HasBatteryAsync()).ReturnsAsync(false);

        // Syntax validation succeeds by default
        _powerShellRunner.Setup(s => s.ValidateScriptSyntaxAsync(It.IsAny<string>(), default))
            .Returns(Task.CompletedTask);

        _sut = new AutounattendScriptBuilder(
            _powerSettingsQueryService.Object,
            _hardwareDetectionService.Object,
            _logService.Object,
            _powerShellRunner.Object,
            new Mock<IWindowsVersionService>().Object);
    }

    // ---------------------------------------------------------------
    // BuildWinhancementsScriptAsync - Empty config
    // ---------------------------------------------------------------

    [Fact]
    public async Task BuildWinhancementsScriptAsync_EmptyConfig_ProducesValidScript()
    {
        var config = new UnifiedConfigurationFile();
        var allSettings = new Dictionary<string, IReadOnlyList<Setting>>();

        var result = await _sut.BuildWinhancementsScriptAsync(config, allSettings);

        result.Should().NotBeNullOrEmpty();
    }

    // ---------------------------------------------------------------
    // BuildWinhancementsScriptAsync - Contains header
    // ---------------------------------------------------------------

    [Fact]
    public async Task BuildWinhancementsScriptAsync_ContainsHeader()
    {
        var config = new UnifiedConfigurationFile();
        var allSettings = new Dictionary<string, IReadOnlyList<Setting>>();

        var result = await _sut.BuildWinhancementsScriptAsync(config, allSettings);

        result.Should().Contain(".SYNOPSIS");
        result.Should().Contain("param(");
    }

    // ---------------------------------------------------------------
    // BuildWinhancementsScriptAsync - Contains logging setup
    // ---------------------------------------------------------------

    [Fact]
    public async Task BuildWinhancementsScriptAsync_ContainsLoggingSetup()
    {
        var config = new UnifiedConfigurationFile();
        var allSettings = new Dictionary<string, IReadOnlyList<Setting>>();

        var result = await _sut.BuildWinhancementsScriptAsync(config, allSettings);

        result.Should().Contain("function Write-Log");
        result.Should().Contain("$LogPath");
    }

    // ---------------------------------------------------------------
    // BuildWinhancementsScriptAsync - Contains helper functions
    // ---------------------------------------------------------------

    [Fact]
    public async Task BuildWinhancementsScriptAsync_ContainsHelperFunctions()
    {
        var config = new UnifiedConfigurationFile();
        var allSettings = new Dictionary<string, IReadOnlyList<Setting>>();

        var result = await _sut.BuildWinhancementsScriptAsync(config, allSettings);

        result.Should().Contain("function Set-RegistryValue");
        result.Should().Contain("function Start-ProcessAsUser");
    }

    // ---------------------------------------------------------------
    // BuildWinhancementsScriptAsync - Contains if (-not $UserCustomizations) block
    // ---------------------------------------------------------------

    [Fact]
    public async Task BuildWinhancementsScriptAsync_ContainsSystemBlock()
    {
        var config = new UnifiedConfigurationFile();
        var allSettings = new Dictionary<string, IReadOnlyList<Setting>>();

        var result = await _sut.BuildWinhancementsScriptAsync(config, allSettings);

        result.Should().Contain("if (-not $UserCustomizations)");
    }

    // ---------------------------------------------------------------
    // BuildWinhancementsScriptAsync - Contains if ($UserCustomizations) block
    // ---------------------------------------------------------------

    [Fact]
    public async Task BuildWinhancementsScriptAsync_ContainsUserBlock()
    {
        var config = new UnifiedConfigurationFile();
        var allSettings = new Dictionary<string, IReadOnlyList<Setting>>();

        var result = await _sut.BuildWinhancementsScriptAsync(config, allSettings);

        result.Should().Contain("if ($UserCustomizations)");
    }

    // ---------------------------------------------------------------
    // BuildWinhancementsScriptAsync - Contains completion block
    // ---------------------------------------------------------------

    [Fact]
    public async Task BuildWinhancementsScriptAsync_ContainsCompletionBlock()
    {
        var config = new UnifiedConfigurationFile();
        var allSettings = new Dictionary<string, IReadOnlyList<Setting>>();

        var result = await _sut.BuildWinhancementsScriptAsync(config, allSettings);

        result.Should().Contain("Script Completed");
    }

    // ---------------------------------------------------------------
    // BuildWinhancementsScriptAsync - Contains custom script placeholders
    // ---------------------------------------------------------------

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

    // ---------------------------------------------------------------
    // BuildWinhancementsScriptAsync - Contains scripts directory setup
    // ---------------------------------------------------------------

    [Fact]
    public async Task BuildWinhancementsScriptAsync_ContainsScriptsDirectorySetup()
    {
        var config = new UnifiedConfigurationFile();
        var allSettings = new Dictionary<string, IReadOnlyList<Setting>>();

        var result = await _sut.BuildWinhancementsScriptAsync(config, allSettings);

        result.Should().Contain("$scriptsDir");
    }

    // ---------------------------------------------------------------
    // BuildWinhancementsScriptAsync - Contains Winhance installer
    // ---------------------------------------------------------------

    [Fact]
    public async Task BuildWinhancementsScriptAsync_ContainsWinhanceInstaller()
    {
        var config = new UnifiedConfigurationFile();
        var allSettings = new Dictionary<string, IReadOnlyList<Setting>>();

        var result = await _sut.BuildWinhancementsScriptAsync(config, allSettings);

        result.Should().Contain("Install Winhance.lnk");
    }

    // ---------------------------------------------------------------
    // BuildWinhancementsScriptAsync - Contains Clean Start Menu
    // ---------------------------------------------------------------

    [Fact]
    public async Task BuildWinhancementsScriptAsync_ContainsCleanStartMenu()
    {
        var config = new UnifiedConfigurationFile();
        var allSettings = new Dictionary<string, IReadOnlyList<Setting>>();

        var result = await _sut.BuildWinhancementsScriptAsync(config, allSettings);

        result.Should().Contain("START MENU LAYOUT");
    }

    // ---------------------------------------------------------------
    // BuildWinhancementsScriptAsync - Contains UserCustomizations scheduled task
    // ---------------------------------------------------------------

    [Fact]
    public async Task BuildWinhancementsScriptAsync_ContainsUserCustomizationsTask()
    {
        var config = new UnifiedConfigurationFile();
        var allSettings = new Dictionary<string, IReadOnlyList<Setting>>();

        var result = await _sut.BuildWinhancementsScriptAsync(config, allSettings);

        result.Should().Contain("WinhanceUserCustomizations");
    }

    // ---------------------------------------------------------------
    // BuildWinhancementsScriptAsync - Contains user detection bridge
    // ---------------------------------------------------------------

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

    // ---------------------------------------------------------------
    // BuildWinhancementsScriptAsync - Validates script syntax
    // ---------------------------------------------------------------

    [Fact]
    public async Task BuildWinhancementsScriptAsync_CallsValidateScriptSyntax()
    {
        var config = new UnifiedConfigurationFile();
        var allSettings = new Dictionary<string, IReadOnlyList<Setting>>();

        await _sut.BuildWinhancementsScriptAsync(config, allSettings);

        _powerShellRunner.Verify(r => r.ValidateScriptSyntaxAsync(
            It.IsAny<string>(), default), Times.Once);
    }

    // ---------------------------------------------------------------
    // BuildWinhancementsScriptAsync - Syntax validation failure throws
    // ---------------------------------------------------------------

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

    // ---------------------------------------------------------------
    // BuildWinhancementsScriptAsync - With WindowsApps items
    // ---------------------------------------------------------------

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
                        AppxPackageName = new[] { "Microsoft.549981C3F5F10" }
                    }
                }
            }
        };
        var allSettings = new Dictionary<string, IReadOnlyList<Setting>>();

        var result = await _sut.BuildWinhancementsScriptAsync(config, allSettings);

        result.Should().Contain("WINDOWS APPS REMOVAL");
        result.Should().Contain("BloatRemoval");
    }

    // ---------------------------------------------------------------
    // BuildWinhancementsScriptAsync - With Optimize features (HKLM)
    // ---------------------------------------------------------------

    [Fact]
    public async Task BuildWinhancementsScriptAsync_WithOptimizeFeatures_EmitsHklmRegistryEntries()
    {
        // Slice 7e-6: the pipeline runs on the catalog Setting dict, so the fixture passes the REAL
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

    // ---------------------------------------------------------------
    // BuildWinhancementsScriptAsync - With Customize features (HKCU)
    // ---------------------------------------------------------------

    [Fact]
    public async Task BuildWinhancementsScriptAsync_WithCustomizeFeatures_EmitsHkcuInUserBlock()
    {
        // Slice 7e-6: the REAL catalog HKCU toggle (gaming-game-mode, RegTarget AutoGameModeEnabled under
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

        // The HKCU entries should appear after "if ($UserCustomizations)"
        var userBlockIndex = result.IndexOf("if ($UserCustomizations)");
        var custValIndex = result.IndexOf("AutoGameModeEnabled", userBlockIndex);
        custValIndex.Should().BeGreaterThan(userBlockIndex);
    }

    // ---------------------------------------------------------------
    // BuildWinhancementsScriptAsync - Logs success on valid syntax
    // ---------------------------------------------------------------

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

    // ---------------------------------------------------------------
    // BuildWinhancementsScriptAsync - Logs error on failed syntax
    // ---------------------------------------------------------------

    [Fact]
    public async Task BuildWinhancementsScriptAsync_FailedSyntax_LogsError()
    {
        _powerShellRunner.Setup(s => s.ValidateScriptSyntaxAsync(It.IsAny<string>(), default))
            .ThrowsAsync(new InvalidOperationException("Bad syntax"));

        var config = new UnifiedConfigurationFile();
        var allSettings = new Dictionary<string, IReadOnlyList<Setting>>();

        try { await _sut.BuildWinhancementsScriptAsync(config, allSettings); }
        catch { /* expected */ }

        _logService.Verify(l => l.Log(
            LogLevel.Error,
            It.Is<string>(s => s.Contains("failed PowerShell syntax validation")),
            null), Times.Once);
    }
    // ---------------------------------------------------------------
    // BuildWinhancementsScriptAsync (def-dict overload) - the 7f-transitional pairing SHIM
    // ---------------------------------------------------------------

    [Fact]
    public async Task BuildWinhancementsScriptAsync_DefDictShim_PairsRealIds_AndSkipsUnpairedSilently()
    {
        // The PUBLIC def-dict overload is the 7f-transitional seam: it pairs each def via alias-normalized
        // SettingCatalog.Find and forwards the catalog Settings to the Setting-dict overload. A REAL id rides
        // the pipeline (power-display-timeout emits its catalog PowerCfgTarget GUIDs, values from the query
        // mock); an UNPAIRED id is skipped silently even though its def still carries the old PowerCfgSettings
        // payload and the bulk query has values for its GUID. (This unpaired-skip pin moved here from
        // PowerSettingsScriptSectionTests when the section stopped seeing defs at 7e-6.)
        var config = new UnifiedConfigurationFile();
        var allSettings = new Dictionary<string, IEnumerable<SettingDefinition>>
        {
            {
                FeatureIds.Power, new[]
                {
                    new SettingDefinition
                    {
                        Id = "power-display-timeout",
                        Name = "Turn off the display",
                        Description = "inert - the paired emit reads the catalog Setting, not this def"
                    },
                    new SettingDefinition
                    {
                        Id = "unpaired-power-setting",
                        Name = "Unpaired",
                        Description = "Retired-fallback payload that must no longer be read",
                        PowerCfgSettings = new[]
                        {
                            new PowerCfgSetting
                            {
                                SubgroupGuid = "fake-subgroup-guid",
                                SettingGuid = "fake-setting-guid",
                                RecommendedValueAC = null,
                                RecommendedValueDC = null,
                                DefaultValueAC = null,
                                DefaultValueDC = null
                            }
                        }
                    }
                }
            }
        };

        _powerSettingsQueryService.Setup(s => s.GetAllPowerSettingsACDCAsync(It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, (int? acValue, int? dcValue)>
            {
                { "3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e", (10, 5) },
                { "fake-setting-guid", (50, 30) }
            });

        var result = await _sut.BuildWinhancementsScriptAsync(config, allSettings);

        result.Should().Contain("7516b95f-f776-4464-8c53-06167f40cc99");
        result.Should().Contain("3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e");
        result.Should().NotContain("fake-subgroup-guid");
        result.Should().NotContain("fake-setting-guid");
    }

    [Fact]
    public async Task BuildWinhancementsScriptAsync_DefDictShim_AliasedDefAndConfigId_EmitViaMergedCatalogSetting()
    {
        // A Windows-10 registry (OS-filtered defs) carries the retired "-win10" This PC folder variant, and a
        // config exported on that machine carries the same "-win10" item id. The shim's alias-normalized Find
        // pairs the def onto the MERGED catalog setting (dedupe-by-Setting.Id runs when a group carries both
        // variants, as fed here, but is UNPINNABLE by output - emission is per config item - and
        // production-inert: no aliased setting is power/native-power, the only dict-values consumers; what
        // this fact PINS red-on-mutation is the pipeline's alias-NORMALIZED config-id lookup) - so the
        // toggle still emits, via the Win10 KeyExists target: the ctor's IWindowsVersionService mock reports
        // build 0, which falls inside BuildRange.Windows10, so the threaded build drops the Win11
        // HiddenByDefault target.
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

        var allSettings = new Dictionary<string, IEnumerable<SettingDefinition>>
        {
            {
                "Explorer", new[]
                {
                    new SettingDefinition
                    {
                        Id = "explorer-customization-thispc-folder-desktop-win10",
                        Name = "Show Desktop in This PC (Win10 variant)",
                        Description = "inert - id-carrier only"
                    },
                    new SettingDefinition
                    {
                        Id = "explorer-customization-thispc-folder-desktop",
                        Name = "Show Desktop in This PC",
                        Description = "inert - id-carrier only; Find-collapses onto the same merged Setting (dedupe)"
                    }
                }
            }
        };

        var result = await _sut.BuildWinhancementsScriptAsync(config, allSettings);

        result.Should().Contain("{B4BFCC3A-DB2C-424C-B029-7FE99A87C641}");
        result.Should().NotContain("HiddenByDefault");
    }
}

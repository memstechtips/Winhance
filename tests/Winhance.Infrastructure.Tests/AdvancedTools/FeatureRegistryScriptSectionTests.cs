using System.Text;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.AdvancedTools.Helpers;
using Winhance.Infrastructure.Features.AdvancedTools.ScriptSections;
using Xunit;

namespace Winhance.Infrastructure.Tests.AdvancedTools;

/// <summary>Slice 7e-6: AppendFeatureGroupRegistryEntries takes the catalog Setting dict (the def dict died with
/// the flip; the builder's PUBLIC def-dict shim - pinned by AutounattendScriptBuilderTests - is the only place
/// defs still enter the pipeline), so every fixture here passes REAL catalog Settings via SettingCatalog.Find.</summary>
public class FeatureRegistryScriptSectionTests
{
    private readonly Mock<ILogService> _logService = new();
    private readonly RegistryCommandEmitter _registryEmitter;
    private readonly FeatureRegistryScriptSection _sut;

    public FeatureRegistryScriptSectionTests()
    {
        _registryEmitter = new RegistryCommandEmitter(_logService.Object);
        _sut = new FeatureRegistryScriptSection(_registryEmitter, _logService.Object);
    }

    // ---------------------------------------------------------------
    // GetFeatureDisplayName
    // ---------------------------------------------------------------

    [Fact]
    public void GetFeatureDisplayName_KnownFeature_ReturnsDisplayNameWithSettings()
    {
        var result = _sut.GetFeatureDisplayName(FeatureIds.Privacy);

        result.Should().Contain("Privacy");
        result.Should().EndWith("Settings");
    }

    [Fact]
    public void GetFeatureDisplayName_UnknownFeature_FallsBackToFeatureId()
    {
        var result = _sut.GetFeatureDisplayName("NonExistentFeature");

        result.Should().Be("NonExistentFeature Settings");
    }

    // ---------------------------------------------------------------
    // AppendFeatureGroupRegistryEntries - Empty feature group
    // ---------------------------------------------------------------

    [Fact]
    public void AppendFeatureGroupRegistryEntries_NoMatchingSettings_LogsWarning()
    {
        var sb = new StringBuilder();
        var featureGroup = CreateFeatureGroup(FeatureIds.Privacy, new[]
        {
            new ConfigurationItem
            {
                Id = "unknown-setting",
                IsSelected = true,
                InputType = InputType.Toggle
            }
        });

        var allSettings = new Dictionary<string, IReadOnlyList<Setting>>();

        _sut.AppendFeatureGroupRegistryEntries(sb, featureGroup, allSettings, "Optimize", isHkcu: false, indent: "    ");

        _logService.Verify(l => l.Log(
            LogLevel.Warning,
            It.Is<string>(s => s.Contains(FeatureIds.Privacy)),
            null), Times.Once);
    }

    // ---------------------------------------------------------------
    // AppendFeatureGroupRegistryEntries - HKLM toggle entries
    // ---------------------------------------------------------------

    [Fact]
    public void AppendFeatureGroupRegistryEntries_HklmToggle_EmitsRegistryCommands()
    {
        var sb = new StringBuilder();
        // The REAL catalog HKLM registry toggle (security-remote-assistance, RegTarget fAllowToGetHelp,
        // HKLM DWORD) - since Slice 7e-6 the dict carries the catalog Setting itself.
        var featureGroup = CreateFeatureGroup(FeatureIds.Privacy, new[]
        {
            new ConfigurationItem
            {
                Id = "security-remote-assistance",
                IsSelected = true,
                InputType = InputType.Toggle
            }
        });

        var allSettings = SettingsFor(FeatureIds.Privacy, "security-remote-assistance");

        _sut.AppendFeatureGroupRegistryEntries(sb, featureGroup, allSettings, "Optimize", isHkcu: false, indent: "    ");

        var output = sb.ToString();
        output.Should().Contain("Set-RegistryValue");
        output.Should().Contain("fAllowToGetHelp");
    }

    // ---------------------------------------------------------------
    // AppendFeatureGroupRegistryEntries - HKCU entries only in HKCU pass
    // ---------------------------------------------------------------

    [Fact]
    public void AppendFeatureGroupRegistryEntries_HkcuEntries_NotEmittedInHklmPass()
    {
        var sb = new StringBuilder();
        // The REAL HKCU-only catalog toggle (gaming-game-mode, RegTarget AutoGameModeEnabled under
        // HKEY_CURRENT_USER) - this negative proves the HIVE filter, not an unknown-id skip.
        var featureGroup = CreateFeatureGroup("TestFeature", new[]
        {
            new ConfigurationItem
            {
                Id = "gaming-game-mode",
                IsSelected = true,
                InputType = InputType.Toggle
            }
        });

        var allSettings = SettingsFor("TestFeature", "gaming-game-mode");

        _sut.AppendFeatureGroupRegistryEntries(sb, featureGroup, allSettings, "Customize", isHkcu: false, indent: "    ");

        sb.ToString().Should().NotContain("Set-RegistryValue");
    }

    [Fact]
    public void AppendFeatureGroupRegistryEntries_HkcuEntries_EmittedInHkcuPass()
    {
        var sb = new StringBuilder();
        var featureGroup = CreateFeatureGroup("TestFeature", new[]
        {
            new ConfigurationItem
            {
                Id = "gaming-game-mode",
                IsSelected = true,
                InputType = InputType.Toggle
            }
        });

        var allSettings = SettingsFor("TestFeature", "gaming-game-mode");

        _sut.AppendFeatureGroupRegistryEntries(sb, featureGroup, allSettings, "Customize", isHkcu: true, indent: "    ");

        var output = sb.ToString();
        output.Should().Contain("Set-RegistryValue");
        output.Should().Contain("AutoGameModeEnabled");
    }

    // ---------------------------------------------------------------
    // AppendFeatureGroupRegistryEntries - Selection type
    // ---------------------------------------------------------------

    [Fact]
    public void AppendFeatureGroupRegistryEntries_SelectionType_DelegatesCorrectly()
    {
        var sb = new StringBuilder();
        // The REAL catalog registry selection (gaming-touch-keyboard-service, HKLM DWORD RegTarget "Start";
        // custom value 3 is arbitrary - the emit path has no lock handling). This no-index +
        // CustomStateValues shape ALSO routes the script pass to the catalog custom-state emitter (7e-5),
        // so the setting's un-baked enabled script (the TextInputHost restore, RunContext.System) emits
        // into this HKLM-pass output alongside the registry write - the assertions below are
        // contains-checks and stay green; noted for byte-level readers.
        var featureGroup = CreateFeatureGroup("TestFeature", new[]
        {
            new ConfigurationItem
            {
                Id = "gaming-touch-keyboard-service",
                InputType = InputType.Selection,
                CustomStateValues = new Dictionary<string, object> { { "Start", 3 } }
            }
        });

        var allSettings = SettingsFor("TestFeature", "gaming-touch-keyboard-service");

        _sut.AppendFeatureGroupRegistryEntries(sb, featureGroup, allSettings, "Optimize", isHkcu: false, indent: "    ");

        var output = sb.ToString();
        output.Should().Contain("Set-RegistryValue");
        output.Should().Contain("-Name 'Start'");
    }

    // ---------------------------------------------------------------
    // AppendFeatureGroupRegistryEntries - Scheduled tasks
    // ---------------------------------------------------------------

    [Fact]
    public void AppendFeatureGroupRegistryEntries_WithScheduledTask_EmitsTaskBatch()
    {
        var sb = new StringBuilder();
        // Slice E1a: the scheduled-task emit sources task paths + description from the catalog Setting
        // (TaskTarget + Display.Description) - the fixture is the REAL catalog scheduled-task setting.
        var featureGroup = CreateFeatureGroup("TestFeature", new[]
        {
            new ConfigurationItem
            {
                Id = "gaming-task-compatibility-appraiser",
                IsSelected = false,
                InputType = InputType.Toggle
            }
        });

        var allSettings = SettingsFor("TestFeature", "gaming-task-compatibility-appraiser");

        _sut.AppendFeatureGroupRegistryEntries(sb, featureGroup, allSettings, "Optimize", isHkcu: false, indent: "    ");

        var output = sb.ToString();
        output.Should().Contain("$scheduledTasks");
        output.Should().Contain("schtasks");
        output.Should().Contain("/Disable");
    }

    // ---------------------------------------------------------------
    // AppendFeatureGroupRegistryEntries - Hibernation
    // ---------------------------------------------------------------

    [Fact]
    public void AppendFeatureGroupRegistryEntries_Hibernation_EmitsPowercfgHibernate()
    {
        var sb = new StringBuilder();
        // The REAL catalog setting power-hibernation-enable: the hibernation powercfg emit keys off the
        // canonical Setting.Id in the scheduled-task pass (and the id also force-opens the HKLM section).
        var featureGroup = CreateFeatureGroup("TestFeature", new[]
        {
            new ConfigurationItem
            {
                Id = "power-hibernation-enable",
                IsSelected = true,
                InputType = InputType.Toggle
            }
        });

        var allSettings = SettingsFor("TestFeature", "power-hibernation-enable");

        _sut.AppendFeatureGroupRegistryEntries(sb, featureGroup, allSettings, "Optimize", isHkcu: false, indent: "    ");

        var output = sb.ToString();
        output.Should().Contain("powercfg /hibernate on");
    }

    // ---------------------------------------------------------------
    // AppendFeatureGroupRegistryEntries - Section header
    // ---------------------------------------------------------------

    [Fact]
    public void AppendFeatureGroupRegistryEntries_EmitsSectionHeader()
    {
        var sb = new StringBuilder();
        var featureGroup = CreateFeatureGroup(FeatureIds.Privacy, new[]
        {
            new ConfigurationItem { Id = "security-remote-assistance", IsSelected = true, InputType = InputType.Toggle }
        });

        var allSettings = SettingsFor(FeatureIds.Privacy, "security-remote-assistance");

        _sut.AppendFeatureGroupRegistryEntries(sb, featureGroup, allSettings, "Optimize", isHkcu: false, indent: "    ");

        var output = sb.ToString();
        output.Should().Contain("============");
        output.Should().Contain("SETTINGS");
    }

    // ---------------------------------------------------------------
    // AppendFeatureGroupRegistryEntries - powercfg-only setting is skipped
    // ---------------------------------------------------------------

    [Fact]
    public void AppendFeatureGroupRegistryEntries_PowerCfgOnlySetting_IsSkipped()
    {
        var sb = new StringBuilder();
        // The powerCfgOnly skip reads the catalog shape, so the excluded side is the REAL powercfg-only
        // catalog setting (power-display-timeout: PowerCfgTarget, no RegTarget). The REAL HKLM toggle
        // (security-remote-assistance) rides along so the section runs and the per-item loop actually
        // reaches the powerCfgOnly branch. If the branch broke, the CustomStateValues below would drive a
        // powercfg emit and the negative assertion would fail loudly.
        var featureGroup = CreateFeatureGroup("TestFeature", new[]
        {
            new ConfigurationItem
            {
                Id = "power-display-timeout",
                InputType = InputType.Selection,
                CustomStateValues = new Dictionary<string, object> { { "PowerCfgValue", 60 } }
            },
            new ConfigurationItem { Id = "security-remote-assistance", IsSelected = true, InputType = InputType.Toggle }
        });

        var allSettings = SettingsFor("TestFeature", "power-display-timeout", "security-remote-assistance");

        _sut.AppendFeatureGroupRegistryEntries(sb, featureGroup, allSettings, "Optimize", isHkcu: false, indent: "");

        var output = sb.ToString();
        // The section ran (the rider toggle emitted), so the powercfg-only setting was skipped by the
        // powerCfgOnly BRANCH itself - and its CustomStateValues produced no powercfg emission.
        output.Should().Contain("fAllowToGetHelp");
        output.Should().NotContain("powercfg");
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static FeatureGroupSection CreateFeatureGroup(string featureId, ConfigurationItem[] items)
    {
        return new FeatureGroupSection
        {
            IsIncluded = true,
            Features = new Dictionary<string, ConfigSection>
            {
                {
                    featureId, new ConfigSection
                    {
                        IsIncluded = true,
                        Items = items
                    }
                }
            }
        };
    }

    private static Dictionary<string, IReadOnlyList<Setting>> SettingsFor(string featureId, params string[] settingIds)
    {
        return new Dictionary<string, IReadOnlyList<Setting>>
        {
            { featureId, settingIds.Select(id => SettingCatalog.Find(id)!).ToArray() }
        };
    }
}

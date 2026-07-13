using System.Text;
using FluentAssertions;
using Microsoft.Win32;
using Moq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.AdvancedTools.Helpers;
using Winhance.Infrastructure.Features.AdvancedTools.ScriptSections;
using Xunit;

namespace Winhance.Infrastructure.Tests.AdvancedTools;

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

        var allSettings = new Dictionary<string, IEnumerable<SettingDefinition>>();

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
        // Slice 7e-4b: the presence gate is catalog-only, so the fixture pairs to a REAL catalog HKLM registry
        // toggle (security-remote-assistance, RegTarget fAllowToGetHelp, HKLM DWORD) and the def is INERT
        // (id-carrier only) - the toggle emit reads the catalog too, so a regression that reads the def emits
        // nothing and fails loudly.
        var settingDef = CreateSettingDef("security-remote-assistance", "Remote Assistance", Array.Empty<RegistrySetting>());

        var featureGroup = CreateFeatureGroup(FeatureIds.Privacy, new[]
        {
            new ConfigurationItem
            {
                Id = "security-remote-assistance",
                IsSelected = true,
                InputType = InputType.Toggle
            }
        });

        var allSettings = new Dictionary<string, IEnumerable<SettingDefinition>>
        {
            { FeatureIds.Privacy, new[] { settingDef } }
        };

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
        // Slice 7e-4b: catalog-only presence gate - repointed onto a REAL HKCU-only catalog toggle
        // (gaming-game-mode, RegTarget AutoGameModeEnabled under HKEY_CURRENT_USER) so this negative still
        // proves the HIVE filter rather than the unpaired-id skip; the def is INERT (id-carrier only).
        var settingDef = CreateSettingDef("gaming-game-mode", "Game Mode", Array.Empty<RegistrySetting>());

        var featureGroup = CreateFeatureGroup("TestFeature", new[]
        {
            new ConfigurationItem
            {
                Id = "gaming-game-mode",
                IsSelected = true,
                InputType = InputType.Toggle
            }
        });

        var allSettings = new Dictionary<string, IEnumerable<SettingDefinition>>
        {
            { "TestFeature", new[] { settingDef } }
        };

        _sut.AppendFeatureGroupRegistryEntries(sb, featureGroup, allSettings, "Customize", isHkcu: false, indent: "    ");

        sb.ToString().Should().NotContain("Set-RegistryValue");
    }

    [Fact]
    public void AppendFeatureGroupRegistryEntries_HkcuEntries_EmittedInHkcuPass()
    {
        var sb = new StringBuilder();
        // Slice 7e-4b: catalog-only presence gate + catalog-sourced toggle emit - repointed onto the REAL HKCU
        // catalog toggle gaming-game-mode; the def is INERT (id-carrier only), so the emitted value name below
        // can only come from the catalog RegTarget.
        var settingDef = CreateSettingDef("gaming-game-mode", "Game Mode", Array.Empty<RegistrySetting>());

        var featureGroup = CreateFeatureGroup("TestFeature", new[]
        {
            new ConfigurationItem
            {
                Id = "gaming-game-mode",
                IsSelected = true,
                InputType = InputType.Toggle
            }
        });

        var allSettings = new Dictionary<string, IEnumerable<SettingDefinition>>
        {
            { "TestFeature", new[] { settingDef } }
        };

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
        // Slice 7e-4b: catalog-only presence gate - the fixture pairs to a REAL catalog registry selection
        // (gaming-touch-keyboard-service, HKLM DWORD RegTarget "Start"; custom value 3 (arbitrary; the
        // emit path has no lock handling)) and the def is INERT (id-carrier only), the 7e-4a recipe.
        // Slice 7e-5: this no-index + CustomStateValues shape now ALSO routes the script pass to the
        // catalog custom-state emitter, so the setting's un-baked enabled script (the TextInputHost
        // restore, RunContext.System) newly emits into this HKLM-pass output alongside the registry
        // write - the assertions below are contains-checks and stay green; noted for byte-level readers.
        var settingDef = CreateSettingDef("gaming-touch-keyboard-service", "Touch Keyboard", Array.Empty<RegistrySetting>());

        var featureGroup = CreateFeatureGroup("TestFeature", new[]
        {
            new ConfigurationItem
            {
                Id = "gaming-touch-keyboard-service",
                InputType = InputType.Selection,
                CustomStateValues = new Dictionary<string, object> { { "Start", 3 } }
            }
        });

        var allSettings = new Dictionary<string, IEnumerable<SettingDefinition>>
        {
            { "TestFeature", new[] { settingDef } }
        };

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
        // Slice E1a: the scheduled-task emit sources task paths + description from the catalog Setting via
        // SettingCatalog.Find, so the fixture must be a REAL catalog scheduled-task setting (a synthetic/unpaired id
        // would emit no batch). Slice 7e-4b: the presence gate is catalog-only too, so the def is INERT
        // (id-carrier only) - nothing on this path reads its fields anymore.
        var settingDef = new SettingDefinition
        {
            Id = "gaming-task-compatibility-appraiser",
            Name = "Task Setting",
            Description = "Toggle a scheduled task"
        };

        var featureGroup = CreateFeatureGroup("TestFeature", new[]
        {
            new ConfigurationItem
            {
                Id = "gaming-task-compatibility-appraiser",
                IsSelected = false,
                InputType = InputType.Toggle
            }
        });

        var allSettings = new Dictionary<string, IEnumerable<SettingDefinition>>
        {
            { "TestFeature", new[] { settingDef } }
        };

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
        var settingDef = new SettingDefinition
        {
            Id = "power-hibernation-enable",
            Name = "Hibernation",
            Description = "Enable or disable hibernation",
            RegistrySettings = new[]
            {
                new RegistrySetting
                {
                    KeyPath = "HKEY_LOCAL_MACHINE\\SYSTEM\\Test",
                    ValueName = "HibernateEnabled",
                    ValueType = RegistryValueKind.DWord,
                    EnabledValue = [1],
                    DisabledValue = [0],
                    RecommendedValue = null,
                    DefaultValue = null
                }
            }
        };

        var featureGroup = CreateFeatureGroup("TestFeature", new[]
        {
            new ConfigurationItem
            {
                Id = "power-hibernation-enable",
                IsSelected = true,
                InputType = InputType.Toggle
            }
        });

        var allSettings = new Dictionary<string, IEnumerable<SettingDefinition>>
        {
            { "TestFeature", new[] { settingDef } }
        };

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
        // Slice 7e-4b: catalog-only presence gate - the fixture pairs to a REAL catalog HKLM toggle
        // (security-remote-assistance) so the section header still emits; the def is INERT (id-carrier only).
        var settingDef = CreateSettingDef("security-remote-assistance", "Test", Array.Empty<RegistrySetting>());

        var featureGroup = CreateFeatureGroup(FeatureIds.Privacy, new[]
        {
            new ConfigurationItem { Id = "security-remote-assistance", IsSelected = true, InputType = InputType.Toggle }
        });

        var allSettings = new Dictionary<string, IEnumerable<SettingDefinition>>
        {
            { FeatureIds.Privacy, new[] { settingDef } }
        };

        _sut.AppendFeatureGroupRegistryEntries(sb, featureGroup, allSettings, "Optimize", isHkcu: false, indent: "    ");

        var output = sb.ToString();
        output.Should().Contain("============");
        output.Should().Contain("SETTINGS");
    }

    // ---------------------------------------------------------------
    // AppendFeatureGroupRegistryEntries - PowerCfgSettings-only setting is skipped
    // ---------------------------------------------------------------

    [Fact]
    public void AppendFeatureGroupRegistryEntries_PowerCfgOnlySettingDef_IsSkipped()
    {
        var sb = new StringBuilder();
        // Slice 7e-4b: the powerCfgOnly skip is catalog-only, so the excluded side must be a REAL powercfg-only
        // catalog setting (power-display-timeout: PowerCfgTarget, no RegTarget) - a synthetic id would now skip
        // via UNPAIRED (no presence at the section gate) and the fact would go vacuous. A REAL HKLM toggle
        // (security-remote-assistance) rides along so the section runs and the per-item loop actually reaches
        // the powerCfgOnly branch. Both defs are INERT (id-carriers only); if the branch broke, the
        // CustomStateValues below would drive a powercfg emit and the negative assertion would fail loudly.
        var powerCfgOnlyDef = CreateSettingDef("power-display-timeout", "Turn off the display", Array.Empty<RegistrySetting>());
        var toggleDef = CreateSettingDef("security-remote-assistance", "Remote Assistance", Array.Empty<RegistrySetting>());

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

        var allSettings = new Dictionary<string, IEnumerable<SettingDefinition>>
        {
            { "TestFeature", new[] { powerCfgOnlyDef, toggleDef } }
        };

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

    private static SettingDefinition CreateSettingDef(
        string id, string description, IReadOnlyList<RegistrySetting> registrySettings)
    {
        return new SettingDefinition
        {
            Id = id,
            Name = id,
            Description = description,
            RegistrySettings = registrySettings
        };
    }
}

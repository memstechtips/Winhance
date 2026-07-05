using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.AdvancedTools.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Winhance.Infrastructure.Tests.Migration;

/// <summary>Phase 6.8 Slice E1b precondition for repointing the autounattend presence-checks (the section-header
/// hive pre-filter in FeatureRegistryScriptSection, the powercfg-only emit skip, and the
/// WarnOnUnreachableNativePowerApiSettings diagnostic in AutounattendScriptBuilder) off SettingDefinition mechanism
/// reads onto the catalog Setting's Targets/Effects. For EVERY old setting, the NEW catalog presence predicate
/// (<see cref="AutounattendMechanismPresence"/> Setting overloads) must equal the OLD SettingDefinition predicate
/// (the same class's def overloads, a verbatim extraction of the pre-E1b inline logic), for every mechanism and
/// both registry hives. Pure - depends only on the catalog, not the machine.
///
/// Rationale each mechanism is equivalent: the only build-gated targets in the whole catalog are the 6 "This PC
/// folder" toggles, whose two per-OS RegTargets are BOTH HKLM, so hive presence is build-invariant; no TaskTarget /
/// ScriptEffect / RegContentEffect / NativePowerEffect / PowerCfgTarget is build-gated. Scripts/regcontent are
/// body-based in the catalog (the converter drops empty bodies), so a both-empty-body mechanism - which emits
/// nothing anyway - would read absent here; a divergence therefore surfaces as a RED row for triage rather than a
/// silent header change.
/// Run: dotnet test --filter MechanismPresenceEquivalence</summary>
public class MechanismPresenceEquivalenceTests
{
    private readonly ITestOutputHelper _output;

    public MechanismPresenceEquivalenceTests(ITestOutputHelper output) => _output = output;

    private static IEnumerable<SettingDefinition> AllDefinitions()
    {
        return new[]
        {
            ExplorerCustomizations.GetExplorerCustomizations().Settings,
            StartMenuCustomizations.GetStartMenuCustomizations().Settings,
            TaskbarCustomizations.GetTaskbarCustomizations().Settings,
            WindowsThemeCustomizations.GetWindowsThemeCustomizations().Settings,
            PowerOptimizations.GetPowerOptimizations().Settings,
            GamingAndPerformanceOptimizations.GetGamingAndPerformanceOptimizations().Settings,
            NotificationOptimizations.GetNotificationOptimizations().Settings,
            PrivacyAndSecurityOptimizations.GetPrivacyAndSecurityOptimizations().Settings,
            SoundOptimizations.GetSoundOptimizations().Settings,
            UpdateOptimizations.GetUpdateOptimizations().Settings,
        }.SelectMany(group => group);
    }

    [Fact]
    public void MechanismPresenceEquivalence_CatalogMatchesSettingDefinition()
    {
        var mismatches = new List<string>();
        var compared = new List<string>();
        int unpaired = 0;

        foreach (var def in AllDefinitions())
        {
            var setting = SettingCatalog.Find(def.Id);
            if (setting == null)
            {
                unpaired++;
                continue;
            }

            compared.Add(def.Id);

            void Check(string mechanism, bool oldValue, bool newValue)
            {
                if (oldValue != newValue)
                    mismatches.Add($"{def.Id} {mechanism}: old(def)={oldValue} new(catalog)={newValue}");
            }

            foreach (var isHkcu in new[] { false, true })
            {
                Check($"RegistryInHive(hkcu={isHkcu})",
                    AutounattendMechanismPresence.HasRegistryInHive(def, isHkcu),
                    AutounattendMechanismPresence.HasRegistryInHive(setting, isHkcu));
                Check($"ScriptInHive(hkcu={isHkcu})",
                    AutounattendMechanismPresence.HasScriptInHive(def, isHkcu),
                    AutounattendMechanismPresence.HasScriptInHive(setting, isHkcu));
            }

            Check("Script",
                AutounattendMechanismPresence.HasScript(def),
                AutounattendMechanismPresence.HasScript(setting));
            Check("ScheduledTask",
                AutounattendMechanismPresence.HasScheduledTask(def),
                AutounattendMechanismPresence.HasScheduledTask(setting));
            Check("Registry",
                AutounattendMechanismPresence.HasRegistry(def),
                AutounattendMechanismPresence.HasRegistry(setting));
            Check("PowerCfg",
                AutounattendMechanismPresence.HasPowerCfg(def),
                AutounattendMechanismPresence.HasPowerCfg(setting));
            Check("RegContent",
                AutounattendMechanismPresence.HasRegContent(def),
                AutounattendMechanismPresence.HasRegContent(setting));
            Check("NativePower",
                AutounattendMechanismPresence.HasNativePower(def),
                AutounattendMechanismPresence.HasNativePower(setting));
        }

        _output.WriteLine($"{compared.Count} settings compared, {mismatches.Count} mismatches, {unpaired} unpaired");
        foreach (var m in mismatches)
            _output.WriteLine(m);

        // A zero-coverage bug must not pass vacuously: settings must be present and every one catalog-paired.
        Assert.NotEmpty(compared);
        Assert.Equal(0, unpaired);

        Assert.True(
            mismatches.Count == 0,
            $"{mismatches.Count} catalog-vs-SettingDefinition mechanism-presence mismatches:\n"
                + string.Join("\n", mismatches));
    }
}

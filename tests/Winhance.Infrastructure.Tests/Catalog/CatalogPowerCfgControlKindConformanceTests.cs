using Winhance.Core.Features.Common.Catalog;
using Xunit;

namespace Winhance.Infrastructure.Tests.Catalog;

// A powercfg setting that derives as ControlKind.Toggle makes two computations disagree: BuildPowerCfgApplyValue
// gates its state branch on Control == Selection and with Numeric null returns null, so the UI apply pipeline
// SILENTLY drops the setting, while ComputePlanRecommendedWrite gates on States.Count and stamps it into every
// freshly-created power plan. Localization-key state labels keep that unreachable today - nothing else enforces it.
// Run: winhance-harness CatalogPowerCfgControlKindConformanceTests
public class CatalogPowerCfgControlKindConformanceTests
{
    [Fact]
    public void Powercfg_settings_never_derive_as_toggle()
    {
        var powerCfgSettings = SettingCatalog.All
            .Where(s => s.Targets.OfType<PowerCfgTarget>().Any())
            .ToList();

        // Guard against a vacuous pass: the catalog is really loaded, and PowerCfgTarget carriers are really found.
        Assert.True(
            SettingCatalog.All.Count >= 400,
            $"SettingCatalog.All returned {SettingCatalog.All.Count} settings (414 today) - the catalog accessor is "
                + "not returning the catalog, so this test is checking nothing.");

        Assert.True(
            powerCfgSettings.Count >= 30,
            $"Only {powerCfgSettings.Count} settings carry a PowerCfgTarget (41 today) - Targets or PowerCfgTarget "
                + "no longer match the way this test looks for them, so this test is checking nothing.");

        var violations = powerCfgSettings
            .Where(s => s.Control == ControlKind.Toggle)
            .Select(s => s.Id)
            .ToList();

        Assert.True(
            violations.Count == 0,
            "A powercfg setting deriving as ControlKind.Toggle makes BuildPowerCfgApplyValue and "
                + "ComputePlanRecommendedWrite disagree: the UI apply path silently DROPS it, while the plan path "
                + "STAMPS it into every freshly-created power plan. Fix it by giving the states localization-key "
                + "labels (what every other powercfg setting uses) or by aligning the two predicates:\n"
                + string.Join("\n", violations));
    }
}

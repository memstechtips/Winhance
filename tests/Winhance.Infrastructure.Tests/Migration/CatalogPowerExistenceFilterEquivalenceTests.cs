using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Migration;

/// <summary>Proves CatalogPowerExistenceFilter reproduces the old PowerSettingsValidationService existence decision.
/// Machine-independent: the old + new filters run with stubbed probes (bulk powercfg query, enablement write,
/// hardware-control) over the same constructed present-GUID scenarios, comparing surviving id-sets. Plus a focused
/// enable-success case (a hidden target is unhidden and kept).</summary>
public class CatalogPowerExistenceFilterEquivalenceTests
{
    private static IReadOnlyList<SettingDefinition> OldDefs => PowerOptimizations.GetPowerOptimizations().Settings;
    private static IReadOnlyList<Setting> Catalog => SettingCatalog.ByFeature[FeatureIds.Power];

    private static HashSet<string> AllPowerGuids() => OldDefs.Where(d => d.PowerCfgSettings != null)
        .SelectMany(d => d.PowerCfgSettings!).Select(p => p.SettingGuid).ToHashSet();

    private static HashSet<string> HardwareControlledGuids() => OldDefs.Where(d => d.PowerCfgSettings != null)
        .SelectMany(d => d.PowerCfgSettings!).Where(p => p.CheckForHardwareControl).Select(p => p.SettingGuid).ToHashSet();

    private static Dictionary<string, (int?, int?)> Dict(IEnumerable<string> guids)
        => guids.ToDictionary(g => g, g => ((int?)0, (int?)0));

    private static HashSet<string> RunOld(HashSet<string> present, bool hwActive, HashSet<string> hw)
    {
        var query = new Mock<IPowerSettingsQueryService>();
        query.Setup(q => q.GetAllPowerSettingsACDCAsync(It.IsAny<string>())).ReturnsAsync(Dict(present));
        query.Setup(q => q.IsSettingHardwareControlledAsync(It.IsAny<PowerCfgSetting>()))
            .ReturnsAsync((PowerCfgSetting p) => hwActive && hw.Contains(p.SettingGuid));
        var reg = new Mock<IWindowsRegistryService>();
        reg.Setup(r => r.ApplySetting(It.IsAny<RegistrySetting>(), It.IsAny<bool>(), It.IsAny<object?>(), It.IsAny<bool>())).Returns(false);
        var svc = new PowerSettingsValidationService(new Mock<ILogService>().Object, query.Object, reg.Object);
        return svc.FilterSettingsByExistenceAsync(OldDefs).GetAwaiter().GetResult().Select(d => d.Id).ToHashSet();
    }

    private static HashSet<string> RunNew(HashSet<string> present, bool hwActive, HashSet<string> hw)
    {
        var query = new Mock<IPowerSettingsQueryService>();
        query.Setup(q => q.GetAllPowerSettingsACDCAsync(It.IsAny<string>())).ReturnsAsync(Dict(present));
        query.Setup(q => q.IsSettingHardwareControlledAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((string sub, string set) => hwActive && hw.Contains(set));
        var reg = new Mock<IWindowsRegistryService>();
        reg.Setup(r => r.SetValue(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<RegistryValueKind>())).Returns(false);
        var svc = new CatalogPowerExistenceFilter(query.Object, reg.Object, new Mock<ILogService>().Object);
        return svc.FilterAsync(Catalog).GetAwaiter().GetResult().Select(s => s.Id).ToHashSet();
    }

    [Fact]
    public void Old_and_new_existence_filters_agree_across_scenarios()
    {
        var all = AllPowerGuids();
        var hw = HardwareControlledGuids();
        Assert.NotEmpty(hw);

        Assert.Equal(RunOld(all, false, hw), RunNew(all, false, hw));
        Assert.Equal(RunOld(all, true, hw), RunNew(all, true, hw));

        var absent = new HashSet<string>(all);
        absent.ExceptWith(all.Take(5));
        Assert.Equal(RunOld(absent, false, hw), RunNew(absent, false, hw));

        Assert.True(RunNew(all, true, hw).Count < RunNew(all, false, hw).Count);
    }

    [Fact]
    public void New_filter_keeps_a_setting_whose_hidden_powercfg_target_is_enabled()
    {
        var pick = Catalog.SelectMany(s => s.Targets.OfType<PowerCfgTarget>().Select(t => (s, t)))
            .First(x => x.t.EnablementKey is not null && x.s.Availability.ValidatesExistence);
        var guid = pick.t.SettingGuid;

        var present = AllPowerGuids();
        present.Remove(guid);
        Assert.NotEmpty(present);

        var query = new Mock<IPowerSettingsQueryService>();
        query.Setup(q => q.GetAllPowerSettingsACDCAsync(It.IsAny<string>())).ReturnsAsync(() => Dict(present));
        query.Setup(q => q.IsSettingHardwareControlledAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);
        var reg = new Mock<IWindowsRegistryService>();
        reg.Setup(r => r.SetValue(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<RegistryValueKind>()))
            .Returns((string path, string vn, object v, RegistryValueKind k) => { present.Add(path.Split('\\').Last()); return true; });
        var svc = new CatalogPowerExistenceFilter(query.Object, reg.Object, new Mock<ILogService>().Object);

        var result = svc.FilterAsync(Catalog).GetAwaiter().GetResult();
        Assert.Contains(result, s => s.Id == pick.s.Id);
    }
}

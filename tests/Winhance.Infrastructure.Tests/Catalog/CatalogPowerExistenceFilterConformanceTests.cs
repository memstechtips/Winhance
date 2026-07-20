using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Catalog;

/// <summary>Machine-independent conformance for CatalogPowerExistenceFilter, driven off the catalog alone: with the
/// probes stubbed (bulk powercfg AC/DC query, enablement write, hardware-control), a setting whose powercfg target is
/// HIDDEN but is successfully unhidden through its enablement key survives the filter.</summary>
public class CatalogPowerExistenceFilterConformanceTests
{
    private static IReadOnlyList<Setting> Catalog => SettingCatalog.ByFeature[FeatureIds.Power];

    /// <summary>Every powercfg GUID the power catalog targets. Catalog-derived. NOTE a PowerCfgTarget's
    /// EnablementKey is a NESTED RegTarget, not a top-level Target, so reading the
    /// powercfg targets off Targets yields the setting's own powercfg mechanisms and nothing else.</summary>
    private static HashSet<string> AllPowerGuids() => Catalog
        .SelectMany(s => s.Targets.OfType<PowerCfgTarget>()).Select(t => t.SettingGuid).ToHashSet();


    private static Dictionary<string, (int?, int?)> Dict(IEnumerable<string> guids)
        => guids.ToDictionary(g => g, g => ((int?)0, (int?)0));


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

    /// <summary>Production hardcodes the unhide write as SetValue(path, "Attributes", 0, DWord) -- it MUST, because
    /// PowerCfgTarget.EnablementKey models only path/name/type and carries NO write value. That hardcoding is safe
    /// only because every enablement writes exactly that constant. The =0 VALUE half is unmodellable catalog-side;
    /// the name/type half is still checkable, and is pinned here so a future enablement key authored with a
    /// different name/type fails loudly instead of being silently mis-written.</summary>
    [Fact]
    public void Every_powercfg_enablement_key_is_the_constant_attributes_dword()
    {
        var keys = SettingCatalog.All
            .SelectMany(s => s.Targets.OfType<PowerCfgTarget>())
            .Select(t => t.EnablementKey)
            .Where(k => k is not null)
            .Select(k => k!)
            .ToList();

        Assert.NotEmpty(keys);
        foreach (var k in keys)
        {
            Assert.Equal("Attributes", k.ValueName);
            Assert.Equal(Microsoft.Win32.RegistryValueKind.DWord, k.Type);
        }
    }
}

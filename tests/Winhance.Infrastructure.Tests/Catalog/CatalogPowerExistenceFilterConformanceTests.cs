using Microsoft.Win32;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Catalog;

// With the probes stubbed, a setting whose powercfg target is HIDDEN but successfully unhidden through its
// enablement key survives the filter.
public class CatalogPowerExistenceFilterConformanceTests
{
    private static IReadOnlyList<Setting> Catalog => SettingCatalog.ByFeature[FeatureIds.Power];

    // A PowerCfgTarget's EnablementKey is a NESTED RegTarget, not a top-level Target, so reading powercfg targets
    // off Targets yields the setting's own mechanisms and nothing else.
    private static HashSet<string> AllPowerGuids() => Catalog
        .SelectMany(s => s.Targets.OfType<PowerCfgTarget>()).Select(t => t.SettingGuid).ToHashSet();


    private static Dictionary<string, (int?, int?)> Dict(IEnumerable<string> guids)
        => guids.ToDictionary(g => g, g => ((int?)0, (int?)0));


    [Fact]
    public async Task New_filter_keeps_a_setting_whose_hidden_powercfg_target_is_enabled()
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
        var svc = new CatalogPowerExistenceFilter(query.Object, reg.Object, new Mock<IScheduledTaskStateService>().Object, new Mock<ILogService>().Object);

        var result = await svc.FilterAsync(Catalog);
        Assert.Contains(result, s => s.Id == pick.s.Id);
    }

    // Production hardcodes the unhide write as SetValue(path, "Attributes", 0, DWord) because EnablementKey
    // models no write value; safe only while every enablement writes exactly that constant - the name/type half is pinned here.
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

    private static (CatalogPowerExistenceFilter svc, Setting pick) TaskFixture(bool? taskEnabledAnswer)
    {
        var pick = SettingCatalog.ByFeature[FeatureIds.GamingPerformance]
            .First(s => s.Availability.ValidatesExistence && s.Targets.OfType<TaskTarget>().Any());
        var query = new Mock<IPowerSettingsQueryService>();
        query.Setup(q => q.GetAllPowerSettingsACDCAsync(It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, (int?, int?)>());
        var tasks = new Mock<IScheduledTaskStateService>();
        tasks.Setup(t => t.GetTasksEnabled(It.IsAny<IReadOnlyCollection<string>>()))
            .Returns((IReadOnlyCollection<string> paths) =>
                paths.ToDictionary(p => p, _ => taskEnabledAnswer));
        var svc = new CatalogPowerExistenceFilter(
            query.Object, new Mock<IWindowsRegistryService>().Object, tasks.Object, new Mock<ILogService>().Object);
        return (svc, pick);
    }

    [Fact]
    public async Task Task_setting_with_no_registered_task_is_filtered_out()
    {
        var (svc, pick) = TaskFixture(taskEnabledAnswer: null);
        var result = await svc.FilterAsync(new[] { pick });
        Assert.DoesNotContain(result, s => s.Id == pick.Id);
    }

    [Fact]
    public async Task Task_setting_with_a_registered_task_survives()
    {
        var (svc, pick) = TaskFixture(taskEnabledAnswer: false);
        var result = await svc.FilterAsync(new[] { pick });
        Assert.Contains(result, s => s.Id == pick.Id);
    }
}

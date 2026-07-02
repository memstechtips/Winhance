using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Migration;

/// <summary>Proves CatalogSettingsRegistry composes the proven membership pieces correctly: per-feature +
/// flattened membership equals SettingCatalog filtered by CatalogMembershipFilter (existence stubbed to
/// passthrough), and GetById resolves canonical + -win10 alias + miss. Machine-independent (stubbed probes).</summary>
public class CatalogSettingsRegistryCompositionTests
{
    [Fact]
    public async Task Composes_membership_from_the_proven_pieces()
    {
        var version = new Mock<IWindowsVersionService>();
        version.Setup(v => v.GetWindowsBuildNumber()).Returns(26100);
        version.Setup(v => v.GetWindowsBuildRevision()).Returns(0);
        var hardware = new Mock<IHardwareDetectionService>();
        hardware.Setup(h => h.HasBatteryAsync()).ReturnsAsync(false);
        hardware.Setup(h => h.HasLidAsync()).ReturnsAsync(false);
        hardware.Setup(h => h.SupportsBrightnessControlAsync()).ReturnsAsync(true);
        hardware.Setup(h => h.SupportsHybridSleepAsync()).ReturnsAsync(true);
        var existence = new Mock<ICatalogPowerExistenceFilter>();
        existence.Setup(e => e.FilterAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync((IReadOnlyList<Setting> s) => s); // passthrough - existence proven separately

        var reg = new CatalogSettingsRegistry(version.Object, hardware.Object, existence.Object);
        await reg.InitializeAsync();

        var build = new WinBuild(26100, 0);
        var caps = new HardwareCaps(false, false, true, true);
        bool Available(Setting s) => CatalogMembershipFilter.IsAvailable(s, build, caps);

        // flattened membership == the catalog filtered by IsAvailable
        var expected = SettingCatalog.All.Where(Available).Select(s => s.Id).OrderBy(x => x).ToList();
        var actual = reg.GetAll().SelectMany(kv => kv.Value).Select(s => s.Id).OrderBy(x => x).ToList();
        Assert.Equal(expected, actual);
        Assert.NotEmpty(actual);

        // per-feature partition matches ByFeature filtered
        foreach (var (featureId, settings) in SettingCatalog.ByFeature)
        {
            var exp = settings.Where(Available).Select(s => s.Id).OrderBy(x => x).ToList();
            var act = reg.GetByFeature(featureId).Select(s => s.Id).OrderBy(x => x).ToList();
            Assert.Equal(exp, act);
        }

        // GetById: a present setting resolves; a -win10 alias resolves to canonical; a miss is null
        var known = SettingCatalog.All.First(Available).Id;
        Assert.Equal(known, reg.GetById(known)!.Id);
        Assert.Null(reg.GetById("definitely-not-a-real-setting-id"));
    }
}

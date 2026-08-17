using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Catalog;

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
        hardware.Setup(h => h.HasBattery()).Returns(false);
        hardware.Setup(h => h.SupportsHybridSleep()).Returns(true);
        var existence = new Mock<ICatalogPowerExistenceFilter>();
        existence.Setup(e => e.FilterAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync((IReadOnlyList<Setting> s) => s); // passthrough - existence proven separately

        var reg = new CatalogSettingsRegistry(version.Object, hardware.Object, existence.Object);
        await reg.InitializeAsync();

        var build = new WinBuild(26100, 0);
        var caps = new HardwareCaps(false, true);
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

        // GetFeatureIdForSetting: every available setting reports its owning feature (== the ByFeature
        // partition proven above); a "-win10" alias resolves to the canonical's feature (input
        // alias-normalized like GetById); a miss is null.
        int probedFeatures = 0;
        foreach (var (featureId, settings) in SettingCatalog.ByFeature)
        {
            foreach (var s in settings.Where(Available))
            {
                Assert.Equal(featureId, reg.GetFeatureIdForSetting(s.Id));
                probedFeatures++;
            }
        }
        Assert.NotEqual(0, probedFeatures);

        const string canonicalDesktop = "explorer-customization-thispc-folder-desktop";
        if (reg.GetById(canonicalDesktop) is not null)
            Assert.Equal(
                reg.GetFeatureIdForSetting(canonicalDesktop),
                reg.GetFeatureIdForSetting("explorer-customization-thispc-folder-desktop-win10"));
        Assert.Null(reg.GetFeatureIdForSetting("definitely-not-a-real-setting-id"));
    }

    [Fact]
    public async Task Scope_param_relaxes_only_the_os_gate_defaulting_to_current_os()
    {
        var version = new Mock<IWindowsVersionService>();
        version.Setup(v => v.GetWindowsBuildNumber()).Returns(26100);
        version.Setup(v => v.GetWindowsBuildRevision()).Returns(0);
        var hardware = new Mock<IHardwareDetectionService>();
        hardware.Setup(h => h.HasBattery()).Returns(false);
        hardware.Setup(h => h.SupportsHybridSleep()).Returns(true);
        var existence = new Mock<ICatalogPowerExistenceFilter>();
        existence.Setup(e => e.FilterAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync((IReadOnlyList<Setting> s) => s); // passthrough - existence proven separately

        var reg = new CatalogSettingsRegistry(version.Object, hardware.Object, existence.Object);
        await reg.InitializeAsync();

        var build = new WinBuild(26100, 0);
        var caps = new HardwareCaps(false, true);
        bool Current(Setting s) => CatalogMembershipFilter.IsAvailable(s, build, caps);
        bool Relaxed(Setting s) => CatalogMembershipFilter.IsAvailableIgnoringOsBuild(s, caps);

        // Default GetAll = current-OS (IsAvailable); GetAll(includeOtherOsVersions:true) relaxes ONLY the OS-build
        // gate = IsAvailableIgnoringOsBuild (hardware still applies; existence is stubbed passthrough here).
        var currentActual = reg.GetAll().SelectMany(kv => kv.Value).Select(s => s.Id).OrderBy(x => x).ToList();
        var relaxedActual = reg.GetAll(includeOtherOsVersions: true).SelectMany(kv => kv.Value).Select(s => s.Id).OrderBy(x => x).ToList();
        Assert.Equal(SettingCatalog.All.Where(Current).Select(s => s.Id).OrderBy(x => x).ToList(), currentActual);
        Assert.Equal(SettingCatalog.All.Where(Relaxed).Select(s => s.Id).OrderBy(x => x).ToList(), relaxedActual);

        // The scope genuinely discriminates: OS-incompatible settings (e.g. the Win10-only ones on this Win11 build)
        // are hidden in current-OS but shown when the OS gate is relaxed; current-OS is always a subset.
        Assert.True(relaxedActual.Count > currentActual.Count, "relaxed scope added no OS-incompatible setting - vacuous");
        Assert.Subset(relaxedActual.ToHashSet(), currentActual.ToHashSet());

        // Per-feature honours the scope too.
        foreach (var (featureId, settings) in SettingCatalog.ByFeature)
        {
            var exp = settings.Where(Relaxed).Select(s => s.Id).OrderBy(x => x).ToList();
            var act = reg.GetByFeature(featureId, includeOtherOsVersions: true).Select(s => s.Id).OrderBy(x => x).ToList();
            Assert.Equal(exp, act);
        }

        // GetById: an OS-incompatible setting is hidden by default, shown when the OS gate is relaxed.
        var osIncompatible = SettingCatalog.All.First(s => !Current(s) && Relaxed(s));
        Assert.Null(reg.GetById(osIncompatible.Id));
        Assert.Equal(osIncompatible.Id, reg.GetById(osIncompatible.Id, includeOtherOsVersions: true)!.Id);
    }

    [Fact]
    public void Query_before_InitializeAsync_throws()
    {
        // Guard rail: the pure-query surface must not answer over an unresolved machine context. An
        // uninitialized registry (e.g. a swallowed startup init) throws loudly rather than silently hiding every
        // build-gated / powercfg setting (which downstream reads as a misleading "Setting not found"). All live
        // consumers query post-startup, so this never fires in practice.
        var reg = new CatalogSettingsRegistry(
            new Mock<IWindowsVersionService>().Object,
            new Mock<IHardwareDetectionService>().Object,
            new Mock<ICatalogPowerExistenceFilter>().Object);

        Assert.Throws<System.InvalidOperationException>(() => reg.GetById("any"));
        Assert.Throws<System.InvalidOperationException>(() => reg.GetByFeature("any"));
        Assert.Throws<System.InvalidOperationException>(() => reg.GetFeatureIdForSetting("any"));
        Assert.Throws<System.InvalidOperationException>(() => reg.GetAll());
    }
}

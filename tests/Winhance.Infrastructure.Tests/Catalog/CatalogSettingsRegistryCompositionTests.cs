using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Catalog;

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

        var expected = SettingCatalog.All.Where(Available).Select(s => s.Id).OrderBy(x => x).ToList();
        var actual = reg.GetAll().SelectMany(kv => kv.Value).Select(s => s.Id).OrderBy(x => x).ToList();
        Assert.Equal(expected, actual);
        Assert.NotEmpty(actual);

        foreach (var (featureId, settings) in SettingCatalog.ByFeature)
        {
            var exp = settings.Where(Available).Select(s => s.Id).OrderBy(x => x).ToList();
            var act = reg.GetByFeature(featureId).Select(s => s.Id).OrderBy(x => x).ToList();
            Assert.Equal(exp, act);
        }

        var known = SettingCatalog.All.First(Available).Id;
        Assert.Equal(known, reg.GetById(known)!.Id);
        Assert.Null(reg.GetById("definitely-not-a-real-setting-id"));

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

        var currentActual = reg.GetAll().SelectMany(kv => kv.Value).Select(s => s.Id).OrderBy(x => x).ToList();
        var relaxedActual = reg.GetAll(new CatalogScope(true, false)).SelectMany(kv => kv.Value).Select(s => s.Id).OrderBy(x => x).ToList();
        Assert.Equal(SettingCatalog.All.Where(Current).Select(s => s.Id).OrderBy(x => x).ToList(), currentActual);
        Assert.Equal(SettingCatalog.All.Where(Relaxed).Select(s => s.Id).OrderBy(x => x).ToList(), relaxedActual);

        Assert.True(relaxedActual.Count > currentActual.Count, "relaxed scope added no OS-incompatible setting - vacuous");
        Assert.Subset(relaxedActual.ToHashSet(), currentActual.ToHashSet());

        foreach (var (featureId, settings) in SettingCatalog.ByFeature)
        {
            var exp = settings.Where(Relaxed).Select(s => s.Id).OrderBy(x => x).ToList();
            var act = reg.GetByFeature(featureId, new CatalogScope(true, false)).Select(s => s.Id).OrderBy(x => x).ToList();
            Assert.Equal(exp, act);
        }

        var osIncompatible = SettingCatalog.All.First(s => !Current(s) && Relaxed(s));
        Assert.Null(reg.GetById(osIncompatible.Id));
        Assert.Equal(osIncompatible.Id, reg.GetById(osIncompatible.Id, new CatalogScope(true, false))!.Id);
    }

    [Fact]
    public async Task IncludeOtherHardware_ShowsBatteryOnlySetting_OnDesktop()
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
        var batteryOnly = SettingCatalog.All.First(s =>
            s.Availability.Hardware.Contains(HardwareRequirement.Battery) && s.Availability.Allows(build));
        var otherHardware = new CatalogScope(IncludeOtherOsVersions: false, IncludeOtherHardware: true);

        Assert.Null(reg.GetById(batteryOnly.Id));
        Assert.Equal(batteryOnly.Id, reg.GetById(batteryOnly.Id, otherHardware)!.Id);

        var current = reg.GetAll().SelectMany(kv => kv.Value).Select(s => s.Id).ToHashSet();
        var relaxed = reg.GetAll(otherHardware).SelectMany(kv => kv.Value).Select(s => s.Id).ToHashSet();
        Assert.DoesNotContain(batteryOnly.Id, current);
        Assert.Contains(batteryOnly.Id, relaxed);
        Assert.Subset(relaxed, current);
    }

    [Fact]
    public async Task IncludeOtherHardware_KeepsExistenceGate_ForHardwareNeutralSettings()
    {
        var version = new Mock<IWindowsVersionService>();
        version.Setup(v => v.GetWindowsBuildNumber()).Returns(26100);
        version.Setup(v => v.GetWindowsBuildRevision()).Returns(0);
        var hardware = new Mock<IHardwareDetectionService>();
        hardware.Setup(h => h.HasBattery()).Returns(false);
        hardware.Setup(h => h.SupportsHybridSleep()).Returns(true);
        var existence = new Mock<ICatalogPowerExistenceFilter>();
        // No powercfg GUID resolves on this machine, so nothing reaches the existence-passed set.
        existence.Setup(e => e.FilterAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(Array.Empty<Setting>());

        var reg = new CatalogSettingsRegistry(version.Object, hardware.Object, existence.Object);
        await reg.InitializeAsync();

        var build = new WinBuild(26100, 0);
        var otherHardware = new CatalogScope(IncludeOtherOsVersions: false, IncludeOtherHardware: true);

        var hardwareNeutral = SettingCatalog.All.First(s =>
            s.Availability.ValidatesExistence && s.Availability.Hardware.Count == 0 && s.Availability.Allows(build));
        var batteryGated = SettingCatalog.All.First(s =>
            s.Availability.ValidatesExistence
            && s.Availability.Hardware.Contains(HardwareRequirement.Battery)
            && s.Availability.Allows(build));

        // The hardware relax is not an existence relax: a setting this machine could have and simply does not
        // expose stays hidden.
        Assert.Null(reg.GetById(hardwareNeutral.Id, otherHardware));
        Assert.Equal(batteryGated.Id, reg.GetById(batteryGated.Id, otherHardware)!.Id);
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

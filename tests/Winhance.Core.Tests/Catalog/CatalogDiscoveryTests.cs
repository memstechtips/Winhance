using System.Collections.Generic;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

public class CatalogDiscoveryTests
{
    private sealed class FakeCtx : IDetectionContext
    {
        public object? GetValue(string keyPath, string? valueName) => null;
        public string[] GetSubKeyNames(string keyPath) => System.Array.Empty<string>();
        public string? PrimaryDnsV4OfActiveAdapter() => null;
        public bool IsSystemRestoreEnabled() => false;
    }

    private sealed class FixedDetector : IStateDetector
    {
        private readonly string? _r;
        public FixedDetector(string? r) => _r = r;
        public string? Detect(Setting setting, IDetectionContext context) => _r;
    }

    private static RegTarget Reg(string key, string valueName) =>
        new(key, new[] { @"HKEY_LOCAL_MACHINE\TEST" }, valueName, RegistryValueKind.DWord);

    [Fact]
    public void Resolves_state_from_registry_reads()
    {
        var setting = new Setting
        {
            Id = "s", Name = "s", Description = "s",
            Targets = new[] { Reg("Mode", "SearchboxTaskbarMode") },
            States = new[]
            {
                new SettingState { Label = "Hide", Set = new Dictionary<string, StateValue> { ["Mode"] = StateValue.Of(0) } },
                new SettingState { Label = "Box",  Set = new Dictionary<string, StateValue> { ["Mode"] = StateValue.Of(2) } },
            },
        };
        // raw read returns 2 for the target's value
        var state = CatalogDiscovery.DetectState(setting, (p, v) => 2, new FakeCtx());
        Assert.Equal("Box", state);
    }

    [Fact]
    public void Absent_read_resolves_via_or_absent_state()
    {
        var setting = new Setting
        {
            Id = "s", Name = "s", Description = "s",
            Targets = new[] { Reg("Start", "Start") },
            States = new[]
            {
                new SettingState { Label = "Manual", Set = new Dictionary<string, StateValue> { ["Start"] = StateValue.Of(3).OrAbsent() } },
            },
        };
        var state = CatalogDiscovery.DetectState(setting, (p, v) => null, new FakeCtx()); // absent
        Assert.Equal("Manual", state);
    }

    [Fact]
    public void No_match_is_custom_null()
    {
        var setting = new Setting
        {
            Id = "s", Name = "s", Description = "s",
            Targets = new[] { Reg("K", "V") },
            States = new[]
            {
                new SettingState { Label = "On", Set = new Dictionary<string, StateValue> { ["K"] = StateValue.Of(1) } },
            },
        };
        Assert.Null(CatalogDiscovery.DetectState(setting, (p, v) => 99, new FakeCtx()));
    }

    [Fact]
    public void Custom_detector_takes_over()
    {
        var setting = new Setting
        {
            Id = "s", Name = "s", Description = "s",
            Detector = new FixedDetector("Show all"),
            // targets/states are ignored when a detector is present
        };
        Assert.Equal("Show all", CatalogDiscovery.DetectState(setting, (p, v) => null, new FakeCtx()));
    }
}

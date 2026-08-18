using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

public class BuildContextDetectionTests
{
    private static readonly string[] HklmK = [@"HKLM\K"];

    // A setting with two build-gated targets on the same key: a HiddenByDefault value-toggle on Win11,
    // a key-existence toggle on Win10. Mirrors the merged ThisPC-folder shape.
    private static Setting TwoMechanismSetting() => new()
    {
        Id = "x",
        Display = new() { Name = "X", Description = "X" },
        Targets = new Target[]
        {
            new RegTarget("value", HklmK, "HiddenByDefault", RegistryValueKind.DWord) { AppliesTo = new[] { BuildRange.Windows11 } },
            new RegTarget("exists", HklmK, null, RegistryValueKind.None) { AppliesTo = new[] { BuildRange.Windows10 } },
        },
        States = new[]
        {
            new SettingState { Label = "Enabled",  Set = new Dictionary<string, StateValue> { ["value"] = StateValue.Of(0), ["exists"] = StateValue.Exists } },
            new SettingState { Label = "Disabled", IsFallback = true, Set = new Dictionary<string, StateValue> { ["value"] = StateValue.Of(1), ["exists"] = StateValue.Absent } },
        },
    };

    private sealed class Ctx : IDetectionContext
    {
        public WinBuild CurrentBuild { get; init; }
        public object? Value;
        public bool KeyPresent;
        public object? GetValue(string keyPath, string? valueName) => Value;
        public string[] GetSubKeyNames(string keyPath) => Array.Empty<string>();
        public bool KeyExists(string keyPath) => KeyPresent;
        public string? PrimaryDnsV4OfActiveAdapter() => null;
        public bool IsSystemRestoreEnabled() => false;
        public bool? ScheduledTaskEnabled(string taskPath) => null;
        public int? PowerCfgValue(string subgroupGuid, string settingGuid, PowerContext context) => null;
        public string? ActivePowerPlanGuid() => null;
    }

    [Fact]
    public void Win11_uses_the_value_target()
    {
        var s = TwoMechanismSetting();
        Assert.Equal("Enabled",  CatalogDiscovery.Detect(s, new Ctx { CurrentBuild = new(22631), Value = 0, KeyPresent = false }).Label);
        Assert.Equal("Disabled", CatalogDiscovery.Detect(s, new Ctx { CurrentBuild = new(22631), Value = 1, KeyPresent = false }).Label);
    }

    [Fact]
    public void Win10_uses_the_key_existence_target()
    {
        var s = TwoMechanismSetting();
        Assert.Equal("Enabled",  CatalogDiscovery.Detect(s, new Ctx { CurrentBuild = new(19045), Value = null, KeyPresent = true }).Label);
        Assert.Equal("Disabled", CatalogDiscovery.Detect(s, new Ctx { CurrentBuild = new(19045), Value = null, KeyPresent = false }).Label);
    }

    [Fact]
    public void Apply_emits_only_the_build_active_target()
    {
        var s = TwoMechanismSetting();
        var win11 = ApplyPlanBuilder.Build(s, "Enabled", new WinBuild(22631));
        Assert.Single(win11); // only the value write, not the key-existence op
    }
}

using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

public class CatalogDiscoveryTests
{
    private static readonly string[] TestPaths = [@"HKEY_LOCAL_MACHINE\TEST"];

    private sealed class FakeCtx : IDetectionContext
    {
        public WinBuild CurrentBuild => new(int.MaxValue);
        private readonly Func<string, string?, object?> _get;
        private readonly bool? _taskEnabled;
        public FakeCtx(Func<string, string?, object?>? get = null, bool? taskEnabled = null)
        {
            _get = get ?? ((_, _) => null);
            _taskEnabled = taskEnabled;
        }
        public object? GetValue(string keyPath, string? valueName) => _get(keyPath, valueName);
        public bool KeyExists(string keyPath) => false;
        public string[] GetSubKeyNames(string keyPath) => Array.Empty<string>();
        public string? PrimaryDnsV4OfActiveAdapter() => null;
        public IReadOnlyList<string> DnsV4ServersOfActiveAdapter() => Array.Empty<string>();
        public bool IsSystemRestoreEnabled() => false;
        public bool? ScheduledTaskEnabled(string taskPath) => _taskEnabled;
        public int? PowerCfgValue(string subgroupGuid, string settingGuid, PowerContext context) => null;
        public string? ActivePowerPlanGuid() => null;
    }

    private sealed class FixedDetector : IStateDetector
    {
        private readonly string? _r;
        public FixedDetector(string? r) => _r = r;
        public string? Detect(Setting setting, IDetectionContext context) => _r;
    }

    private static RegTarget Reg(string key, string valueName) =>
        new(key, TestPaths, valueName, RegistryValueKind.DWord);

    [Fact]
    public void Resolves_state_from_registry_reads()
    {
        var setting = new Setting
        {
            Id = "s", Display = new() { Name = "s", Description = "s" },
            Targets = new[] { Reg("Mode", "SearchboxTaskbarMode") },
            States = new[]
            {
                new SettingState { Label = "Hide", Set = new Dictionary<string, StateValue> { ["Mode"] = StateValue.Of(0) } },
                new SettingState { Label = "Box",  Set = new Dictionary<string, StateValue> { ["Mode"] = StateValue.Of(2) } },
            },
        };
        var state = CatalogDiscovery.Detect(setting, new FakeCtx((p, v) => 2)).Label;
        Assert.Equal("Box", state);
    }

    [Fact]
    public void Absent_read_resolves_via_or_absent_state()
    {
        var setting = new Setting
        {
            Id = "s", Display = new() { Name = "s", Description = "s" },
            Targets = new[] { Reg("Start", "Start") },
            States = new[]
            {
                new SettingState { Label = "Manual", Set = new Dictionary<string, StateValue> { ["Start"] = StateValue.Of(3).OrAbsent() } },
            },
        };
        var state = CatalogDiscovery.Detect(setting, new FakeCtx((p, v) => null)).Label;
        Assert.Equal("Manual", state);
    }

    [Fact]
    public void No_match_is_custom_null()
    {
        var setting = new Setting
        {
            Id = "s", Display = new() { Name = "s", Description = "s" },
            Targets = new[] { Reg("K", "V") },
            States = new[]
            {
                new SettingState { Label = "On", Set = new Dictionary<string, StateValue> { ["K"] = StateValue.Of(1) } },
            },
        };
        Assert.Null(CatalogDiscovery.Detect(setting, new FakeCtx((p, v) => 99)).Label);
    }

    [Fact]
    public void Resolves_scheduled_task_state_from_context()
    {
        var setting = new Setting
        {
            Id = "s", Display = new() { Name = "s", Description = "s" },
            Targets = new[] { new TaskTarget("Task", @"\MS\Task") },
            States = new[]
            {
                new SettingState { Label = "Enabled", Set = new Dictionary<string, StateValue> { ["Task"] = StateValue.Of(true) } },
                new SettingState { Label = "Disabled", Set = new Dictionary<string, StateValue> { ["Task"] = StateValue.Of(false) }, IsFallback = true },
            },
        };

        Assert.Equal("Enabled", CatalogDiscovery.Detect(setting, new FakeCtx(taskEnabled: true)).Label);
        Assert.Equal("Disabled", CatalogDiscovery.Detect(setting, new FakeCtx(taskEnabled: false)).Label);
        // An absent task is not present, so nothing matches and the engine falls back to Disabled. (The
        // harness never reaches this path - it treats an absent task as Unavailable before calling the engine.)
        Assert.Equal("Disabled", CatalogDiscovery.Detect(setting, new FakeCtx(taskEnabled: null)).Label);
    }

    [Fact]
    public void Custom_detector_takes_over()
    {
        var setting = new Setting
        {
            Id = "s", Display = new() { Name = "s", Description = "s" },
            Detector = new FixedDetector("Show all"),
            // targets/states are ignored when a detector is present
        };
        Assert.Equal("Show all", CatalogDiscovery.Detect(setting, new FakeCtx()).Label);
    }
}

using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Catalog;
using Xunit;
using Winhance.TestSupport;

namespace Winhance.Core.Tests.Catalog;

public class SystemRestoreDetectorTests
{
    private sealed class FakeCtx : IDetectionContext
    {
        public WinBuild CurrentBuild => new(int.MaxValue);
        public bool RestoreEnabled;
        public object? GetValue(string keyPath, string? valueName) => null;
        public string[] GetSubKeyNames(string keyPath) => System.Array.Empty<string>();
        public bool KeyExists(string keyPath) => false;
        public bool? ScheduledTaskEnabled(string taskPath) => null;
        public IReadOnlyList<DnsServer> DnsServersOfActiveAdapter() => System.Array.Empty<DnsServer>();
        public bool IsSystemRestoreEnabled() => RestoreEnabled;
        public int? PowerCfgValue(string subgroupGuid, string settingGuid, PowerContext context) => null;
        public string? ActivePowerPlanGuid() => null;
    }

    private static readonly Setting Dummy = new() { Id = "d", Display = new() { Name = TestKeys.Of("d"), Description = TestKeys.Of("d") } };

    private static readonly SystemRestoreDetector Restore = new(TestKeys.Of("On"), TestKeys.Of("Off"));

    [Fact]
    public void Restore_enabled_resolves_to_on()
        => Assert.Equal("On", Restore.Detect(Dummy, new FakeCtx { RestoreEnabled = true }));

    [Fact]
    public void Restore_disabled_resolves_to_off()
        => Assert.Equal("Off", Restore.Detect(Dummy, new FakeCtx { RestoreEnabled = false }));
}

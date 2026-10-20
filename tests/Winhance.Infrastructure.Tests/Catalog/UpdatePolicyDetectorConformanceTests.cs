using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Localization;
using Winhance.Core.Features.Common.Catalog;
using Xunit;

namespace Winhance.Infrastructure.Tests.Catalog;

// Registry alone cannot read this setting (Disabled/Paused share NoAutoUpdate=1/AUOptions=1 and Disabled is a
// filesystem DLL rename), which is why the detector - not target matching - is the authority. Uses the REAL
// catalog setting's attached detector.
public class UpdatePolicyDetectorConformanceTests
{
    private const string Ux = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings";

    private static readonly LocKey DefaultLabel = LocKey.Setting.UpdatesPolicyMode.Option0;
    private static readonly LocKey DeferLabel = LocKey.Setting.UpdatesPolicyMode.Option1;
    private static readonly LocKey PausedLabel = LocKey.Setting.UpdatesPolicyMode.Option2;
    private static readonly LocKey DisabledLabel = LocKey.Setting.UpdatesPolicyMode.Option3;

    private static readonly Setting UpdatePolicy =
        SettingCatalog.All.First(s => s.Id == "updates-policy-mode");

    private sealed class Ctx : IDetectionContext
    {
        private readonly Dictionary<(string, string?), object?> _vals;
        private readonly bool _dllsRenamed;
        public Ctx(bool dllsRenamed = false, Dictionary<(string, string?), object?>? vals = null)
        {
            _dllsRenamed = dllsRenamed;
            _vals = vals ?? new Dictionary<(string, string?), object?>();
        }
        public WinBuild CurrentBuild => new(int.MaxValue);
        public object? GetValue(string keyPath, string? valueName)
            => _vals.TryGetValue((keyPath, valueName), out var v) ? v : null;
        public string[] GetSubKeyNames(string keyPath) => Array.Empty<string>();
        public bool KeyExists(string keyPath) => false;
        public string? PrimaryDnsV4OfActiveAdapter() => null;
        public IReadOnlyList<string> DnsV4ServersOfActiveAdapter() => Array.Empty<string>();
        public bool IsSystemRestoreEnabled() => false;
        public bool? ScheduledTaskEnabled(string taskPath) => null;
        public int? PowerCfgValue(string subgroupGuid, string settingGuid, PowerContext context) => null;
        public string? ActivePowerPlanGuid() => null;
        public bool CriticalUpdateDllsRenamed() => _dllsRenamed;
    }

    private static string? Detect(Ctx ctx) => UpdatePolicy.Detector!.Detect(UpdatePolicy, ctx);

    private static Ctx WithValues(bool dllsRenamed, params (string Value, object? Data)[] reads)
        => new(dllsRenamed, reads.ToDictionary(r => (Ux, (string?)r.Value), r => r.Data));

    [Fact]
    public void Clean_machine_reads_the_windows_default()
        => Assert.Equal(DefaultLabel.Value, Detect(new Ctx()));

    [Fact]
    public void DeferFeatureUpdates_reads_the_deferred_state()
        => Assert.Equal(DeferLabel.Value, Detect(WithValues(false, ("DeferFeatureUpdates", 1))));

    [Fact]
    public void DeferFeatureUpdates_not_one_is_not_deferred()
        => Assert.Equal(DefaultLabel.Value, Detect(WithValues(false, ("DeferFeatureUpdates", 0))));

    [Theory]
    [InlineData("PauseUpdatesStartTime")]
    [InlineData("PauseUpdatesExpiryTime")]
    [InlineData("PausedQualityDate")]
    [InlineData("PausedFeatureDate")]
    public void Any_pause_marker_reads_paused(string valueName)
        => Assert.Equal(PausedLabel.Value, Detect(WithValues(false, (valueName, "2025-01-01T00:00:00Z"))));

    [Fact]
    public void Renamed_dlls_read_disabled()
        => Assert.Equal(DisabledLabel.Value, Detect(new Ctx(dllsRenamed: true)));

    [Fact]
    public void Disabled_outranks_paused_and_defer()
        => Assert.Equal(DisabledLabel.Value, Detect(WithValues(true,
            ("PauseUpdatesStartTime", "2025-01-01T00:00:00Z"), ("DeferFeatureUpdates", 1))));

    [Fact]
    public void Paused_outranks_defer()
        => Assert.Equal(PausedLabel.Value, Detect(WithValues(false,
            ("PauseUpdatesStartTime", "2025-01-01T00:00:00Z"), ("DeferFeatureUpdates", 1))));

    [Fact]
    public void Detector_is_wired_and_its_labels_match_the_catalog_states()
    {
        Assert.NotNull(UpdatePolicy.Detector);
        Assert.IsType<UpdatePolicyDetector>(UpdatePolicy.Detector);
        var stateLabels = UpdatePolicy.States.Select(s => s.Label.Value).ToHashSet(StringComparer.Ordinal);
        foreach (var label in new[] { DefaultLabel, DeferLabel, PausedLabel, DisabledLabel })
            Assert.Contains(label.Value, stateLabels);
    }

    // UpdateService applies this through the synchronous ApplyExecutor and cannot await, so a
    // process-launching effect added here would be split off the plan and never run.
    [Fact]
    public void No_state_carries_an_effect_the_synchronous_apply_path_cannot_run()
        => Assert.DoesNotContain(UpdatePolicy.States.SelectMany(s => s.Effects), e => e.IsAsyncIo);
}

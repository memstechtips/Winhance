using System;
using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Catalog;
using Xunit;

namespace Winhance.Infrastructure.Tests.Migration;

/// <summary>Machine-INDEPENDENT conformance for <see cref="UpdatePolicyDetector"/> (Phase 6.9 Slice 6): over
/// CONSTRUCTED contexts it asserts the detector resolves the update-policy state exactly as the old
/// <c>UpdateService.GetCurrentUpdatePolicyIndexAsync</c> did (renamed DLLs -> Disabled; a live pause -> Paused;
/// DeferFeatureUpdates==1 -> the deferred state; else the Windows default), including the precedence order. Uses the
/// REAL catalog setting's attached detector, so it also proves the wiring (Detector present, labels match the States).
/// Registry alone cannot read this setting (Disabled/Paused share NoAutoUpdate=1/AUOptions=1 and Disabled is a
/// filesystem DLL rename), which is why the detector - not target matching - is the authority.</summary>
public class UpdatePolicyDetectorConformanceTests
{
    private const string Ux = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings";

    private const string DefaultLabel = "Normal (Windows Default)";
    private const string DeferLabel = "Security Updates Only (Recommended)";
    private const string PausedLabel = "Paused for a long time (Unpause in Settings)";
    private const string DisabledLabel = "Disabled (NOT Recommended, Security Risk)";

    private static readonly Setting UpdatePolicy =
        SettingCatalog.All.First(s => s.Id == "updates-policy-mode");

    /// <summary>Constructed detection context: only the (keyPath, valueName) pairs supplied read as present; the DLL
    /// rename flag is explicit. Everything else is absent/false.</summary>
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
        => Assert.Equal(DefaultLabel, Detect(new Ctx()));

    [Fact]
    public void DeferFeatureUpdates_reads_the_deferred_state()
        => Assert.Equal(DeferLabel, Detect(WithValues(false, ("DeferFeatureUpdates", 1))));

    [Fact]
    public void DeferFeatureUpdates_not_one_is_not_deferred()
        => Assert.Equal(DefaultLabel, Detect(WithValues(false, ("DeferFeatureUpdates", 0))));

    [Theory]
    [InlineData("PauseUpdatesStartTime")]
    [InlineData("PauseUpdatesExpiryTime")]
    [InlineData("PausedQualityDate")]
    [InlineData("PausedFeatureDate")]
    public void Any_pause_marker_reads_paused(string valueName)
        => Assert.Equal(PausedLabel, Detect(WithValues(false, (valueName, "2025-01-01T00:00:00Z"))));

    [Fact]
    public void Renamed_dlls_read_disabled()
        => Assert.Equal(DisabledLabel, Detect(new Ctx(dllsRenamed: true)));

    [Fact]
    public void Disabled_outranks_paused_and_defer()
        => Assert.Equal(DisabledLabel, Detect(WithValues(true,
            ("PauseUpdatesStartTime", "2025-01-01T00:00:00Z"), ("DeferFeatureUpdates", 1))));

    [Fact]
    public void Paused_outranks_defer()
        => Assert.Equal(PausedLabel, Detect(WithValues(false,
            ("PauseUpdatesStartTime", "2025-01-01T00:00:00Z"), ("DeferFeatureUpdates", 1))));

    [Fact]
    public void Detector_is_wired_and_its_labels_match_the_catalog_states()
    {
        Assert.NotNull(UpdatePolicy.Detector);
        var detector = Assert.IsType<UpdatePolicyDetector>(UpdatePolicy.Detector);
        var stateLabels = UpdatePolicy.States.Select(s => s.Label).ToHashSet(StringComparer.Ordinal);
        foreach (var label in new[] { detector.DefaultLabel, detector.DeferLabel, detector.PausedLabel, detector.DisabledLabel })
            Assert.Contains(label, stateLabels);
    }
}

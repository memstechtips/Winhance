using Microsoft.Win32;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Catalog;

// Pins ResolveRawValuesToIndex against the catalog alone: ROUND-TRIP (a state's own WritePayload resolves back
// to that state's index - detection inverts apply); DETECTOR selections resolve to 0; a DetectedIndex reading
// short-circuits; FALLBACK: an all-absent reading or an IsFallback state resolves to the WindowsDefault option,
// a present-but-unmatched reading to Custom.
public class ComboBoxResolverSettingConformanceTests
{
    private static readonly string[] TestPaths = [@"HKEY_LOCAL_MACHINE\SOFTWARE\Winhance\Test"];

    // A ComboBoxResolver needs a version service for the build-aware WindowsDefault fallback. These conformance
    // facts exercise unconditional-default selections, so any build works; stub a Windows 11 build.
    private static IWindowsVersionService StubVersion()
    {
        var m = new Mock<IWindowsVersionService>();
        m.Setup(v => v.GetWindowsBuildNumber()).Returns(22631);
        m.Setup(v => v.GetWindowsBuildRevision()).Returns(0);
        return m.Object;
    }

    // A PowerCfgTarget's Set key re-keys to "PowerCfgValue" in the live readings; a RegTarget's key IS the reading
    // key. Mirrors ComboBoxResolver.ReadKeyForTarget (private) - the read-key mapping, not the resolver's logic.
    private static string ReadKeyForTarget(Setting setting, string targetKey) =>
        setting.Targets.FirstOrDefault(t => t.Key == targetKey) is PowerCfgTarget ? "PowerCfgValue" : targetKey;

    // The canonical live reading for one of a state's Set entries: the value apply writes (WritePayload). A pure
    // Absent entry (delete-on-apply, no payload) means the key is ABSENT (omitted); an Exists entry (any value
    // present) gets a present sentinel.
    private static Dictionary<string, object?> CanonicalReads(Setting setting, SettingState state)
    {
        var reads = new Dictionary<string, object?>();
        foreach (var entry in state.Set)
        {
            var key = ReadKeyForTarget(setting, entry.Key);
            var sv = entry.Value;
            if (sv.WritePayload != null)
                reads[key] = sv.WritePayload;
            else if (sv.AcceptsAnyPresent)
                reads[key] = 1; // Exists: any present value satisfies it
            // else Absent: key omitted (absent from the reads)
        }
        return reads;
    }

    [Fact]
    public void Canonical_state_reads_round_trip_to_that_state_index()
    {
        var resolver = new ComboBoxResolver(StubVersion());
        var selections = SettingCatalog.All.Where(s => s.Control == ControlKind.Selection).ToList();

        var mismatches = new List<string>();
        int comparedStates = 0;
        int nonTrivial = 0; // resolved to a non-zero index -> not an all-0 vacuous pass

        foreach (var setting in selections)
        {
            var states = setting.States;
            // Detector selections (all states empty Set) have no value-match; covered by the separate fact.
            if (states.All(s => s.Set.Count == 0))
                continue;

            for (int i = 0; i < states.Count; i++)
            {
                if (states[i].Set.Count == 0)
                    continue; // an option with no ValueMappings never value-matched (the Count > 0 guard)
                var reads = CanonicalReads(setting, states[i]);
                if (reads.Count == 0)
                    continue; // a pure delete/absent state - its all-absent reads hit the fallback edge, not value-match
                int resolved = resolver.ResolveRawValuesToIndex(setting, new Dictionary<string, object?>(reads));
                comparedStates++;
                if (resolved != 0)
                    nonTrivial++;
                if (resolved != i)
                    mismatches.Add($"{setting.Id}[{i}] '{states[i].Label}': reads={Fmt(reads)} resolved={resolved}");
            }
        }

        Assert.True(comparedStates > 30, $"only {comparedStates} selection states compared - population scoping bug");
        Assert.True(nonTrivial > 0, "every state resolved to index 0 - the round-trip would pass vacuously");
        Assert.True(
            mismatches.Count == 0,
            $"{mismatches.Count} of {comparedStates} selection states did NOT round-trip to their own index:\n"
                + string.Join("\n", mismatches));
    }

    [Fact]
    public void Detector_selections_with_empty_set_states_resolve_to_index_zero()
    {
        var resolver = new ComboBoxResolver(StubVersion());
        var detectorSelections = SettingCatalog.All
            .Where(s => s.Control == ControlKind.Selection && s.States.Count > 0 && s.States.All(st => st.Set.Count == 0))
            .ToList();

        Assert.NotEmpty(detectorSelections); // the DNS server + system-tray custom-detector selections exist

        foreach (var setting in detectorSelections)
        {
            // No value-match to run -> index 0, regardless of the (non-DetectedIndex) reads.
            Assert.Equal(0, resolver.ResolveRawValuesToIndex(setting, new Dictionary<string, object?>()));
            Assert.Equal(0, resolver.ResolveRawValuesToIndex(setting,
                new Dictionary<string, object?> { ["UnrelatedKey"] = 7 }));
        }
    }

    [Fact]
    public void DetectedIndex_reading_short_circuits_to_that_index()
    {
        var resolver = new ComboBoxResolver(StubVersion());
        var anySelection = SettingCatalog.All.First(s => s.Control == ControlKind.Selection);
        // A custom-detector DetectedIndex overrides value-matching for any setting.
        Assert.Equal(3, resolver.ResolveRawValuesToIndex(anySelection,
            new Dictionary<string, object?> { ["DetectedIndex"] = 3 }));
    }

    [Fact]
    public void All_backing_absent_resolves_to_the_windows_default_option()
    {
        var resolver = new ComboBoxResolver(StubVersion());
        // A synthetic 3-option single-key registry selection (Control derives to Selection): options A/B/C write
        // Val=1/2/3, option C is the WindowsDefault, and no state uses OrAbsent -> an all-absent reading hits the
        // allBackingValuesAbsent fallback (ComboBoxResolver.cs:194-245) and resolves to the WindowsDefault index.
        var setting = MakeRegSelection(windowsDefaultIndex: 2);

        // Every backing key absent -> the WindowsDefault option (index 2), NOT Custom.
        Assert.Equal(2, resolver.ResolveRawValuesToIndex(setting, new Dictionary<string, object?>()));

        // A present-but-unmatched reading is NOT all-absent and there is no IsFallback state, so the fallback
        // branch is skipped -> Custom, even though the setting HAS a WindowsDefault (ComboBoxResolver.cs:241-248).
        Assert.Equal(
            ComboBoxConstants.CustomStateIndex,
            resolver.ResolveRawValuesToIndex(setting, new Dictionary<string, object?> { ["Val"] = 999 }));
    }

    [Fact]
    public void An_IsFallback_state_catches_an_unmatched_reading_as_the_windows_default()
    {
        var resolver = new ComboBoxResolver(StubVersion());
        // Same shape, but option C is ALSO the IsFallback catch-all -> even a present, unmatched reading resolves
        // to it (ComboBoxResolver.cs:241).
        var setting = MakeRegSelection(windowsDefaultIndex: 2, fallbackIndex: 2);
        Assert.Equal(2, resolver.ResolveRawValuesToIndex(setting, new Dictionary<string, object?> { ["Val"] = 999 }));
    }

    // A synthetic 3-option single-key registry selection. Options A/B/C write Val=1/2/3; the option at
    // windowsDefaultIndex carries the WindowsDefault role, and the one at fallbackIndex (if any) is the IsFallback
    // catch-all. Three non-Enabled/Disabled states with no Numeric/OptionSource -> Control derives to Selection.
    private static Setting MakeRegSelection(int windowsDefaultIndex, int? fallbackIndex = null)
    {
        SettingState State(string label, int val, int idx) => new()
        {
            Label = label,
            Roles = idx == windowsDefaultIndex
                ? new[] { StateRole.WindowsDefault }
                : System.Array.Empty<StateRole>(),
            IsFallback = idx == fallbackIndex,
            Set = new Dictionary<string, StateValue> { ["Val"] = StateValue.Of(val) },
        };
        return new Setting
        {
            Id = "syn-reg-selection",
            Display = new() { Name = "Synthetic", Description = "Synthetic registry selection" },
            Targets = new Target[]
            {
                new RegTarget("Val", TestPaths, "Val", RegistryValueKind.DWord),
            },
            States = new[] { State("A", 1, 0), State("B", 2, 1), State("C", 3, 2) },
        };
    }

    private static string Fmt(IReadOnlyDictionary<string, object?> d) =>
        "{" + string.Join(", ", d.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value ?? "null"}")) + "}";
}

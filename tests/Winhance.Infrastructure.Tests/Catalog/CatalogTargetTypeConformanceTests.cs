using System.Text.Json;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Xunit;
using Xunit.Abstractions;
using Winhance.TestSupport;

namespace Winhance.Infrastructure.Tests.Catalog;

/// <summary>
/// Conformance: every <see cref="RegTarget.Type"/> the catalog declares must match the type Windows
/// actually stores that value under, on every clean-install probe capture we hold.
///
/// This exists because a wrong declared type is silently destructive rather than merely wrong. A target
/// with a surgical shape (BitMask / ByteOnly / StringFlagMask / CompositeStringKey) needs a specific CLR
/// type to reduce at all: when the stored type does not match, detection cannot place the setting on any
/// state, and before the recover-or-refuse fix the surgical writers would overwrite the whole value with a
/// zeroed array. So the declared type is load-bearing, and nothing else in the suite checks it - the
/// clean-install conformance test compares detected STATE, which a type mismatch makes unresolvable rather
/// than wrong, so it lands in a different bucket.
///
/// Provenance of the assertion: run offline against all five probe captures on 2026-07-27 (Win10 19045 +
/// Win11 26200 x4) - 727 present-value comparisons, 0 mismatches. This test pins that result against the
/// three committed fixtures so a future catalog edit cannot quietly contradict reality.
///
/// Scope: only targets the fixture observed PRESENT. A value absent on a clean install is one Winhance
/// itself creates, so Winhance chooses its type and it is self-consistent by construction - there is no
/// external truth to conform to, and asserting one would be inventing evidence.
///
/// Run: winhance-harness CatalogTargetTypeConformanceTests
/// </summary>
public class CatalogTargetTypeConformanceTests
{
    private readonly ITestOutputHelper _output;

    public CatalogTargetTypeConformanceTests(ITestOutputHelper output) => _output = output;

    /// <summary>Declared-vs-observed pairs that are known-good despite differing. Windows stores some values
    /// as REG_EXPAND_SZ where the catalog declares REG_SZ; both read back as a string, so every string-shaped
    /// reduction behaves identically and neither detection nor apply is affected. Empty of anything else - if
    /// a new pair appears here it needs a written reason, not a shrug.</summary>
    private static readonly HashSet<(RegistryValueKind Declared, RegistryValueKind Observed)> BenignPairs = new()
    {
        (RegistryValueKind.String, RegistryValueKind.ExpandString),
        (RegistryValueKind.ExpandString, RegistryValueKind.String),
    };

    private static readonly string[] Fixtures =
    {
        "cleaninstall-win10-22h2-pro-vm.json",
        "cleaninstall-win11-25h2-gold-laptop.json",
        "cleaninstall-win11-25h2-post-update-vm.json",
    };

    [Fact]
    public void Declared_registry_types_match_what_clean_installs_actually_store()
    {
        var declared = new Dictionary<(string SettingId, string TargetKey), RegTarget>();
        foreach (var setting in SettingCatalog.All)
        {
            foreach (var reg in setting.Targets.OfType<RegTarget>())
                declared[(setting.Id, reg.Key)] = reg;
        }

        var mismatches = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var covered = new HashSet<(string, string)>();
        int compared = 0;

        foreach (var fixtureName in Fixtures)
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(FixturePath(fixtureName)));
            foreach (var settingNode in doc.RootElement.GetProperty("settings").EnumerateArray())
            {
                string settingId = settingNode.GetProperty("id").GetString()!;
                if (!settingNode.TryGetProperty("targets", out var targets))
                    continue;

                foreach (var target in targets.EnumerateArray())
                {
                    if (!target.TryGetProperty("kind", out var kind) || kind.GetString() != "Registry")
                        continue;
                    if (!target.TryGetProperty("valueKind", out var kindNode))
                        continue;

                    // "None" is the probe's marker for "value absent", which carries no type to compare.
                    string? observedName = kindNode.GetString();
                    if (string.IsNullOrEmpty(observedName) || observedName == "None")
                        continue;
                    if (!Enum.TryParse<RegistryValueKind>(observedName, out var observed))
                        continue;

                    string targetKey = target.GetProperty("key").GetString()!;
                    if (!declared.TryGetValue((settingId, targetKey), out var reg))
                        continue; // catalog moved on past this capture - covered by the clean-install test

                    compared++;
                    covered.Add((settingId, targetKey));

                    if (reg.Type == observed || BenignPairs.Contains((reg.Type, observed)))
                        continue;

                    string shape = DescribeShape(reg);
                    mismatches[$"{settingId}/{targetKey} [{fixtureName}]"] =
                        $"catalog declares {reg.Type} but the machine stores {observed}{shape}";
                }
            }
        }

        _output.WriteLine($"compared {compared} present-value readings across {Fixtures.Length} fixtures "
            + $"({covered.Count} distinct targets, of {declared.Count} declared).");
        foreach (var kv in mismatches)
            _output.WriteLine($"  mismatch: {kv.Key} - {kv.Value}");

        // Non-vacuity: a fixture-loading or key-shape regression would silently compare nothing and pass.
        Assert.True(compared >= 100,
            $"only {compared} type comparisons ran - fixture or key-matching regression, not a clean result.");

        Assert.True(mismatches.Count == 0,
            "Catalog registry types contradict what a clean Windows install stores. A wrong type on a "
            + "surgical target (bit/byte/flag/composite) makes the setting undetectable and its writes "
            + "unsafe:\n" + string.Join("\n", mismatches.Select(kv => $"  {kv.Key}: {kv.Value}")));
    }

    /// <summary>Names the surgical shape, if any, so a failure says outright how much damage a wrong type
    /// would do rather than leaving the reader to look it up.</summary>
    private static string DescribeShape(RegTarget reg)
    {
        if (reg.BitMask is not null) return " (SURGICAL: bitmask - unreadable and unsafe to write)";
        if (reg.ByteOnly) return " (SURGICAL: single byte - unreadable and unsafe to write)";
        if (reg.StringFlagMask is not null) return " (SURGICAL: decimal-string flags)";
        if (reg.CompositeStringKey is not null) return " (SURGICAL: packed composite string)";
        return " (plain value - detection is numeric-lenient, so this is a documentation defect)";
    }

    private static string FixturePath(string name)
        => Path.Combine(SolutionDir(), "tests", "Winhance.Infrastructure.Tests", "Catalog", "Fixtures", name);

    // Anchors on the compile-time source path (CatalogCleanInstallConformanceTests precedent) so fixtures
    // resolve from the repo even when the build output is redirected off the network share.
    private static string SolutionDir() => RepoPaths.SolutionDir();
}

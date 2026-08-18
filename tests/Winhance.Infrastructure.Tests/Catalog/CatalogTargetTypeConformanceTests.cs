using System.Text.Json;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Xunit;
using Xunit.Abstractions;
using Winhance.TestSupport;

namespace Winhance.Infrastructure.Tests.Catalog;

// A wrong declared type is silently destructive: a surgical shape (BitMask / ByteOnly / StringFlagMask /
// CompositeStringKey) needs a specific CLR type to reduce, and before the recover-or-refuse fix the writers would
// overwrite the whole value with a zeroed array. Only targets the fixture observed PRESENT are checked - a value
// absent on a clean install is one Winhance creates, so it chooses its type. Run: winhance-harness CatalogTargetTypeConformanceTests
public class CatalogTargetTypeConformanceTests
{
    private readonly ITestOutputHelper _output;

    public CatalogTargetTypeConformanceTests(ITestOutputHelper output) => _output = output;

    // Windows stores some values as REG_EXPAND_SZ where the catalog declares REG_SZ; both read back as string, so no
    // reduction is affected. A new pair here needs a written reason.
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

    // So a failure says outright how much damage a wrong type would do.
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

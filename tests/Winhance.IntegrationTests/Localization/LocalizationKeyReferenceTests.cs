using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Localization;
using Winhance.IntegrationTests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Winhance.IntegrationTests.Localization;

// Closes the "code <-> key" gap: LocalizationJsonValidityTests only validate per-language files against
// en.json. (a) literal GetString("...") keys have NO fallback at the call site, so a miss renders [Key] - HARD
// failure. (b) computed catalog keys resolve through GetStringOrFallback, so a miss silently falls back to the
// English baked into the catalog; only Name and Description are hard-asserted (100% present), the rest are
// REPORTED. (c) dead-key report, soft.
public class LocalizationKeyReferenceTests
{
    private readonly ITestOutputHelper _output;

    public LocalizationKeyReferenceTests(ITestOutputHelper output) => _output = output;

    private static readonly string SrcDir = Path.Combine(TestContext.SolutionDir, "src");

    private static readonly string EnJsonPath = Path.Combine(
        TestContext.SolutionDir, "src", "Winhance.UI", "Features", "Common", "Localization", "en.json");

    // Matches a localization key passed as the FIRST argument to any GetString(...) call, on any
    // receiver (_localization.GetString("X"), localizationService.GetString("X", arg), etc.).
    // Only pure string literals are captured; dynamically-built keys (GetString(someVar),
    // GetString($"Setting_{id}")) are intentionally skipped here — those that come from the settings
    // catalog are covered by check (b).
    private static readonly Regex GetStringLiteral = new(
        @"GetString\(\s*""([^""]+)""", RegexOptions.Compiled);

    private static HashSet<string> EnglishKeys()
    {
        var json = File.ReadAllText(EnJsonPath);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.EnumerateObject().Select(p => p.Name).ToHashSet();
    }

    private static IReadOnlyList<Setting> AllSettings() => SettingCatalog.All;

    private static IEnumerable<string> AllCsFiles() =>
        Directory.EnumerateFiles(SrcDir, "*.cs", SearchOption.AllDirectories);

    [Fact]
    public void StaticLiteralLocalizationKeys_MustExistInEnglish()
    {
        var enKeys = EnglishKeys();

        // key -> set of files referencing it (for actionable failure output)
        var references = new Dictionary<string, SortedSet<string>>();

        foreach (var file in AllCsFiles())
        {
            var text = File.ReadAllText(file);
            foreach (Match m in GetStringLiteral.Matches(text))
            {
                var key = m.Groups[1].Value;
                if (!references.TryGetValue(key, out var files))
                {
                    files = new SortedSet<string>();
                    references[key] = files;
                }
                files.Add(Path.GetRelativePath(SrcDir, file));
            }
        }

        var missing = references
            .Where(kvp => !enKeys.Contains(kvp.Key))
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => $"  \"{kvp.Key}\"  <- {string.Join(", ", kvp.Value)}")
            .ToList();

        missing.Should().BeEmpty(because:
            "every localization key passed as a literal to GetString(\"...\") has no call-site " +
            "fallback — a missing key renders the raw \"[Key]\" string in the UI. " +
            "Add these keys to en.json (and the other language files), or fix the typo'd call site:\n" +
            string.Join("\n", missing));
    }

    // Only Name and Description are hard-asserted; the other computed keys are intentionally absent in bulk (3 of
    // the many ComboBox settings define an _Option_Custom key; the 130+ DNS-server option names fall back), so
    // hard-asserting them would fail en masse on intentional fallbacks.
    [Fact]
    public void ComputedCatalogKeys_NameAndDescription_MustExist()
    {
        var enKeys = EnglishKeys();
        var settings = AllSettings();
        settings.Should().NotBeEmpty(because: "the settings catalog must enumerate at least one feature group");

        var missing = new List<string>();
        foreach (var setting in settings)
        {
            var name = SettingLocalizationKeys.Name(setting);
            var desc = SettingLocalizationKeys.Description(setting);
            if (!enKeys.Contains(name)) missing.Add($"  {name}  (setting Id: {setting.Id})");
            if (!enKeys.Contains(desc)) missing.Add($"  {desc}  (setting Id: {setting.Id})");
        }

        missing.Should().BeEmpty(because:
            "every shipped setting's Name and Description key must exist in en.json:\n" +
            string.Join("\n", missing));
    }

    // Group keys use the any-of rule: compact OR snake OR the cross-group-info ("space -> underscore") variant.
    [Fact]
    public void ComputedCatalogKeys_CoverageReport()
    {
        var enKeys = EnglishKeys();
        var settings = AllSettings();

        var groupNames = settings
            .Where(s => s.Display.GroupName != null)
            .Select(s => s.Display.GroupName!)
            .Distinct()
            .ToList();

        var coveredGroups = 0;
        var uncoveredGroups = new List<string>();
        foreach (var g in groupNames)
        {
            var variants = new[]
            {
                SettingLocalizationKeys.GroupCompact(g),
                SettingLocalizationKeys.GroupSnake(g),
                $"SettingGroup_{g.Replace(" ", "_")}", // BuildCrossGroupInfoMessage format
            };
            if (variants.Any(enKeys.Contains)) coveredGroups++;
            else uncoveredGroups.Add($"{g}  (tried: {string.Join(", ", variants.Distinct())})");
        }

        var groupVariantSet = groupNames
            .SelectMany(g => new[]
            {
                SettingLocalizationKeys.GroupCompact(g),
                SettingLocalizationKeys.GroupSnake(g),
            })
            .ToHashSet();

        var nonGroupExpected = new HashSet<string>();
        foreach (var s in settings)
        {
            foreach (var key in SettingLocalizationKeys.ExpectedKeys(s))
            {
                if (groupVariantSet.Contains(key)) continue; // handled by the any-of group logic
                nonGroupExpected.Add(key);
            }
        }

        var presentNonGroup = nonGroupExpected.Where(enKeys.Contains).ToList();
        var absentNonGroup = nonGroupExpected.Where(k => !enKeys.Contains(k)).OrderBy(k => k).ToList();

        _output.WriteLine($"[catalog] settings enumerated: {settings.Count}");
        _output.WriteLine($"[catalog] distinct group names: {groupNames.Count} " +
                          $"(covered: {coveredGroups}, uncovered: {uncoveredGroups.Count})");
        _output.WriteLine($"[catalog] non-group computed keys: {nonGroupExpected.Count} " +
                          $"(present in en.json: {presentNonGroup.Count}, " +
                          $"absent/fallback: {absentNonGroup.Count})");

        if (uncoveredGroups.Count > 0)
        {
            _output.WriteLine("");
            _output.WriteLine("Group names with NO key variant present (relying on raw group name):");
            foreach (var g in uncoveredGroups) _output.WriteLine($"  - {g}");
        }

        if (absentNonGroup.Count > 0)
        {
            _output.WriteLine("");
            _output.WriteLine("Computed keys absent from en.json (resolved via GetStringOrFallback — " +
                              "these fall back to the hardcoded catalog English, not a UI bug):");
            foreach (var k in absentNonGroup) _output.WriteLine($"  - {k}");
        }

        // Non-failing by design — this is a coverage report, not an assertion.
        true.Should().BeTrue();
    }

    // Many keys are referenced dynamically (XAML bindings, interpolated key names) that this static analysis cannot
    // see, so a hard assertion would be hopelessly noisy; _Meta_ keys are whitelisted (read directly by the localization service).
    [Fact]
    public void DeadKeys_Report()
    {
        var enKeys = EnglishKeys();

        var referenced = new HashSet<string>();

        foreach (var file in AllCsFiles())
        {
            var text = File.ReadAllText(file);
            foreach (Match m in GetStringLiteral.Matches(text))
                referenced.Add(m.Groups[1].Value);
        }

        foreach (var s in AllSettings())
        {
            foreach (var key in SettingLocalizationKeys.ExpectedKeys(s))
                referenced.Add(key);
            if (s.Display.GroupName != null)
                referenced.Add($"SettingGroup_{s.Display.GroupName.Replace(" ", "_")}");
        }

        var dead = enKeys
            .Where(k => !referenced.Contains(k))
            .Where(k => !k.StartsWith("_Meta_"))
            .OrderBy(k => k)
            .ToList();

        _output.WriteLine($"[dead-keys] en.json keys: {enKeys.Count}, " +
                          $"statically reachable: {referenced.Intersect(enKeys).Count()}, " +
                          $"potentially dead (approx): {dead.Count}");
        _output.WriteLine("NOTE: approximate — keys used via XAML bindings or dynamically-built " +
                          "key names are NOT detected by this static scan and may show as 'dead'.");
        if (dead.Count > 0)
        {
            _output.WriteLine("");
            foreach (var k in dead) _output.WriteLine($"  - {k}");
        }

        // Non-failing by design.
        true.Should().BeTrue();
    }
}

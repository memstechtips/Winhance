using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Winhance.Core.Features.Common.Localization;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;
using Winhance.IntegrationTests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Winhance.IntegrationTests.Localization;

/// <summary>
/// Closes the "code ↔ key" gap that <see cref="LocalizationJsonValidityTests"/> leaves open:
/// those tests only validate the per-language JSON files against <c>en.json</c>; nothing checks
/// that the keys the C# code actually asks for exist in <c>en.json</c>.
///
/// Two complementary checks:
///   (a) static string-literal keys passed to <c>GetString("...")</c> — these have NO fallback at
///       the call site, so a miss renders the raw <c>[Key]</c> string in the UI. HARD failure.
///   (b) computed/catalog keys derived from the shipped <see cref="SettingDefinition"/>s via
///       <see cref="SettingLocalizationKeys.ExpectedKeys"/>. The settings-localization service
///       resolves these through <c>GetStringOrFallback</c> (see
///       <c>Winhance.UI.Features.Common.Services.SettingLocalizationService</c>), so a missing key
///       is NOT a broken-UI bug — it silently falls back to the hardcoded English string baked into
///       the <see cref="SettingDefinition"/>. Because of that, (b) HARD-asserts only the Name and
///       Description keys (the primary, always-shown UI strings, empirically 100% present today) and
///       REPORTS the remaining computed keys (options / custom-state / tooltips / warnings / groups)
///       as informational coverage rather than failing on the many intentional fallbacks.
///       See the long comment on <see cref="ComputedCatalogKeys_NameAndDescription_MustExist"/>.
///   (c) dead-key report (soft, non-failing): en.json keys referenced by neither (a) nor (b).
/// </summary>
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

    /// <summary>
    /// The full settings catalog the app ships, enumerated directly from the public static
    /// provider methods (the same ones <c>CompatibleSettingsRegistry.GetKnownFeatureProviders()</c>
    /// wires up) — no DI, no reflection, no runtime registry needed.
    /// </summary>
    private static IReadOnlyList<SettingDefinition> AllSettings()
    {
        var groups = new[]
        {
            // Customize features
            ExplorerCustomizations.GetExplorerCustomizations(),
            StartMenuCustomizations.GetStartMenuCustomizations(),
            TaskbarCustomizations.GetTaskbarCustomizations(),
            WindowsThemeCustomizations.GetWindowsThemeCustomizations(),
            // Optimize features
            PowerOptimizations.GetPowerOptimizations(),
            GamingAndPerformanceOptimizations.GetGamingAndPerformanceOptimizations(),
            NotificationOptimizations.GetNotificationOptimizations(),
            PrivacyAndSecurityOptimizations.GetPrivacyAndSecurityOptimizations(),
            SoundOptimizations.GetSoundOptimizations(),
            UpdateOptimizations.GetUpdateOptimizations(),
        };

        return groups.SelectMany(g => g.Settings).ToList();
    }

    private static IEnumerable<string> AllCsFiles() =>
        Directory.EnumerateFiles(SrcDir, "*.cs", SearchOption.AllDirectories);

    // ---------------------------------------------------------------------------------------------
    // Check (a) — static literal keys. HARD failure.
    // ---------------------------------------------------------------------------------------------

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

    // ---------------------------------------------------------------------------------------------
    // Check (b) — computed/catalog keys.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// HARD-asserts that every shipped setting has its Name and Description localization keys in
    /// en.json. These are the primary always-shown strings for each setting and are empirically
    /// 100% present today.
    ///
    /// The OTHER keys in <see cref="SettingLocalizationKeys.ExpectedKeys"/> (per-option display,
    /// tooltip, warning, the per-setting <c>_Option_Custom</c> override, and group keys) are
    /// deliberately NOT hard-asserted here: the service resolves all of them through
    /// <c>GetStringOrFallback</c>, so many are intentionally absent and fall back to the hardcoded
    /// English baked into the catalog (e.g. only 3 of the many ComboBox settings define an
    /// <c>_Option_Custom</c> key; the 130+ individual DNS-server option display names fall back).
    /// Hard-asserting those would fail en masse on intentional fallbacks, not real bugs. They are
    /// reported instead by <see cref="ComputedCatalogKeys_CoverageReport"/>.
    /// </summary>
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

    /// <summary>
    /// Informational coverage report for the fallback-eligible computed keys. NON-failing: it walks
    /// the full catalog, collects <see cref="SettingLocalizationKeys.ExpectedKeys"/> for each
    /// setting, and logs which are present / absent in en.json. Group keys use the any-of rule: a
    /// group counts as covered if the compact OR snake OR cross-group-info ("space → underscore"
    /// only — the third format built by <c>BuildCrossGroupInfoMessage</c>) variant exists.
    /// </summary>
    [Fact]
    public void ComputedCatalogKeys_CoverageReport()
    {
        var enKeys = EnglishKeys();
        var settings = AllSettings();

        // Group-key "any of" acceptable set, including the cross-group-info third format.
        var groupNames = settings
            .Where(s => s.GroupName != null)
            .Select(s => s.GroupName!)
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

        // Non-group computed keys (Name/Description are covered by the hard test above).
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

    // ---------------------------------------------------------------------------------------------
    // Check (c) — dead keys. SOFT, non-failing, informational only.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Reports en.json keys referenced by NEITHER check (a) literal GetString calls NOR check (b)
    /// computed catalog keys. NON-failing: many keys are referenced dynamically (XAML bindings,
    /// interpolated/dynamically-built key names, template/dialog keys) that this static analysis
    /// cannot see, so a hard assertion would be hopelessly noisy. Keys beginning with <c>_Meta_</c>
    /// are whitelisted — e.g. <c>_Meta_LanguageDisplayName</c> is read directly by the localization
    /// service, not via GetString.
    /// </summary>
    [Fact]
    public void DeadKeys_Report()
    {
        var enKeys = EnglishKeys();

        var referenced = new HashSet<string>();

        // (a) literals
        foreach (var file in AllCsFiles())
        {
            var text = File.ReadAllText(file);
            foreach (Match m in GetStringLiteral.Matches(text))
                referenced.Add(m.Groups[1].Value);
        }

        // (b) computed catalog keys (all variants, including all group formats)
        foreach (var s in AllSettings())
        {
            foreach (var key in SettingLocalizationKeys.ExpectedKeys(s))
                referenced.Add(key);
            if (s.GroupName != null)
                referenced.Add($"SettingGroup_{s.GroupName.Replace(" ", "_")}");
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

using System.Text.Json;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using Winhance.TestSupport;

namespace Winhance.Core.Tests.Localization;

// Reports (never fails) keys whose translated string is identical - a translator paid twice for the same
// words, and two strings free to drift apart. Two exclusions carry the design: CONTRACTUAL keys (Setting_{id}_*,
// SettingGroup_, PowerPlan_, _Meta_, and the COMPOSED families - SettingStatusBannerManager appends
// Toggle/Selection to a Common_*Banner_ prefix, and a state Label may itself be a Template_/ServiceOption_ key)
// are skipped because renaming one breaks shipped configs; and a group only counts if identical in EVERY
// language - SoftwareApps_Column_Status and TechnicalDetails_Column_PowerPlanStatus are both "Status" in
// English but "Status" and "Stav" in Czech.
public class LocalizationDuplicateValueReportTests
{
    private readonly ITestOutputHelper _output;

    public LocalizationDuplicateValueReportTests(ITestOutputHelper output) => _output = output;

    // Families whose NAME is derived (from a catalog id, or composed from a prefix) rather than authored; their
    // duplication is a consequence of the naming contract.
    private static readonly string[] ContractualPrefixes =
    [
        // Computed from a catalog Setting.Id / group name / power-plan name by SettingLocalizationKeys.
        "Setting_", "SettingGroup_", "PowerPlan_", "_Meta_",
        // SettingStatusBannerManager builds these as prefix + ("Toggle" | "Selection") at runtime.
        "Common_MalformedBanner_", "Common_UndeterminedBanner_", "Common_CustomBanner_",
        // A catalog state Label can BE one of these; SettingViewModelFactory uses it as the key.
        "Template_", "ServiceOption_",
    ];

    private static bool IsContractual(string key) =>
        ContractualPrefixes.Any(p => key.StartsWith(p, StringComparison.Ordinal));

    private static string LocalizationDir() => RepoPaths.LocalizationDir();

    private static Dictionary<string, string> Load(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.GetString() ?? string.Empty);
    }

    [Fact]
    public void DuplicateTranslations_Report()
    {
        var dir = LocalizationDir();
        var locales = Directory.GetFiles(dir, "*.json")
            .ToDictionary(f => Path.GetFileNameWithoutExtension(f), Load);

        // Non-vacuity. A broken path or glob would otherwise report "nothing to consolidate", which
        // reads exactly like a clean bill of health.
        locales.Should().HaveCountGreaterThan(20, "every shipped language file must be scanned");
        locales.Should().ContainKey("en");
        var english = locales["en"];
        english.Should().HaveCountGreaterThan(500, "en.json is the reference and must have loaded");

        var groups = english
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Value) && !IsContractual(kv.Key))
            .GroupBy(kv => kv.Value.Trim(), StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => new
            {
                Value = g.Key,
                Keys = g.Select(kv => kv.Key).OrderBy(k => k, StringComparer.Ordinal).ToList()
            })
            .ToList();

        // Keep only groups that stay identical in EVERY language. Anything that diverges anywhere is
        // two genuinely different strings that happen to collide in English.
        var collapsible = groups
            .Where(g => locales.Values.All(loc =>
                g.Keys.All(loc.ContainsKey)
                && g.Keys.Select(k => loc[k].Trim()).Distinct(StringComparer.Ordinal).Count() == 1))
            .OrderByDescending(g => g.Keys.Count)
            .ThenBy(g => g.Value, StringComparer.Ordinal)
            .ToList();

        var redundant = collapsible.Sum(g => g.Keys.Count - 1);

        _output.WriteLine($"Locale files scanned : {locales.Count}");
        _output.WriteLine($"en.json keys         : {english.Count}");
        _output.WriteLine($"Collapsible groups   : {collapsible.Count}");
        _output.WriteLine($"Redundant keys       : {redundant} (x{locales.Count} files = {redundant * locales.Count} translated lines)");
        _output.WriteLine($"Diverging groups     : {groups.Count - collapsible.Count} (correctly kept separate)");
        _output.WriteLine(string.Empty);

        foreach (var g in collapsible)
        {
            _output.WriteLine($"\"{g.Value}\"");
            foreach (var k in g.Keys)
                _output.WriteLine($"    {k}");
        }

        // Report-only, like DeadKeys_Report. Read the output when you want to consolidate.
        collapsible.Should().NotBeNull();
    }
}

using System.Runtime.CompilerServices;
using System.Text.Json;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Winhance.Core.Tests.Localization;

/// <summary>
/// Surfaces keys that could be consolidated: different keys whose translated string is identical, so
/// a translator is paid twice for the same words and the two can quietly drift apart later.
/// <para>
/// Reports rather than fails, matching the integration suite's DeadKeys_Report. A newly added key
/// that happens to coincide with an existing one is not a defect, so a hard failure would be noise.
/// </para>
/// <para>
/// Two exclusions carry the whole design, and both are why a naive "same English string means merge
/// them" sweep would corrupt the translations:
/// </para>
/// <list type="number">
/// <item>
/// CONTRACTUAL keys are skipped. A key like Setting_{id}_Name is COMPUTED from a catalog Setting.Id
/// at runtime by SettingLocalizationKeys - the name is not free-form, and renaming it breaks every
/// shipped .winhance config carrying that id. Same for SettingGroup_, PowerPlan_ and _Meta_.
/// Two further families are COMPOSED rather than written out anywhere, so no grep for the whole
/// key finds them: SettingStatusBannerManager appends "Toggle" or "Selection" to a
/// Common_*Banner_ prefix, and a catalog state Label may itself BE a Template_ / ServiceOption_
/// key that SettingViewModelFactory looks up verbatim.
/// </item>
/// <item>
/// A group only counts if it is identical in EVERY language, not just English. Strings that coincide
/// in English routinely diverge elsewhere, and merging those would silently replace one of them:
/// SoftwareApps_Column_Status and TechnicalDetails_Column_PowerPlanStatus are both "Status" in
/// English but "Status" and "Stav" in Czech; Common_CustomDialog_Enabled and
/// TechnicalDetails_Task_Enabled are both "Enabled" but differ in Arabic. Those must stay separate.
/// </item>
/// </list>
/// </summary>
public class LocalizationDuplicateValueReportTests
{
    private readonly ITestOutputHelper _output;

    public LocalizationDuplicateValueReportTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Key families whose NAME is derived rather than authored - computed from a catalog id, or
    /// composed at runtime from a prefix plus a suffix. These may share a value freely; the
    /// duplication is a consequence of the naming contract, not something to clean up.
    /// </summary>
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

    private static string LocalizationDir([CallerFilePath] string callerPath = "")
    {
        var dir = Path.GetDirectoryName(callerPath);
        while (dir != null && !File.Exists(Path.Combine(dir, "Winhance.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        if (dir is null) throw new InvalidOperationException($"Winhance.sln not found from {callerPath}");
        return Path.Combine(dir, "src", "Winhance.UI", "Features", "Common", "Localization");
    }

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

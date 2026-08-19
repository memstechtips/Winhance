using System.Text.Json;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Helpers;
using Xunit;
using Xunit.Abstractions;
using Winhance.TestSupport;

namespace Winhance.Infrastructure.Tests.Catalog;

// The shipped Recommended config must match the in-app per-setting Recommended states 1:1; the in-app applier
// is the PRIMARY path and the config is kept-but-redundant. Reuses the PRODUCTION primitives, never a hand-copied
// rule table - a second source of truth is exactly what this prevents. Recommended roles are build-invariant, so
// one fixed build. Customize + Optimize only. Run: dotnet test --filter RecommendedConfigConformance
public class RecommendedConfigConformanceTests
{
    private readonly ITestOutputHelper _output;

    public RecommendedConfigConformanceTests(ITestOutputHelper output) => _output = output;

    // Any build gives identical results for the Recommended role (verified: every catalog Recommended role is
    // unconditional; build-scoping is exclusive to WindowsDefault on the merged "This PC folder" toggles).
    private static readonly WinBuild Build = new(26100);

    [Fact]
    public void Recommended_config_matches_in_app_recommended_states()
    {
        var config = LoadRecommendedConfig();

        var items = config.Customize.Features.Values
            .Concat(config.Optimize.Features.Values)
            .SelectMany(section => section.Items)
            .ToList();

        var violations = new List<string>();
        var presentCanonicalIds = new HashSet<string>(StringComparer.Ordinal);
        int checkedWithRecommendation = 0;

        foreach (var item in items)
        {
            var canonicalId = SettingIdAliases.Normalize(item.Id);
            presentCanonicalIds.Add(canonicalId);

            var setting = SettingCatalog.Find(item.Id);
            if (setting is null)
            {
                violations.Add($"[dangling] config id '{item.Id}' resolves to no catalog setting.");
                continue;
            }

            var (hasRecommendation, mismatch) = CompareToRecommended(setting, item);
            if (hasRecommendation)
                checkedWithRecommendation++;
            if (mismatch is not null)
                violations.Add($"[value] {canonicalId} ({setting.Control}): {mismatch}");
        }

        // REVERSE / COMPLETENESS: every catalog setting WITH a recommendation must appear in the config (by canonical
        // id), hardware/GPU/existence-gated ones included. The shipped Recommended config is authored complete so a
        // laptop/GPU user importing it gets those recommendations too; non-applicable settings are dropped at
        // apply/review time (CatalogSettingsRegistry membership), NOT at authoring time. A missing recommendation fails.
        foreach (var setting in SettingCatalog.All)
        {
            if (!RecommendedSettingsResolver.HasRecommendedValue(setting, Build))
                continue;
            if (presentCanonicalIds.Contains(setting.Id))
                continue;
            violations.Add(
                $"[missing] recommended setting '{setting.Id}' ({setting.Control}) is absent from the Recommended config.");
        }

        Assert.True(items.Count > 100, $"only {items.Count} settings items read from the config - scoping/deserialization bug.");
        Assert.True(checkedWithRecommendation > 50, $"only {checkedWithRecommendation} items had a recommendation to check - population bug.");

        if (violations.Count > 0)
        {
            _output.WriteLine($"{violations.Count} Recommended-config conformance violation(s):");
            foreach (var v in violations.OrderBy(v => v, StringComparer.Ordinal))
                _output.WriteLine("  " + v);
        }

        Assert.True(
            violations.Count == 0,
            $"The shipped Recommended config does not match the in-app Recommended states ({violations.Count} violation(s)):\n"
                + string.Join("\n", violations.OrderBy(v => v, StringComparer.Ordinal)));
    }

    // Mirrors RecommendedSettingsApplier's per-Control dispatch, computing the expected value from the production
    // primitives and comparing it to what the config item stores. Returns (hasRecommendation, mismatchOrNull).
    private static (bool HasRecommendation, string? Mismatch) CompareToRecommended(Setting setting, ConfigurationItem item)
    {
        switch (setting.Control)
        {
            case ControlKind.Action:
                return (false, null); // Actions are excluded from Apply-Recommended.

            case ControlKind.PowerPlan:
                // power-plan-selection's recommended plan is owned by PowerPlanActivationService, not a per-setting
                // role (HasRecommendedValue is false for it), so it is not part of this 1:1 invariant.
                return (false, null);

            case ControlKind.Toggle:
            {
                var rec = CatalogToggleState.GetRecommended(setting, Build);
                if (rec is null)
                    return (false, null);
                if (item.IsSelected != rec)
                    return (true, $"recommended IsSelected={rec}, config has {FmtBool(item.IsSelected)}.");
                return (true, null);
            }

            case ControlKind.Slider:
            {
                var expected = RecommendedSettingsResolver.BuildPowerCfgApplyValue(setting, useRecommended: true);
                if (expected is null)
                    return (false, null);
                var units = RecommendedSettingsResolver.GetPowerCfgDisplayUnits(setting);
                return (true, CompareSlider(expected, item, units));
            }

            case ControlKind.Selection:
            {
                var expected = RecommendedSettingsResolver.BuildPowerCfgApplyValue(setting, useRecommended: true);
                if (expected is not null)
                    return (true, CompareSelectionAcDc(expected, item));

                var idx = RecommendedSettingsResolver.GetRecommendedIndex(setting); // registry selection
                if (idx is null)
                    return (false, null);
                if (item.SelectedIndex != idx)
                    return (true, $"recommended SelectedIndex={idx}, config has {FmtInt(item.SelectedIndex)}.");
                return (true, null);
            }

            default:
                return (false, null);
        }
    }

    // Powercfg SELECTION: BuildPowerCfgApplyValue returns option INDICES. Separate mode -> {ACValue,DCValue} which the
    // config stores under ACIndex/DCIndex; combined mode -> a bare int index stored under SelectedIndex. No unit
    // conversion (indices, not values).
    private static string? CompareSelectionAcDc(object expected, ConfigurationItem item)
    {
        if (expected is IReadOnlyDictionary<string, object?> dict)
        {
            int? expAc = AsInt(dict.GetValueOrDefault("ACValue"));
            int? expDc = AsInt(dict.GetValueOrDefault("DCValue"));
            int? actAc = AsInt(item.PowerSettings?.GetValueOrDefault("ACIndex"));
            int? actDc = AsInt(item.PowerSettings?.GetValueOrDefault("DCIndex"));
            if (expAc != actAc || expDc != actDc)
                return $"recommended AC={FmtInt(expAc)},DC={FmtInt(expDc)}; config ACIndex={FmtInt(actAc)},DCIndex={FmtInt(actDc)}.";
            return null;
        }

        int? exp = AsInt(expected);
        if (exp != item.SelectedIndex)
            return $"recommended SelectedIndex={FmtInt(exp)}, config has {FmtInt(item.SelectedIndex)}.";
        return null;
    }

    // Powercfg SLIDER: BuildPowerCfgApplyValue returns AC/DC in DISPLAY units; the config stores SYSTEM units
    // (ConfigExportService writes the raw state.AcValue/DcValue). Convert expected display -> system before comparing.
    private static string? CompareSlider(object expected, ConfigurationItem item, string units)
    {
        if (expected is IReadOnlyDictionary<string, object?> dict)
        {
            int? expAc = ToSystem(AsInt(dict.GetValueOrDefault("ACValue")), units);
            int? expDc = ToSystem(AsInt(dict.GetValueOrDefault("DCValue")), units);
            int? actAc = AsInt(item.PowerSettings?.GetValueOrDefault("ACValue"));
            int? actDc = AsInt(item.PowerSettings?.GetValueOrDefault("DCValue"));
            if (expAc != actAc || expDc != actDc)
                return $"recommended AC={FmtInt(expAc)},DC={FmtInt(expDc)} (system units); config ACValue={FmtInt(actAc)},DCValue={FmtInt(actDc)}.";
            return null;
        }

        int? exp = ToSystem(AsInt(expected), units);
        int? act = AsInt(item.PowerSettings?.GetValueOrDefault("Value"));
        if (exp != act)
            return $"recommended value={FmtInt(exp)} (system units), config has {FmtInt(act)}.";
        return null;
    }

    private static int? ToSystem(int? display, string units)
        => display.HasValue ? RecommendedSettingsResolver.ConvertDisplayToSystemUnits(display.Value, units) : (int?)null;

    // Config dictionaries deserialize their object values as JsonElement; normalize to int for comparison.
    private static int? AsInt(object? o) => o switch
    {
        null => null,
        int i => i,
        long l => (int)l,
        JsonElement je when je.ValueKind == JsonValueKind.Number => je.GetInt32(),
        JsonElement je when je.ValueKind == JsonValueKind.String && int.TryParse(je.GetString(), out var s) => s,
        string str when int.TryParse(str, out var s) => s,
        _ => null,
    };

    private static string FmtInt(int? v) => v.HasValue ? v.Value.ToString() : "<unset>";

    private static string FmtBool(bool? v) => v.HasValue ? v.Value.ToString() : "<unset>";

    private static WinhanceConfigFile LoadRecommendedConfig()
    {
        var path = Path.Combine(
            SolutionDir(), "src", "Winhance.UI", "Features", "Common", "Resources", "Configs",
            "Winhance_Recommended_Config.winhance");
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<WinhanceConfigFile>(json, ConfigFileConstants.JsonOptions)
            ?? throw new InvalidOperationException("Recommended config deserialized to null.");
    }

    // Anchors on the compile-time source path (like Winhance.IntegrationTests/Helpers/TestContext), so it resolves
    // the in-repo config even when the test bin folder lives outside the tree (network-share / redirected build root).
    private static string SolutionDir() => RepoPaths.SolutionDir();
}

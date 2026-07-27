using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace Winhance.Infrastructure.Tests.Catalog;

/// <summary>
/// Invariant: every settings-card template that shows a value must also report when Winhance could not
/// place that value.
///
/// This exists because of a real bug, twice over. The detection-outcome overlay was added to the toggle
/// and the registry dropdown, and silently missing from the other eight templates - so a powercfg
/// dropdown whose value matched no catalog option rendered as an empty, unexplained box while its banner
/// said the value was unrecognized. Nothing caught it: the UI project cannot be compiled on Linux, and no
/// test looked at the markup.
///
/// The templates have since been consolidated onto shared controls, which makes the omission far less
/// likely - but "less likely" is not "impossible", and consolidation can be undone by the next person who
/// needs a one-off layout. This test is the part that does not decay: add an input control to a card
/// template without an outcome overlay and the harness fails, on Linux, before anyone builds.
///
/// It reads the XAML as text on purpose. It must fail for markup that is well-formed and would compile.
///
/// Run: winhance-harness CardTemplateOutcomeCoverageTests
/// </summary>
public class CardTemplateOutcomeCoverageTests
{
    private readonly ITestOutputHelper _output;

    public CardTemplateOutcomeCoverageTests(ITestOutputHelper output) => _output = output;

    /// <summary>Controls that display a detected value to the user. A template hosting any of these owes
    /// the user an explanation when detection could not place that value.</summary>
    private static readonly string[] InputControls =
    {
        "ToggleSwitch", "CheckBox", "NumberBox",
        "local:ComboBoxEx", "local:PowerPlanComboBox",
        "local:SettingComboBox", "local:SettingNumberBox",
    };

    /// <summary>Markers that satisfy the invariant: either the overlay control itself, or - for the toggle,
    /// whose overlay is an interactive Button rather than a passive marker - a binding to the outcome.</summary>
    private static readonly string[] OutcomeMarkers =
    {
        "local:SettingOutcomeOverlay",
        "OverlayVisibilityFor(Outcome)",
        "local:SettingComboBox",   // owns its overlay internally
        "local:SettingNumberBox",  // owns its overlay internally
    };

    /// <summary>The only legitimate exemption: an action button runs a task rather than displaying a
    /// detected state, so there is no value for detection to fail to place. Adding a name here must be a
    /// deliberate decision with the same justification, not a way to quiet the test.</summary>
    private static readonly HashSet<string> Exempt = new(StringComparer.Ordinal)
    {
        "ActionSettingTemplate",
    };

    [Fact]
    public void Every_card_template_that_shows_a_value_also_reports_an_unplaceable_one()
    {
        string xaml = File.ReadAllText(CardTemplatePath());
        var templates = SplitTemplates(xaml);

        Assert.True(templates.Count >= 6,
            $"only {templates.Count} DataTemplates parsed out of SettingsCardItem.xaml - the parser or the "
            + "file's shape changed, so this test would pass vacuously.");

        var offenders = new List<string>();
        int checkedCount = 0;

        foreach (var (name, body) in templates)
        {
            bool hasInput = InputControls.Any(c => body.Contains("<" + c, StringComparison.Ordinal));
            if (!hasInput)
                continue;

            checkedCount++;
            if (Exempt.Contains(name))
                continue;

            if (!OutcomeMarkers.Any(m => body.Contains(m, StringComparison.Ordinal)))
            {
                string which = string.Join(", ",
                    InputControls.Where(c => body.Contains("<" + c, StringComparison.Ordinal)));
                offenders.Add($"{name} (hosts {which})");
            }
        }

        _output.WriteLine($"templates: {templates.Count}, input-bearing: {checkedCount}, "
            + $"exempt: {Exempt.Count}, offenders: {offenders.Count}");

        // Non-vacuity: if nothing was recognised as an input control the test proves nothing.
        Assert.True(checkedCount >= 5,
            $"only {checkedCount} input-bearing templates found - the control names in InputControls no "
            + "longer match the markup, so this test is not actually checking anything.");

        Assert.True(offenders.Count == 0,
            "These settings-card templates show a value but never tell the user when Winhance could not "
            + "place it - the setting renders blank or misleading with no explanation:\n  "
            + string.Join("\n  ", offenders)
            + "\n\nAdd a <local:SettingOutcomeOverlay .../> beside the control (or use the shared "
            + "SettingComboBox / SettingNumberBox, which own one), or add the template to Exempt with a "
            + "written reason if it genuinely displays no detected state.");
    }

    /// <summary>Every exempt name must still exist, so a rename cannot leave a silent hole in the gate.</summary>
    [Fact]
    public void Exempt_templates_still_exist()
    {
        string xaml = File.ReadAllText(CardTemplatePath());
        var names = SplitTemplates(xaml).Select(t => t.Name).ToHashSet(StringComparer.Ordinal);

        var stale = Exempt.Where(e => !names.Contains(e)).ToList();
        Assert.True(stale.Count == 0,
            "Exempt lists templates that no longer exist - remove them so the exemption cannot outlive "
            + "the thing it excused:\n  " + string.Join("\n  ", stale));
    }

    /// <summary>Splits the resource dictionary into (template key, body). Nested DataTemplates would break
    /// a naive split, but the card templates do not nest - and the count assertion above catches it if
    /// that ever changes.</summary>
    private static List<(string Name, string Body)> SplitTemplates(string xaml)
    {
        var result = new List<(string, string)>();
        var starts = Regex.Matches(xaml, @"<DataTemplate\s+x:Key=""(?<key>\w+)""");

        foreach (Match start in starts)
        {
            int from = start.Index;
            int end = xaml.IndexOf("</DataTemplate>", from, StringComparison.Ordinal);
            if (end < 0)
                continue;
            result.Add((start.Groups["key"].Value, xaml[from..end]));
        }

        return result;
    }

    private static string CardTemplatePath()
        => Path.Combine(SolutionDir(), "src", "Winhance.UI", "Features", "Common", "Controls",
            "SettingsCardItem.xaml");

    // Anchors on the compile-time source path (CatalogCleanInstallConformanceTests precedent) so the file
    // resolves from the repo even when the build output is redirected off the network share.
    private static string SolutionDir([CallerFilePath] string callerPath = "")
    {
        var dir = Path.GetDirectoryName(callerPath)!;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "src")))
            dir = Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("solution root not found from " + callerPath);
    }
}

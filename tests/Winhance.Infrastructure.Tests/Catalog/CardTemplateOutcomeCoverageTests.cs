using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;
using Winhance.TestSupport;

namespace Winhance.Infrastructure.Tests.Catalog;

// The detection-outcome overlay was added to the toggle and the registry dropdown and silently missing from the
// other eight templates - a powercfg dropdown whose value matched no option rendered as an empty box while its
// banner said the value was unrecognized. Nothing caught it: the UI project cannot be compiled on Linux and no
// test looked at the markup. Reads the XAML as text on purpose - it must fail for markup that is well-formed
// and would compile. Run: winhance-harness CardTemplateOutcomeCoverageTests
public class CardTemplateOutcomeCoverageTests
{
    private readonly ITestOutputHelper _output;

    public CardTemplateOutcomeCoverageTests(ITestOutputHelper output) => _output = output;

    // A template hosting any of these owes the user an explanation when detection could not place the value.
    private static readonly string[] InputControls =
    {
        "ToggleSwitch", "CheckBox", "NumberBox",
        "local:ComboBoxEx", "local:PowerPlanComboBox",
        "local:SettingComboBox", "local:SettingNumberBox",
    };

    // Either the overlay control itself, or - for the toggle, whose overlay is an interactive Button - a binding to the outcome.
    private static readonly string[] OutcomeMarkers =
    {
        "local:SettingOutcomeOverlay",
        "OverlayVisibilityFor(Outcome)",
        "local:SettingComboBox",   // owns its overlay internally
        "local:SettingNumberBox",  // owns its overlay internally
    };

    // An action button runs a task rather than displaying a detected state. Adding a name here must be a deliberate
    // decision with the same justification, not a way to quiet the test.
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

    // A rename must not leave a silent hole in the gate.
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

    // Nested DataTemplates would break a naive split, but the card templates do not nest - the count assertion
    // catches it if that changes.
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
    private static string SolutionDir() => RepoPaths.SolutionDir();
}

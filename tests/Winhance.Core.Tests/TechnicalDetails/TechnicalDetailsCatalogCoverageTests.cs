using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.TechnicalDetails;
using Xunit;
using Xunit.Abstractions;

namespace Winhance.Core.Tests.TechnicalDetails;

// A null from BuildMatrix makes SettingItemViewModel.HasTechnicalDetails false, which hides the panel AND its
// toggle bar; six script-driven settings (gaming-dns-server rewrites DNS entirely through PowerShell) silently
// lost the only window into what they do, and the symptom looked like a UI rendering bug rather than a null
// from a pure Core function.
public class TechnicalDetailsCatalogCoverageTests
{
    private readonly ITestOutputHelper _output;

    public TechnicalDetailsCatalogCoverageTests(ITestOutputHelper output) => _output = output;

    private static readonly WinBuild Build = new(26100);

    // No-setup mock: TryGetString reports every key missing, so every lookup falls back to its English default.
    private static ILocalizationService FallbackLoc() => new Mock<ILocalizationService>().Object;

    [Fact]
    public void EverySetting_ProducesATechnicalDetailsPanel()
    {
        var missing = new List<string>();
        var compared = 0;
        var skippedPowerPlan = 0;

        foreach (var setting in SettingCatalog.All)
        {
            // The power-plan matrix is built from the live list of installed schemes, which only
            // exists at runtime - under a synthetic snapshot it has no options and legitimately
            // returns null. It is the one shape this sweep cannot judge.
            if (setting.Control == ControlKind.PowerPlan)
            {
                skippedPowerPlan++;
                continue;
            }

            compared++;
            var matrix = TechnicalDetailsBuilder.Build(setting, new SettingStateSnapshot(), FallbackLoc(), Build);
            if (matrix is null)
                missing.Add($"{setting.Id} (Control={setting.Control}, Targets={setting.Targets.Count}, States={setting.States.Count})");
        }

        _output.WriteLine($"compared: {compared}, skipped power-plan: {skippedPowerPlan}, missing: {missing.Count}");

        // Non-vacuity. A sweep that silently compares nothing and passes is worse than no test, and a
        // skip census makes over-skipping visible rather than invisible.
        compared.Should().BeGreaterThan(300, "the sweep must actually cover the catalog");
        skippedPowerPlan.Should().Be(1, "power-plan-selection is the only OptionSource setting; a second one means this exemption needs revisiting");

        missing.Should().BeEmpty(
            "a setting with no target still has something to document - its scripts, its fixed registry "
            + "writes, whether it asks for confirmation - and returning null hides the whole panel");
    }

    // Pinned by id so a failure says which behaviour broke rather than "one of 400 settings".
    [Theory]
    [InlineData("gaming-dns-server")]
    [InlineData("system-restore-protection")]
    [InlineData("taskbar-system-tray-icons-11")]
    [InlineData("taskbar-clean")]
    [InlineData("start-menu-clean-10")]
    [InlineData("start-menu-clean-11")]
    public void TargetLessSetting_StillDocumentsItself(string settingId)
    {
        var setting = SettingCatalog.Find(settingId);
        setting.Should().NotBeNull($"{settingId} must exist in the catalog for this test to mean anything");

        var matrix = TechnicalDetailsBuilder.Build(setting, new SettingStateSnapshot(), FallbackLoc(), Build);

        matrix.Should().NotBeNull($"{settingId} has no target, but it still has scripts or side effects to show");

        // Non-vacuity: a matrix that exists but carries nothing would render an empty box, which is
        // no better than the missing panel this test exists to prevent.
        var carriesSomething = matrix!.CodeBlocks.Count > 0 || matrix.Notes.Count > 0
            || matrix.Requirements.Count > 0 || matrix.HasOptionLinks;
        carriesSomething.Should().BeTrue($"{settingId}'s panel must actually contain something to read");

        // The panel existing is not enough on its own: the first fix returned a matrix with no
        // rows, so a setting with real named options showed one code block per option and no way
        // to tell the options apart.
        // An Action carries no States, so States.Count is not its row count. It gets exactly ONE row -
        // the action itself, with the values it writes beside it - when it has registry writes to fill
        // that row; a script-only Action (start-menu-clean-10) keeps an empty option list, because a
        // labelled row with no cell to its right heads nothing.
        var expectedRows = setting!.Control == ControlKind.Action
            ? (setting.Effects.OfType<RegistryWriteEffect>().Any() ? 1 : 0)
            : setting.States.Count;
        matrix.Options.Should().HaveCount(expectedRows,
            $"{settingId}'s options are real whether or not there is a column to write them in");
        // NotContain rather than OnlyContain: two of these ids are script-only Actions with no rows at
        // all, and an empty option list is the right answer for them - OnlyContain would fail them for
        // being empty rather than for holding a wrong row.
        matrix.Options.Should().NotContain(o => o.Cells.Count != matrix.Columns.Count,
            "every row carries one cell per column, and most of these settings have no columns at all");
    }

    // gaming-dns-server names every DNS provider it can set and exactly one is both what Winhance suggests and what
    // Windows ships with; a panel listing a script per option without saying which is that tells the reader nothing actionable.
    [Fact]
    public void TargetLessSetting_KeepsTheRolesOnItsOptions()
    {
        var setting = SettingCatalog.Find("gaming-dns-server");
        setting.Should().NotBeNull();

        var matrix = TechnicalDetailsBuilder.Build(setting, new SettingStateSnapshot(), FallbackLoc(), Build);

        matrix!.Options.Should().HaveCount(setting!.States.Count);
        matrix.Options.Should().OnlyContain(o => o.Label.Length > 0,
            "every row still names the option it documents");
        matrix.Options[0].IsRecommended.Should().BeTrue("the first state carries StateRole.Recommended");
        matrix.Options[0].IsWindowsDefault.Should().BeTrue("and StateRole.WindowsDefault with it");
        matrix.Options.Skip(1).Should().OnlyContain(o => !o.IsRecommended && !o.IsWindowsDefault,
            "no other state carries a role, and inventing one would be worse than saying nothing");
    }
}

using FluentAssertions;
using Winhance.Core.Features.Common.Catalog;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

// Pins the migration from an INFERRED appearance notice (any setting writing under Themes\Personalize) to a
// DECLARED one (ApplyBehavior.NotifyWindows): the declared set must equal the set the old inference picked. Add
// a theme setting and forget the declaration and dark mode stops applying live until the shell restarts;
// declare it on a setting that writes no personalisation key and it pays a per-window SendMessageTimeout for nothing.
public class AppearanceNoticeConformanceTests
{
    // Where Windows keeps light/dark mode and transparency.
    private const string PersonalizeKeyFragment = @"Themes\Personalize";

    // The old inferred rule, kept as the ASSERTION: case-insensitive because registry paths are authored by hand;
    // every path of a mirrored target counts.
    private static bool WritesThePersonalizeKey(Setting setting) =>
        setting.Targets.OfType<RegTarget>().Any(
            target => target.Paths is not null
                && target.Paths.Any(path => path is not null
                    && path.Contains(PersonalizeKeyFragment, StringComparison.OrdinalIgnoreCase)));

    private static bool DeclaresAppearanceNotice(Setting setting) =>
        setting.Apply.NotifyWindows.HasFlag(WindowsChange.Appearance);

    [Fact]
    public void DeclaredAppearanceNotice_MatchesExactlyTheSettingsThatWriteThePersonalizeKey()
    {
        var writesTheKey = SettingCatalog.All.Where(WritesThePersonalizeKey).Select(s => s.Id).ToList();
        var declaresIt = SettingCatalog.All.Where(DeclaresAppearanceNotice).Select(s => s.Id).ToList();

        // Non-vacuity. An empty catalog, a renamed target or a predicate that quietly matches nothing would
        // make the equivalence below trivially true and assert precisely nothing.
        writesTheKey.Should().NotBeEmpty(
            "settings that write the personalisation key exist - an empty expectation proves nothing");

        declaresIt.Should().BeEquivalentTo(writesTheKey,
            "the notice is declared now, and a declaration has to say what is true of the setting");
    }

    [Fact]
    public void DeclaredAppearanceNotice_IsAbsentFromTheOverwhelmingMajorityOfExplorerRestartSettings()
    {
        var explorerRestartSettings = SettingCatalog.All
            .Where(s => s.Apply.Restart is RestartProcess rp && rp.Name == "Explorer")
            .ToList();

        var declaringOnes = explorerRestartSettings.Where(DeclaresAppearanceNotice).ToList();

        explorerRestartSettings.Should().HaveCountGreaterThan(declaringOnes.Count * 5,
            "the split only pays off because the overwhelming majority of Explorer-restart settings cannot "
            + "change how Windows looks - declaring the notice broadly would give the cost straight back");
    }

    [Fact]
    public void NonThemeExplorerSetting_DeclaresNoAppearanceNotice()
    {
        var taskView = SettingCatalog.ById["taskbar-task-view"];

        // The setting the split was reported against: it DOES declare an Explorer restart, so it reaches the
        // broadcast - it just has no business paying for the theme half of it.
        taskView.Apply.Restart.Should().Be(new RestartProcess("Explorer"));
        DeclaresAppearanceNotice(taskView).Should().BeFalse();
    }
}

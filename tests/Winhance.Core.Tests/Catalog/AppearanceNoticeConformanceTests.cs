using System;
using System.Linq;
using FluentAssertions;
using Winhance.Core.Features.Common.Catalog;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

/// <summary>
/// The appearance notice used to be INFERRED: code looked for a ...\Themes\Personalize registry path and
/// decided for itself which settings deserved the expensive half of the shell broadcast. It is now DECLARED
/// on the setting (<see cref="ApplyBehavior.NotifyWindows"/>), next to its confirmation gate and its restart.
///
/// This test is the migration's pin. The declared set has to be exactly the set the old inference picked, so
/// the change provably moved where the fact LIVES without moving which settings pay for the broadcast.
///
/// It keeps earning its place afterwards, which is the point of writing it as a rule rather than an id list.
/// Add a theme setting and forget the declaration, and dark mode silently stops applying live until the shell
/// restarts. Declare it on a setting that writes no personalisation key, and that setting pays a
/// per-top-level-window SendMessageTimeout it cannot possibly benefit from. Either way, this fails.
/// </summary>
public class AppearanceNoticeConformanceTests
{
    /// <summary>The registry key Windows keeps light/dark mode and transparency under.</summary>
    private const string PersonalizeKeyFragment = @"Themes\Personalize";

    /// <summary>
    /// The OLD, inferred rule, kept alive here as the ASSERTION rather than as production behaviour: does the
    /// setting write under the personalisation key? Case-insensitive, because registry paths are authored by
    /// hand and Windows does not care about their casing. Every path of a mirrored target counts.
    /// </summary>
    private static bool WritesThePersonalizeKey(Setting setting) =>
        setting.Targets.OfType<RegTarget>().Any(
            target => target.Paths is not null
                && target.Paths.Any(path => path is not null
                    && path.Contains(PersonalizeKeyFragment, StringComparison.OrdinalIgnoreCase)));

    /// <summary>The NEW, declared rule - what ProcessRestartManager actually reads.</summary>
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

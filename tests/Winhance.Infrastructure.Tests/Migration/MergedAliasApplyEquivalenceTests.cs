using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Catalog.Migration;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;
using Xunit;
using Xunit.Abstractions;

namespace Winhance.Infrastructure.Tests.Migration;

/// <summary>Edge-1 proof: the 6 retired "-win10" This PC alias defs, applied under their "-win10" id on Windows
/// 10, route (via ApplyRequestResolver alias-normalize) to the canonical MERGED catalog Setting built with the Win10
/// build gate - and that merged Win10 apply reproduces the old "-win10" executor's registry write intent exactly.
/// Pure - no registry I/O. Run: dotnet test --filter MergedAliasApplyEquivalence</summary>
public class MergedAliasApplyEquivalenceTests
{
    private readonly ITestOutputHelper _output;
    public MergedAliasApplyEquivalenceTests(ITestOutputHelper output) => _output = output;

    // The retired alias defs are exactly the old defs whose id normalizes to a DIFFERENT (canonical) id.
    private static IReadOnlyList<SettingDefinition> AliasDefs() =>
        ExplorerCustomizations.GetExplorerCustomizations().Settings
            .Where(d => SettingIdAliases.Normalize(d.Id) != d.Id)
            .ToList();

    [Fact]
    public void MergedAliasApplyEquivalence_OldWin10ExecutorAndNewMergedApplyAgree()
    {
        var aliasDefs = AliasDefs();
        Assert.NotEmpty(aliasDefs); // guard against a scoping bug that would make the test vacuous

        var win10 = new WinBuild(19045); // a Windows 10 build (< 22000)
        var rows = ApplyEquivalenceHarness.RunMergedAliasApply(aliasDefs, SettingCatalog.All, win10);

        foreach (var row in rows)
        {
            var tag = row.Match ? "[MATCH]" : "[DIFF]";
            _output.WriteLine($"{tag} {row.Id}");
            if (!row.Match)
            {
                _output.WriteLine($"    old: {row.OldState}");
                _output.WriteLine($"    new: {row.NewState}");
            }
        }

        // Every alias def must contribute both directions (Enabled + Disabled); a missing merged peer would drop rows.
        Assert.Equal(aliasDefs.Count * 2, rows.Count);
        var matched = rows.Count(r => r.Match);
        _output.WriteLine($"{matched}/{rows.Count} match");
        Assert.True(rows.All(r => r.Match), $"{rows.Count - matched} merged-alias apply plans differ - see output");
    }
}

using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Catalog.Migration;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;
using Xunit;
using Xunit.Abstractions;

namespace Winhance.Infrastructure.Tests.Migration;

/// <summary>Throwaway migration check: for every InputType.Action one-shot, compares the old live apply's
/// enabled-branch write intent (WindowsRegistryService.ApplySetting + enabled scripts, mirrored) against the
/// new ApplyPlanBuilder.BuildAction plan. Pure - no registry I/O. Run: dotnet test --filter ActionApplyEquivalence</summary>
public class ActionApplyEquivalenceTests
{
    private readonly ITestOutputHelper _output;

    public ActionApplyEquivalenceTests(ITestOutputHelper output) => _output = output;

    private static IEnumerable<SettingDefinition> AllDefinitions() => new[]
    {
        StartMenuCustomizations.GetStartMenuCustomizations().Settings,
        TaskbarCustomizations.GetTaskbarCustomizations().Settings,
    }.SelectMany(group => group);

    [Fact]
    public void ActionApplyEquivalence_OldAndNewApplyAgree()
    {
        var rows = ApplyEquivalenceHarness.RunActionApply(AllDefinitions());

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

        Assert.NotEmpty(rows); // sanity: the 3 Actions were found
        var matched = rows.Count(r => r.Match);
        _output.WriteLine($"{matched}/{rows.Count} match, {rows.Count - matched} differ");

        Assert.True(rows.All(r => r.Match), $"{rows.Count - matched} action apply plans differ - see output");
    }
}

using Xunit;
using static Winhance.UI.Tests.Conventions.ConventionSource;

namespace Winhance.UI.Tests.Conventions;

// Whether every row a select-all box covers is ticked is decided in SelectAllRows alone, which the view models read.
// A box that counts the rows itself can disagree with the others after a search or a status change.
public class SelectAllConventionTests
{
    private const string SelectAllRows = Ui + "Features/SoftwareApps/ViewModels/SelectAllRows.cs";

    [Fact]
    public void EveryRowTicked_IsDecidedOnlyInSelectAllRows()
    {
        var offenders = Count(CsUi, @"(?:\bTrueForAll|\.All)\(\s*\w+\s*=>\s*\w+\.IsSelected\s*\)", SelectAllRows);

        Assert.True(offenders.Count == 0,
            "Code decides whether every row is ticked by itself; read the view model's IsAllSelected or call "
            + "SelectAllRows:\n  " + string.Join("\n  ", offenders.Keys));
    }
}

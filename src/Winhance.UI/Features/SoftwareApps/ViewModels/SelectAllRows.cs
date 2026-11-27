namespace Winhance.UI.Features.SoftwareApps.ViewModels;

// A select-all box of a tab. The caller passes the rows the box covers, which are the ones the filter and search
// show, so the box never reads or ticks a row the user cannot see.
internal static class SelectAllRows
{
    internal static bool AreAllSelected(IEnumerable<AppItemViewModel> rows)
    {
        var covered = rows.ToList();
        return covered.Count > 0 && covered.TrueForAll(a => a.IsSelected);
    }

    internal static void Toggle(IEnumerable<AppItemViewModel> rows)
    {
        var covered = rows.ToList();
        var newValue = !AreAllSelected(covered);
        foreach (var item in covered)
            item.IsSelected = newValue;
    }
}

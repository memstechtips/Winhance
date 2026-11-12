namespace Winhance.UI.Features.SoftwareApps.ViewModels;

// The "Select All With Updates" box of a tab. The caller passes the rows it can update.
internal static class UpdateSelection
{
    internal static bool IsAllSelected(IEnumerable<AppItemViewModel> rows)
    {
        var withUpdates = rows.Where(a => a.HasUpdate).ToList();
        return withUpdates.Count > 0 && withUpdates.All(a => a.IsSelected);
    }

    internal static void Toggle(IEnumerable<AppItemViewModel> rows)
    {
        var withUpdates = rows.Where(a => a.HasUpdate).ToList();
        var newValue = !IsAllSelected(withUpdates);
        foreach (var item in withUpdates)
            item.IsSelected = newValue;
    }
}

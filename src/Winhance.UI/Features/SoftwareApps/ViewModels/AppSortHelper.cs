using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.WinUI.Collections;
using Winhance.UI.Features.SoftwareApps.Models;

namespace Winhance.UI.Features.SoftwareApps.ViewModels;

/// <summary>
/// Single source of truth for app-list ordering, shared by WindowsAppsViewModel
/// and ExternalAppsViewModel so the two never drift. Applied both to the
/// AdvancedCollectionView (Table view) and to the in-memory LINQ projections
/// (Card/Compact views and External category groups).
/// </summary>
internal static class AppSortHelper
{
    public static void ApplySortDescriptions(AdvancedCollectionView view, AppSortMode mode)
    {
        using (view.DeferRefresh())
        {
            view.SortDescriptions.Clear();
            switch (mode)
            {
                case AppSortMode.NameAsc:
                    view.SortDescriptions.Add(new SortDescription("Name", SortDirection.Ascending));
                    break;
                case AppSortMode.NameDesc:
                    view.SortDescriptions.Add(new SortDescription("Name", SortDirection.Descending));
                    break;
                default: // NameAscInstalledFirst
                    view.SortDescriptions.Add(new SortDescription("IsInstalled", SortDirection.Descending));
                    view.SortDescriptions.Add(new SortDescription("Name", SortDirection.Ascending));
                    break;
            }
        }
    }

    public static IEnumerable<AppItemViewModel> Order(IEnumerable<AppItemViewModel> source, AppSortMode mode) => mode switch
    {
        AppSortMode.NameAsc => source.OrderBy(a => a.Name),
        AppSortMode.NameDesc => source.OrderByDescending(a => a.Name),
        _ => source.OrderByDescending(a => a.IsInstalled).ThenBy(a => a.Name),
    };
}

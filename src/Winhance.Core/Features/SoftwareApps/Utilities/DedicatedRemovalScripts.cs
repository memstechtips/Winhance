using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Utilities;

// Items that share a script share one file, so each script runs once: an item whose script also removes another
// selected item stands for both, and a second run would race on the same file.
public static class DedicatedRemovalScripts
{
    public static List<ItemDefinition> OnePerScript(IEnumerable<ItemDefinition> items)
    {
        var scripted = items.Where(a => a.RemovalScript != null).ToList();
        var covered = scripted.SelectMany(a => a.AlsoRemoves ?? []).ToHashSet();
        return [.. scripted.Where(a => !covered.Contains(a.Id)).DistinctBy(a => a.RemovalScriptName)];
    }
}

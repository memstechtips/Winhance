using Winhance.Core.Features.Common.Selections;
using Winhance.Core.Features.SoftwareApps.Catalogs;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Utilities;

// How the selected Windows apps split into removal work. The answer file and the offline image build both read this,
// so they cannot disagree. A dedicated script wins over a capability, a feature and a package.
// SpecialApps are registry-uninstall apps (OneNote) that only run on an installed system; the image build ignores them.
// DedicatedPackages are the Appx packages of every selected dedicated app: the image build removes them itself because
// no script can run there, while the answer file leaves them to the dedicated script.
public sealed record AppRemovalPlan(
    IReadOnlyList<string> Packages,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<string> Features,
    IReadOnlyList<string> SpecialApps,
    IReadOnlyList<ItemDefinition> Dedicated,
    IReadOnlyList<string> DedicatedPackages)
{
    private static readonly List<ItemDefinition> DedicatedCatalog = WindowsAppDefinitions.GetWindowsApps().Items
        .Where(a => a.RemovalScript != null)
        .ToList();

    public static AppRemovalPlan For(IReadOnlyList<AppChoice> apps)
    {
        var packages = new List<string>();
        var capabilities = new List<string>();
        var features = new List<string>();
        var specialApps = new List<string>();
        var selectedDedicated = new List<ItemDefinition>();

        foreach (var app in apps)
        {
            if (DedicatedCatalog.FirstOrDefault(d => d.Id == app.Id) is { } item)
            {
                selectedDedicated.Add(item);
            }
            else if (!string.IsNullOrEmpty(app.CapabilityName))
            {
                capabilities.Add(app.CapabilityName);
            }
            else if (!string.IsNullOrEmpty(app.OptionalFeatureName))
            {
                features.Add(app.OptionalFeatureName);
            }
            else if (app.AppxPackageName?.Length > 0)
            {
                packages.AddRange(app.AppxPackageName);

                if (app.AppxPackageName.Any(name => name.Contains("OneNote", StringComparison.OrdinalIgnoreCase)) &&
                    !specialApps.Contains("OneNote"))
                {
                    specialApps.Add("OneNote");
                }
            }
        }

        var dedicated = DedicatedRemovalScripts.OnePerScript(selectedDedicated)
            .OrderBy(d => DedicatedCatalog.FindIndex(x => x.RemovalScriptName == d.RemovalScriptName))
            .ToList();

        return new AppRemovalPlan(
            packages,
            capabilities,
            features,
            specialApps,
            dedicated,
            [.. selectedDedicated.SelectMany(d => d.AppxPackageName ?? [])]);
    }
}

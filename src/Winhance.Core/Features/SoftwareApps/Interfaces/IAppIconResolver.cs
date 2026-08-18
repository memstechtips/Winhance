using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

// Two layered sources, in order: installed AppX extraction (current user / all users / provisioned) for
// windows-app-* entries present on the machine, then the package-icons repo (jsDelivr @main), sha256-verified
// against the manifest. Neither -> IconPath stays null and the UI renders a category glyph. There is no live Store API.
public interface IAppIconResolver
{
    // Failures are logged and swallowed - IconPath stays null on any per-entry or batch-level failure.
    // applyThemeAdaptation: Windows Apps icons get backplate crop + synthesized light/dark variants; External Apps
    // vendor logos are cached as shipped.
    Task ResolveBatchAsync(
        IEnumerable<ItemDefinition> definitions,
        bool applyThemeAdaptation = true,
        CancellationToken ct = default);
}

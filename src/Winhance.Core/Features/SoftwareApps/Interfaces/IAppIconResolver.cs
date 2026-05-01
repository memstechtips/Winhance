using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

/// <summary>
/// Resolves and caches app icons, populating ItemDefinition.IconPath for each
/// entry. Tries layered sources in order:
///   Layer 1a — installed AppX (current user / all users / provisioned).
///   Layer 1b — Win32 binary extraction from <c>InstalledBinaryHint</c>
///              (registry DisplayIcon — Windows ARP).
///   Layer 2a — Microsoft Store CDN (when <c>MsStoreId</c> is present).
///   Layer 2b — Per-entry <c>IconSources</c> (URLs and/or local file paths,
///              tried in array order; first hit wins).
/// Entries that resolve via none of the above keep IconPath null and the UI
/// renders a category-specific fallback glyph.
/// </summary>
public interface IAppIconResolver
{
    /// <summary>
    /// Walks the given definitions, resolves icons via the layered chain,
    /// and stamps ItemDefinition.IconPath on success. Failures are logged and
    /// swallowed — IconPath stays null on any per-entry or batch-level failure.
    /// </summary>
    Task ResolveBatchAsync(IEnumerable<ItemDefinition> definitions, CancellationToken ct = default);
}

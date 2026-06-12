using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

/// <summary>
/// Resolves and caches app icons, populating ItemDefinition.IconPath for each
/// entry. Tries two layered sources in order:
///   Layer 1 — installed AppX extraction (current user / all users / provisioned),
///             for windows-app-* entries whose package is present on the machine.
///   Layer 2 — the package-icons repo (jsDelivr @main), sha256-verified against
///             the manifest. Resolves external-app-*, windows-app-* (by AppX
///             identity), capability-*, and feature-* entries via
///             <see cref="Winhance.Core.Features.SoftwareApps.Models.RepoIconKey"/>.
/// Entries that resolve via neither keep IconPath null (colored fallback) and the
/// UI renders a category-specific fallback glyph. There is no live Store API and
/// no local binary-hint extraction.
/// </summary>
public interface IAppIconResolver
{
    /// <summary>
    /// Walks the given definitions, resolves icons via the layered chain,
    /// and stamps ItemDefinition.IconPath on success. Failures are logged and
    /// swallowed — IconPath stays null on any per-entry or batch-level failure.
    /// </summary>
    /// <param name="applyThemeAdaptation">
    /// When true (Windows Apps), cached icons get theme adaptation: uniform
    /// backplates are cropped and monochrome icons get synthesized light/dark
    /// variants. When false (External Apps), vendor brand logos are cached
    /// exactly as shipped — only the basic transparent-border trim is applied.
    /// </param>
    Task ResolveBatchAsync(
        IEnumerable<ItemDefinition> definitions,
        bool applyThemeAdaptation = true,
        CancellationToken ct = default);
}

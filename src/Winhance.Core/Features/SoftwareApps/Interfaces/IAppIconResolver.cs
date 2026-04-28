using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

/// <summary>
/// Resolves and caches AppX app icons, populating ItemDefinition.IconPath
/// for installed AppX entries. Capabilities, optional features, and
/// not-installed AppX entries are skipped (their IconPath stays null and
/// the UI renders a category-specific fallback glyph).
/// </summary>
public interface IAppIconResolver
{
    /// <summary>
    /// Walks the given definitions, resolves icons for installed AppX entries,
    /// and stamps ItemDefinition.IconPath on success. Failures are logged and
    /// swallowed — IconPath stays null on any per-package or batch-level failure.
    /// </summary>
    Task ResolveBatchAsync(IEnumerable<ItemDefinition> definitions, CancellationToken ct = default);
}

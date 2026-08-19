using Winhance.Core.Features.Common.Catalog;

namespace Winhance.Core.Features.Common.Selections;

// The one machine-to-choices reader: what every catalog setting in scope is set to on this machine right now.
public interface ISettingSnapshotSource
{
    Task<IReadOnlyList<SettingChoice>> CaptureAsync(CatalogScope scope);
}

using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Selections;

namespace Winhance.Core.Features.Common.Interfaces;

// The one machine-to-choices reader: what every catalog setting in scope is set to on this machine right now.
public interface ISettingSnapshotSource
{
    Task<IReadOnlyList<SettingChoice>> CaptureAsync(CatalogScope scope);

    // Keyed by the wanted choice's id; an id this machine does not offer is left out. A carried file lets a picture
    // this PC already has under another name count as a match.
    Task<IReadOnlyDictionary<string, SettingMatch>> CompareAsync(IReadOnlyList<SettingChoice> wanted, IReadOnlyList<CarriedFile> carried, CatalogScope scope);
}

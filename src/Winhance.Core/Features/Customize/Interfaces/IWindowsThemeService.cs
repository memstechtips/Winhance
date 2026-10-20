using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Core.Features.Customize.Interfaces;

// Sync on purpose: it is called from inside the synchronous apply plan. A leaf of IStateWriter, so it must hold no
// reference back to the writer.
public interface IWindowsThemeService
{
    bool RefreshDesktop();

    bool SetSlideshow(string folder);

    // Null while the desktop plays no slideshow.
    string? CurrentAlbum(Setting setting, IDetectionContext context);

    // Whether a picture has to travel as bytes, because the PC it is applied to may not have it.
    bool Travels(string path);

    string DestinationFor(string path);

    string AlbumDestinationFor(string folder);
}

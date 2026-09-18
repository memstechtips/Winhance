using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.UI.Shell;

namespace Winhance.Infrastructure.Features.Customize.Services;

// COM because the folder id Windows records embeds the folder as the shell sees it and cannot be written from
// outside that machine. SetSlideshow takes "a single item which is the container itself".
internal interface IDesktopSlideshow
{
    void Set(string folder, int position, int intervalMs, bool shuffle);
}

internal sealed class DesktopSlideshow : IDesktopSlideshow
{
    public void Set(string folder, int position, int intervalMs, bool shuffle)
    {
        var desktop = (IDesktopWallpaper)new DesktopWallpaper();
        try
        {
            PInvoke.SHCreateItemFromParsingName(folder, null, typeof(IShellItem).GUID, out object item)
                .ThrowOnFailure();
            PInvoke.SHCreateShellItemArrayFromShellItem((IShellItem)item, typeof(IShellItemArray).GUID, out object album)
                .ThrowOnFailure();

            desktop.SetSlideshow((IShellItemArray)album);
            desktop.SetSlideshowOptions(shuffle ? DESKTOP_SLIDESHOW_OPTIONS.DSO_SHUFFLEIMAGES : 0, (uint)intervalMs);
            desktop.SetPosition((DESKTOP_WALLPAPER_POSITION)position);
        }
        finally
        {
            Marshal.ReleaseComObject(desktop);
        }
    }
}

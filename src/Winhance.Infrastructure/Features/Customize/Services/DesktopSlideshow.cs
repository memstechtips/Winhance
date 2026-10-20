using System.Runtime.InteropServices;
using Winhance.Desktop;

namespace Winhance.Infrastructure.Features.Customize.Services;

// COM because the folder id Windows records embeds the folder as the shell sees it and cannot be written from
// outside that machine. The call itself lives in DesktopBackgroundInterop, which the answer file also carries.
internal interface IDesktopSlideshow
{
    void Set(string folder, int position, int intervalMs, bool shuffle);

    string? Folder();
}

internal sealed class DesktopSlideshow : IDesktopSlideshow
{
    public void Set(string folder, int position, int intervalMs, bool shuffle) =>
        DesktopBackground.Slideshow(folder, position, intervalMs, shuffle);

    public string? Folder()
    {
        try
        {
            return DesktopBackground.SlideshowFolder() is { Length: > 0 } folder ? folder : null;
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            return null;
        }
    }
}

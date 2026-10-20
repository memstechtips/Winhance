using Winhance.Core.Features.WimUtil.Models;

namespace Winhance.UI.Features.WimUtil.Models;

// The media a session started from the WIMUtil card is building. The bar's Save reads the folder.
public sealed class WimUtilSession
{
    public string? WorkingDirectory { get; set; }

    // Read when the session starts and again after Generate removes editions, so the Unattend page can offer them
    // without reading the image itself.
    public IReadOnlyList<ImageEdition> Editions
    {
        get;
        set
        {
            field = value;
            EditionsChanged?.Invoke(this, EventArgs.Empty);
        }
    } = [];

    public event EventHandler? EditionsChanged;

    public void Reset()
    {
        WorkingDirectory = null;
        Editions = [];
    }
}

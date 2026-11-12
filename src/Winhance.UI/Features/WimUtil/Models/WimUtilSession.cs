using Winhance.Core.Features.WimUtil.Models;

namespace Winhance.UI.Features.WimUtil.Models;

// The media a session started from the WIMUtil card is building. The bar's Save reads the folder.
public sealed class WimUtilSession
{
    public string? WorkingDirectory { get; set; }

    // Mirrors the WIMUtil page's choice of building changes into the image instead of applying them during setup.
    public bool BuiltIntoImage
    {
        get;
        set
        {
            if (field == value)
                return;

            field = value;
            BuiltIntoImageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

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

    // The Unattend summary cards count the selection; step 3's header reads the same numbers.
    public ChangeCounts Changes
    {
        get;
        set
        {
            if (field == value)
                return;

            field = value;
            ChangesChanged?.Invoke(this, EventArgs.Empty);
        }
    } = new(0, 0, 0);

    public event EventHandler? EditionsChanged;

    public event EventHandler? ChangesChanged;

    public event EventHandler? BuiltIntoImageChanged;

    public void Reset()
    {
        WorkingDirectory = null;
        Editions = [];
    }

    public sealed record ChangeCounts(int RemovedApps, int InstalledApps, int Settings);
}

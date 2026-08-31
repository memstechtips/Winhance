namespace Winhance.Core.Features.AdvancedTools.Interfaces;

public interface IIsoImageReader
{
    IIsoAttachment Attach(string isoPath);
}

// Disposing detaches the image. The attachment is bound to a handle Winhance owns, so Windows
// also detaches it if the process dies mid-run - which is what the PowerShell Mount-DiskImage
// spawn could not do, since that process exited immediately and left the ISO attached.
public interface IIsoAttachment : IDisposable
{
    string RootPath { get; }
}

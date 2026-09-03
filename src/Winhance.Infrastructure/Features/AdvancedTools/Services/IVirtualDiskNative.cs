namespace Winhance.Infrastructure.Features.AdvancedTools.Services;

// Mirrors ATTACH_VIRTUAL_DISK_FLAG. Declared here rather than used directly so the flag choices
// - which decide whether the attachment survives Winhance exiting - are assertable without an ISO.
[Flags]
internal enum AttachFlags
{
    None = 0,
    ReadOnly = 1,
    NoDriveLetter = 2,
    PermanentLifetime = 4,
}

internal interface IVirtualDiskHandle : IDisposable
{
    bool IsClosed { get; }
}

internal interface IVirtualDiskNative
{
    IVirtualDiskHandle Open(string isoPath);

    void Attach(IVirtualDiskHandle handle, AttachFlags flags);

    string GetVolumeRootPath(IVirtualDiskHandle handle);
}

using Winhance.Core.Features.AdvancedTools.Interfaces;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.AdvancedTools.Services;

internal sealed class VirtualDiskIsoImageReader : IIsoImageReader
{
    private readonly IVirtualDiskNative _native;
    private readonly ILogService _logService;

    public VirtualDiskIsoImageReader(IVirtualDiskNative native, ILogService logService)
    {
        _native = native;
        _logService = logService;
    }

    public IIsoAttachment Attach(string isoPath)
    {
        var handle = _native.Open(isoPath);

        try
        {
            // READ_ONLY is mandatory for an ISO - ATTACH_VIRTUAL_DISK_FLAG_NONE is documented as
            // unsupported for one. NO_DRIVE_LETTER keeps the image out of Explorer while Winhance
            // copies from it. PERMANENT_LIFETIME is the flag that would decouple the attachment from
            // this handle, which is exactly the ownership we want, so it is never requested.
            _native.Attach(handle, AttachFlags.ReadOnly | AttachFlags.NoDriveLetter);

            var rootPath = _native.GetVolumeRootPath(handle);
            _logService.LogInformation($"Attached {isoPath} at {rootPath}");

            return new VirtualDiskAttachment(handle, rootPath, _logService);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    private sealed class VirtualDiskAttachment : IIsoAttachment
    {
        private readonly IVirtualDiskHandle _handle;
        private readonly ILogService _logService;
        private bool _disposed;

        internal VirtualDiskAttachment(IVirtualDiskHandle handle, string rootPath, ILogService logService)
        {
            _handle = handle;
            _logService = logService;
            RootPath = rootPath;
        }

        public string RootPath { get; }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _handle.Dispose();
            _logService.LogInformation("Detached the source ISO");
        }
    }
}

using System.Runtime.InteropServices;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Native;
using Winhance.Infrastructure.Features.Common.Utilities;

namespace Winhance.Infrastructure.Features.AdvancedTools.Services;

internal sealed class DismImageInfoReader : IDismImageInfoReader
{
    private readonly ILogService _logService;

    public DismImageInfoReader(ILogService logService)
    {
        _logService = logService;
    }

    public IReadOnlyList<DismImageEntry> GetImageInfo(string imagePath)
    {
        // Bracketing the native call is the only diagnostic there is: a struct declared with the
        // wrong packing kills the process outright, with no exception to catch and no stack to log.
        // If the log ends on the first of these two lines, the fault is inside DISM's marshalling.
        _logService.LogInformation($"DismGetImageInfo: reading {imagePath}");

        var entries = DismSessionManager.ExecuteWithoutSession(() =>
        {
            DismApi.ThrowIfFailed(
                DismApi.DismGetImageInfo(imagePath, out var imageInfo, out var count),
                "GetImageInfo");

            if (imageInfo == IntPtr.Zero || count == 0)
            {
                return (IReadOnlyList<DismImageEntry>)[];
            }

            try
            {
                return DismApi.MarshalArray<DismApi.DISM_IMAGE_INFO>(imageInfo, count)
                    .Select(info => new DismImageEntry(
                        (int)info.ImageIndex,
                        Marshal.PtrToStringUni(info.ImageName) ?? string.Empty))
                    .ToArray();
            }
            finally
            {
                _ = DismApi.DismDelete(imageInfo);
            }
        });

        _logService.LogInformation($"DismGetImageInfo: {entries.Count} image(s) in {imagePath}");
        return entries;
    }
}

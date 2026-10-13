using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Win32.Foundation;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Native;

namespace Winhance.Infrastructure.Features.WimUtil.Services;

// WIMGAPI rather than DISM: WIMExtractImagePath copies single files out of an image, where DISM would mount
// the whole of boot.wim to read a few megabytes, and leave a stale mount behind if anything interrupts it.
internal sealed class WimgApiFileExtractor(IFileSystemService fileSystemService, ILogService logService) : IWimFileExtractor
{
    public IReadOnlyList<string> Extract(
        string wimPath,
        int imageIndex,
        IReadOnlyDictionary<string, string> destinationByImagePath)
    {
        var wim = WimgApi.WIMCreateFile(wimPath, WimgApi.WIM_GENERIC_READ, WimgApi.WIM_OPEN_EXISTING, 0, 0, out _);
        if (wim == 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), $"Could not open {wimPath}.");
        }

        try
        {
            // WIMLoadImage fails without a scratch folder for the image's metadata.
            if (!WimgApi.WIMSetTemporaryPath(wim, fileSystemService.GetTempPath()))
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError(), $"Could not set a temporary folder for {wimPath}.");
            }

            var image = WimgApi.WIMLoadImage(wim, (uint)imageIndex);
            if (image == 0)
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError(), $"Could not load image {imageIndex} of {wimPath}.");
            }

            try
            {
                return ExtractEach(image, destinationByImagePath);
            }
            finally
            {
                _ = WimgApi.WIMCloseHandle(image);
            }
        }
        finally
        {
            _ = WimgApi.WIMCloseHandle(wim);
        }
    }

    private List<string> ExtractEach(nint image, IReadOnlyDictionary<string, string> destinationByImagePath)
    {
        var extracted = new List<string>();
        foreach (var (imagePath, destination) in destinationByImagePath)
        {
            var folder = fileSystemService.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(folder))
            {
                fileSystemService.CreateDirectory(folder);
            }

            if (WimgApi.WIMExtractImagePath(image, imagePath, destination, 0))
            {
                extracted.Add(imagePath);
                continue;
            }

            var error = Marshal.GetLastPInvokeError();
            if (error is not ((int)WIN32_ERROR.ERROR_FILE_NOT_FOUND or (int)WIN32_ERROR.ERROR_PATH_NOT_FOUND))
            {
                throw new Win32Exception(error, $"Could not extract {imagePath}.");
            }

            logService.LogInformation($"{imagePath} is not in the image");
        }

        return extracted;
    }
}

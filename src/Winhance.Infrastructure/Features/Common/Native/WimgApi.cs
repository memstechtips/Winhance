using System.Runtime.InteropServices;

namespace Winhance.Infrastructure.Features.Common.Native;

// wimgapi.dll ships in System32, but its header and import library ship only in the ADK, so the Win32
// metadata has no entry for it and CsWin32 cannot generate it.
internal static partial class WimgApi
{
    private const string Library = "wimgapi.dll";

    internal const uint WIM_GENERIC_READ = 0x80000000;
    internal const uint WIM_OPEN_EXISTING = 3;

    [LibraryImport(Library, EntryPoint = "WIMCreateFile", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint WIMCreateFile(
        string wimPath,
        uint desiredAccess,
        uint creationDisposition,
        uint flagsAndAttributes,
        uint compressionType,
        out uint creationResult);

    [LibraryImport(Library, EntryPoint = "WIMSetTemporaryPath", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool WIMSetTemporaryPath(nint wim, string path);

    [LibraryImport(Library, EntryPoint = "WIMLoadImage", SetLastError = true)]
    internal static partial nint WIMLoadImage(nint wim, uint imageIndex);

    [LibraryImport(Library, EntryPoint = "WIMExtractImagePath", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool WIMExtractImagePath(nint image, string imagePath, string destinationPath, uint extractFlags);

    [LibraryImport(Library, EntryPoint = "WIMCloseHandle", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool WIMCloseHandle(nint handle);
}

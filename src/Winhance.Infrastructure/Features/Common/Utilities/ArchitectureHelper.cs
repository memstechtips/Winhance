using System.Runtime.InteropServices;

namespace Winhance.Infrastructure.Features.Common.Utilities;

/// <summary>
/// Shared architecture detection logic used by StoreDownloadService and WinGetInstaller.
/// Uses OSArchitecture to detect the native OS architecture.
/// </summary>
internal static class ArchitectureHelper
{
    public static string GetCurrentArchitecture()
    {
        var arch = RuntimeInformation.OSArchitecture;
        return arch switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "arm64",
            Architecture.Arm => "arm",
            _ => "x64"
        };
    }
}

namespace Winhance.Infrastructure.Features.Common.Utilities;

// DISM and msiexec exit 3010 (ERROR_SUCCESS_REBOOT_REQUIRED) for an install that worked and wants a restart.
internal static class InstallerExitCode
{
    public const int RebootRequired = 3010;

    public static bool IsSuccess(int exitCode) => exitCode is 0 or RebootRequired;
}

using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Security;

namespace Winhance.Infrastructure.Features.Common.Utilities;

internal static class ProcessPrivilege
{
    public static void Enable(string privilege) => Adjust(privilege, true);

    // Backup and restore reach every file and key on the machine, so they stay on only while the call that needs them runs.
    public static IDisposable Hold(params string[] privileges)
    {
        var scope = new Scope(privileges);
        try
        {
            foreach (var privilege in privileges)
            {
                Adjust(privilege, true);
            }
        }
        catch
        {
            scope.Dispose();
            throw;
        }

        return scope;
    }

    // Administrators hold these privileges disabled. When the token lacks one, AdjustTokenPrivileges still succeeds
    // and the call that needs it fails on its own, which the caller logs like any other failure.
    private static unsafe void Adjust(string privilege, bool enable)
    {
        if (!PInvoke.OpenProcessToken(
                PInvoke.GetCurrentProcess_SafeHandle(),
                TOKEN_ACCESS_MASK.TOKEN_ADJUST_PRIVILEGES | TOKEN_ACCESS_MASK.TOKEN_QUERY,
                out var token))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not open the process token.");
        }

        using (token)
        {
            if (!PInvoke.LookupPrivilegeValue(null, privilege, out var luid))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not look up {privilege}.");
            }

            var privileges = new TOKEN_PRIVILEGES { PrivilegeCount = 1 };
            privileges.Privileges[0] = new LUID_AND_ATTRIBUTES
            {
                Luid = luid,
                Attributes = enable ? TOKEN_PRIVILEGES_ATTRIBUTES.SE_PRIVILEGE_ENABLED : default,
            };

            if (!PInvoke.AdjustTokenPrivileges(token, false, &privileges, 0, null, null))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not {(enable ? "enable" : "disable")} {privilege}.");
            }
        }
    }

    private sealed class Scope(string[] privileges) : IDisposable
    {
        public void Dispose()
        {
            foreach (var privilege in privileges)
            {
                Adjust(privilege, false);
            }
        }
    }
}

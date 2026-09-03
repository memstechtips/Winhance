namespace Winhance.Core.Features.Common.Interfaces;

// Direct registry + WMI (behind IWmiApi); no PowerShell hosting.
public interface ISystemRestoreService
{
    // Source of truth is the REG_MULTI_SZ at HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SPP\Clients under the
    // System Restore client GUID {09F7EDC5-294E-4180-AF6A-FB0E6A0E9513} - the key sysdm.cpl and
    // Enable-/Disable-ComputerRestore use; it updates synchronously with the toggle, before any restore point exists.
    // HKLM\SOFTWARE\Policies\Microsoft\Windows NT\SystemRestore\DisableSR=1 is a group-policy override that forces
    // SR off; honoured here. False on any read error.
    bool IsEnabledForC();
}

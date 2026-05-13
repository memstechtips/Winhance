namespace Winhance.Core.Features.Common.Interfaces;

/// <summary>
/// Reads the current System Protection (System Restore) state on the system drive.
/// Backed by direct registry + ManagementObjectSearcher calls — no PowerShell hosting required.
/// Shared by SystemSettingsDiscoveryService (for the system-restore-protection toggle)
/// and SystemBackupService (gate before creating a restore point).
/// </summary>
public interface ISystemRestoreService
{
    /// <summary>
    /// Returns true if System Protection is currently enabled for C: drive.
    ///
    /// Source of truth is the REG_MULTI_SZ at
    /// HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SPP\Clients under the
    /// System Restore client GUID {09F7EDC5-294E-4180-AF6A-FB0E6A0E9513} — the
    /// same key sysdm.cpl and Enable-/Disable-ComputerRestore read/write.
    /// Updates synchronously with the toggle, so this method correctly reflects
    /// state immediately after Enable-ComputerRestore runs (before any restore
    /// point exists).
    ///
    /// HKLM\SOFTWARE\Policies\Microsoft\Windows NT\SystemRestore\DisableSR=1
    /// is a group-policy override that forces SR off system-wide regardless of
    /// the SPP entry list; honoured by this method.
    ///
    /// Returns false on any read error.
    /// </summary>
    bool IsEnabledForC();
}

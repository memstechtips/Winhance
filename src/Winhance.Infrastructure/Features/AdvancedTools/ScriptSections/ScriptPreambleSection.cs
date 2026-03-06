using System.Text;

namespace Winhance.Infrastructure.Features.AdvancedTools.ScriptSections;

/// <summary>
/// Emits the script header, logging setup, helper functions, and Start-ProcessAsUser.
/// These are entirely static string emission with no instance state.
/// </summary>
internal static class ScriptPreambleSection
{
    public static void AppendHeader(StringBuilder sb)
    {
        sb.AppendLine($@"<#
.SYNOPSIS
    Winhance Windows 10/11 Customization and Optimization Script
.DESCRIPTION
    Applies registry settings, UWP app removals, optimizations and customizations based on Windows version detection
.NOTES
    Requires Administrator privileges
    Compatible with Windows 10 and Windows 11
    Logs all activities to C:\ProgramData\Winhance\Unattend\Logs\Winhancements.txt
.PARAMETER UserCustomizations
    When specified, applies ONLY HKCU (user-specific) registry settings.
    When not specified, applies all settings EXCEPT HKCU entries.
    Note: User customizations are tracked and will only apply once per user.
    To re-apply, delete: HKCU\Software\Winhance\UserCustomizationsApplied
.EXAMPLE
    .\Winhancements.ps1
    Runs in normal mode - applies all system-wide settings (HKLM) but skips user settings (HKCU)
.EXAMPLE
    .\Winhancements.ps1 -UserCustomizations
    Runs in user mode - applies ONLY user-specific settings (HKCU)
#>

param(
    [switch]$UserCustomizations
)");
    }

    public static void AppendLoggingSetup(StringBuilder sb)
    {
        sb.AppendLine(@"
# ============================================================================
# LOGGING SETUP
# ============================================================================

$LogPath = 'C:\ProgramData\Winhance\Unattend\Logs\Winhancements.txt'
$null = New-Item -Path (Split-Path $LogPath) -ItemType Directory -Force

function Write-Log {
    param(
        [string]$Message,
        [ValidateSet(""INFO"", ""SUCCESS"", ""WARNING"", ""ERROR"")]
        [string]$Level = ""INFO""
    )

    $Timestamp = Get-Date -Format ""yyyy-MM-dd HH:mm:ss""
    $LogEntry = ""[$Timestamp] [$Level] $Message""

    # Write to log file
    Add-Content -Path $LogPath -Value $LogEntry -Encoding UTF8

    # Optional: Also write to console for real-time monitoring
    # Uncomment the next line if you want console output during testing
    # Write-Host $LogEntry
}

# Initialize log file
Write-Log ""================================================================================="" ""INFO""
Write-Log ""Winhance Windows Optimization & Customization Script Started"" ""INFO""
Write-Log ""Script Path: $($MyInvocation.MyCommand.Path)"" ""INFO""
Write-Log ""Log File: $LogPath"" ""INFO""
if ($UserCustomizations) {
    Write-Log ""MODE: User Customizations Only (HKCU registry entries)"" ""INFO""
} else {
    Write-Log ""MODE: System Customizations (All settings except HKCU entries)"" ""INFO""
}
Write-Log ""================================================================================="" ""INFO""
");
    }

    public static void AppendHelperFunctions(StringBuilder sb)
    {
        sb.AppendLine(@"
function Get-TargetUser {
    try {
        $user = Get-WmiObject Win32_ComputerSystem | Select-Object -ExpandProperty UserName
        if ($user -and $user -ne ""NT AUTHORITY\SYSTEM"") {
            $username = $user.Split('\')[1]
            if ($username -ne ""defaultuser0"") {
                return $username
            }
        }
    } catch { }

    try {
        $explorer = Get-Process explorer -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($explorer) {
            $owner = $explorer.GetOwner()
            if ($owner.User -ne ""defaultuser0"") {
                return $owner.User
            }
        }
    } catch { }

    return $null
}

function Get-UserSID {
    param($Username)
    try {
        $profListPath = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList'
        foreach ($key in Get-ChildItem $profListPath -ErrorAction SilentlyContinue) {
            $profPath = (Get-ItemProperty $key.PSPath -ErrorAction SilentlyContinue).ProfileImagePath
            if ($profPath -and $profPath.EndsWith(""\$Username"")) {
                return $key.PSChildName
            }
        }
        return $null
    } catch {
        return $null
    }
}

function Set-RegistryValue {
    param(
        [string]$Path,
        [string]$Name,
        [string]$Type,
        $Value,
        [string]$Description
    )

    try {
        if (-not (Test-Path $Path)) {
            New-Item -Path $Path -Force | Out-Null
        }
        Set-ItemProperty -Path $Path -Name $Name -Value $Value -Type $Type -Force
        Write-Log ""$Description | $Path\$Name = $Value"" ""SUCCESS""
    }
    catch {
        Write-Log ""Failed to set $Path\$Name : $($_.Exception.Message)"" ""ERROR""
    }
}

function Remove-RegistryValue {
    param(
        [string]$Path,
        [string]$Name,
        [string]$Description
    )

    try {
        if (Test-Path $Path) {
            $existingValue = Get-ItemProperty -Path $Path -Name $Name -ErrorAction SilentlyContinue
            if ($existingValue) {
                Remove-ItemProperty -Path $Path -Name $Name -ErrorAction SilentlyContinue
                Write-Log ""$Description | Removed $Path\$Name"" ""SUCCESS""
            }
        }
    }
    catch {
        Write-Log ""Failed to remove $Path\$Name : $($_.Exception.Message)"" ""ERROR""
    }
}

function Remove-RegistryKey {
    param(
        [string]$Path,
        [string]$Description
    )

    try {
        if (Test-Path $Path) {
            Remove-Item -Path $Path -Recurse -Force -ErrorAction SilentlyContinue
            Write-Log ""$Description | Removed key $Path"" ""SUCCESS""
        }
    }
    catch {
        Write-Log ""Failed to remove key $Path : $($_.Exception.Message)"" ""ERROR""
    }
}

function New-RegistryKey {
    param(
        [string]$Path,
        [string]$Description
    )

    try {
        if (-not (Test-Path $Path)) {
            New-Item -Path $Path -Force | Out-Null
            Write-Log ""$Description | Created key $Path"" ""SUCCESS""
        }
    }
    catch {
        Write-Log ""Failed to create key $Path : $($_.Exception.Message)"" ""ERROR""
    }
}

function Set-BinaryBit {
    param(
        [string]$Path,
        [string]$Name,
        [int]$ByteIndex,
        [byte]$BitMask,
        [bool]$SetBit,
        [string]$Description
    )

    try {
        if (-not (Test-Path $Path)) {
            New-Item -Path $Path -Force | Out-Null
        }

        $currentValue = Get-ItemProperty -Path $Path -Name $Name -ErrorAction SilentlyContinue
        if ($null -eq $currentValue -or $null -eq $currentValue.$Name) {
            $bytes = New-Object byte[] ([Math]::Max(12, $ByteIndex + 1))
        } else {
            $bytes = $currentValue.$Name
            if ($bytes.Length -le $ByteIndex) {
                $newBytes = New-Object byte[] ($ByteIndex + 1)
                [Array]::Copy($bytes, $newBytes, $bytes.Length)
                $bytes = $newBytes
            }
        }

        if ($SetBit) {
            $bytes[$ByteIndex] = $bytes[$ByteIndex] -bor $BitMask
        } else {
            $bytes[$ByteIndex] = $bytes[$ByteIndex] -band (-bnot $BitMask)
        }

        Set-ItemProperty -Path $Path -Name $Name -Value $bytes -Type Binary -Force
        Write-Log ""$Description | $Path\$Name bit mask 0x$($BitMask.ToString('X2')) at byte $ByteIndex = $SetBit"" ""SUCCESS""
    }
    catch {
        Write-Log ""Failed to modify binary bit $Path\$Name : $($_.Exception.Message)"" ""ERROR""
    }
}

function Set-BinaryByte {
    param(
        [string]$Path,
        [string]$Name,
        [int]$ByteIndex,
        [byte]$ByteValue,
        [string]$Description
    )

    try {
        if (-not (Test-Path $Path)) {
            New-Item -Path $Path -Force | Out-Null
        }

        $currentValue = Get-ItemProperty -Path $Path -Name $Name -ErrorAction SilentlyContinue
        if ($null -eq $currentValue -or $null -eq $currentValue.$Name) {
            $bytes = New-Object byte[] ([Math]::Max(12, $ByteIndex + 1))
        } else {
            $bytes = $currentValue.$Name
            if ($bytes.Length -le $ByteIndex) {
                $newBytes = New-Object byte[] ($ByteIndex + 1)
                [Array]::Copy($bytes, $newBytes, $bytes.Length)
                $bytes = $newBytes
            }
        }

        $bytes[$ByteIndex] = $ByteValue
        Set-ItemProperty -Path $Path -Name $Name -Value $bytes -Type Binary -Force
        Write-Log ""$Description | $Path\$Name byte $ByteIndex = 0x$($ByteValue.ToString('X2'))"" ""SUCCESS""
    }
    catch {
        Write-Log ""Failed to modify binary byte $Path\$Name : $($_.Exception.Message)"" ""ERROR""
    }
}
");
        AppendStartProcessAsUser(sb);
    }

    public static void AppendStartProcessAsUser(StringBuilder sb)
    {
        sb.AppendLine(@"
function Start-ProcessAsUser {
    param([string]$CommandLine)

    if (-not ([System.Management.Automation.PSTypeName]'Winhance.TL'.Type)) {
        Add-Type -MemberDefinition @'
[DllImport(""advapi32.dll"",SetLastError=true)]public static extern bool OpenProcessToken(IntPtr h,uint a,out IntPtr t);
[DllImport(""advapi32.dll"",SetLastError=true)]public static extern bool GetTokenInformation(IntPtr t,int c,IntPtr i,int l,out int r);
[DllImport(""advapi32.dll"",SetLastError=true)]public static extern bool DuplicateTokenEx(IntPtr t,uint a,IntPtr s,int il,int tt,out IntPtr n);
[DllImport(""advapi32.dll"",SetLastError=true,CharSet=CharSet.Unicode)]public static extern bool CreateProcessAsUserW(IntPtr t,string app,string cmd,IntPtr pa,IntPtr ta,bool ih,int cf,IntPtr env,string dir,ref SI si,out PI pi);
[DllImport(""kernel32.dll"",SetLastError=true)]public static extern bool CloseHandle(IntPtr h);
[DllImport(""kernel32.dll"")]public static extern uint WTSGetActiveConsoleSessionId();
[DllImport(""kernel32.dll"",SetLastError=true)]public static extern bool ProcessIdToSessionId(uint p,out uint s);
[DllImport(""kernel32.dll"",SetLastError=true)]public static extern uint WaitForSingleObject(IntPtr h,uint ms);
[DllImport(""kernel32.dll"",SetLastError=true)]public static extern bool GetExitCodeProcess(IntPtr h,out uint c);
[DllImport(""userenv.dll"",SetLastError=true)]public static extern bool CreateEnvironmentBlock(out IntPtr env,IntPtr token,bool inherit);
[DllImport(""userenv.dll"",SetLastError=true)]public static extern bool DestroyEnvironmentBlock(IntPtr env);
[StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]public struct SI{public int cb;public string r1,d,t;public int x,y,w,h,cc,cr,fa,fl;public short sw,r2;public IntPtr r3,i,o,e;}
[StructLayout(LayoutKind.Sequential)]public struct PI{public IntPtr hp,ht;public int pid,tid;}
'@ -Name TL -Namespace Winhance -ErrorAction Stop
    }

    $T = [Winhance.TL]
    $tok = $dup = $envBlock = [IntPtr]::Zero
    $pi = New-Object Winhance.TL+PI
    $launched = $false

    try {
        $cs = $T::WTSGetActiveConsoleSessionId()
        if ($cs -eq 0xFFFFFFFF) { Write-Log ""No active console session"" ""ERROR""; return $false }

        $ep = $null
        foreach ($p in (Get-Process explorer -ErrorAction SilentlyContinue)) {
            $s = [uint32]0
            if ($T::ProcessIdToSessionId([uint32]$p.Id, [ref]$s) -and $s -eq $cs) { $ep = $p; break }
        }
        if (-not $ep) { Write-Log ""No explorer.exe in session $cs"" ""ERROR""; return $false }

        if (-not $T::OpenProcessToken($ep.Handle, 0x000A, [ref]$tok)) {
            Write-Log ""OpenProcessToken failed (err $([Runtime.InteropServices.Marshal]::GetLastWin32Error()))"" ""ERROR""; return $false
        }

        # If user is admin with UAC, get the linked elevated token (TokenElevationType=18, TokenLinkedToken=19)
        $dupSrc = $tok; $linked = [IntPtr]::Zero
        $eb = [Runtime.InteropServices.Marshal]::AllocHGlobal(4); $rl = 0
        if ($T::GetTokenInformation($tok, 18, $eb, 4, [ref]$rl) -and [Runtime.InteropServices.Marshal]::ReadInt32($eb) -eq 3) {
            $lb = [Runtime.InteropServices.Marshal]::AllocHGlobal([IntPtr]::Size)
            if ($T::GetTokenInformation($tok, 19, $lb, [IntPtr]::Size, [ref]$rl)) {
                $linked = [Runtime.InteropServices.Marshal]::ReadIntPtr($lb)
                $dupSrc = $linked
                Write-Log ""Using elevated linked token for admin user"" ""INFO""
            }
            [Runtime.InteropServices.Marshal]::FreeHGlobal($lb)
        }
        [Runtime.InteropServices.Marshal]::FreeHGlobal($eb)

        if (-not $T::DuplicateTokenEx($dupSrc, 0xF01FF, [IntPtr]::Zero, 2, 1, [ref]$dup)) {
            Write-Log ""DuplicateTokenEx failed (err $([Runtime.InteropServices.Marshal]::GetLastWin32Error()))"" ""ERROR""; return $false
        }
        if ($linked -ne [IntPtr]::Zero) { $null = $T::CloseHandle($linked) }

        $null = $T::CreateEnvironmentBlock([ref]$envBlock, $dup, $false)

        $si = New-Object Winhance.TL+SI
        $si.cb = [Runtime.InteropServices.Marshal]::SizeOf($si)
        $si.d = ""winsta0\default""
        $psExe = ""$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe""

        if (-not $T::CreateProcessAsUserW($dup, $psExe, $CommandLine, [IntPtr]::Zero, [IntPtr]::Zero, $false, 0x08000400, $envBlock, $env:SystemRoot, [ref]$si, [ref]$pi)) {
            Write-Log ""CreateProcessAsUserW failed (err $([Runtime.InteropServices.Marshal]::GetLastWin32Error()))"" ""ERROR""; return $false
        }

        $launched = $true
        Write-Log ""Launched child process as user (PID $($pi.pid))"" ""INFO""

        $wait = $T::WaitForSingleObject($pi.hp, 600000)
        if ($wait -ne 0) { Write-Log ""Child process timed out"" ""ERROR""; return $false }

        $ec = [uint32]0
        $null = $T::GetExitCodeProcess($pi.hp, [ref]$ec)
        Write-Log ""Child process exited with code $ec"" ""INFO""
        return ($ec -eq 0)
    }
    catch { Write-Log ""Start-ProcessAsUser: $($_.Exception.Message)"" ""ERROR""; return $false }
    finally {
        if ($launched) {
            if ($pi.ht -ne [IntPtr]::Zero) { $null = $T::CloseHandle($pi.ht) }
            if ($pi.hp -ne [IntPtr]::Zero) { $null = $T::CloseHandle($pi.hp) }
        }
        if ($envBlock -ne [IntPtr]::Zero) { $null = $T::DestroyEnvironmentBlock($envBlock) }
        if ($dup -ne [IntPtr]::Zero) { $null = $T::CloseHandle($dup) }
        if ($tok -ne [IntPtr]::Zero) { $null = $T::CloseHandle($tok) }
    }
}
");
    }

    public static void AppendCompletionBlock(StringBuilder sb)
    {
        sb.AppendLine();
        sb.AppendLine("Write-Log \"================================================================================\" \"INFO\"");
        sb.AppendLine("Write-Log \"Winhance Windows Optimization & Customization Script Completed\" \"SUCCESS\"");
        sb.AppendLine("Write-Log \"================================================================================\" \"INFO\"");
    }
}

namespace Winhance.Core.Features.SoftwareApps.Models;

public static class OneDriveRemovalScript
{
    public const string ScriptVersion = "1.1";

    public static string GetScript()
    {
        return @"
<#
  .SYNOPSIS
      Removes Microsoft OneDrive from Windows 10/11 systems.
      Script Version: " + ScriptVersion + @"

  .DESCRIPTION
      This script detects and removes Microsoft OneDrive installations including:
      - OneDrive AppxPackage (Microsoft.OneDriveSync)
      - Registry-based uninstallation using HKLM and HKU uninstall entries
      - OneDrive files and folders from the current users' AppData folder. (NOTE: Userdata in %USERPROFILE%\OneDrive is preserved)
      - System-wide OneDrive installation files
      - OneDrive scheduled tasks
      - Start Menu shortcuts
      - Default user profile configuration to prevent auto-installation

      This script is designed to run in any context: user sessions, SYSTEM account, or scheduled tasks.

  .NOTES
      Source: https://github.com/memstechtips/Winhance

      Requirements:
      - Windows 10/11
      - Administrator privileges (script will auto-elevate)
      - PowerShell 5.1 or higher
#>

# Check if script is running as Administrator
If (!([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]""Administrator"")) {
    Try {
        Start-Process PowerShell.exe -ArgumentList (""-NoProfile -ExecutionPolicy Bypass -File `""{0}`"""" -f $PSCommandPath) -Verb RunAs
        Exit
    }
    Catch {
        Write-Host ""Failed to run as Administrator. Please rerun with elevated privileges.""
        Exit
    }
}

# Setup logging
$logFolder = ""C:\ProgramData\Winhance\Logs""
$logFile = ""$logFolder\OneDriveRemovalLog.txt""

# Create log directory if it doesn't exist
if (!(Test-Path $logFolder)) {
    New-Item -ItemType Directory -Path $logFolder -Force | Out-Null
}

# Function to write to log file
function Write-Log {
    param (
        [string]$Message
    )

    # Check if log file exists and is over 500KB (512000 bytes)
    if ((Test-Path $logFile) -and (Get-Item $logFile).Length -gt 512000) {
        Remove-Item $logFile -Force -ErrorAction SilentlyContinue
        $timestamp = Get-Date -Format ""yyyy-MM-dd HH:mm:ss""
        ""$timestamp - Log rotated - previous log exceeded 500KB"" | Out-File -FilePath $logFile
    }

    $timestamp = Get-Date -Format ""yyyy-MM-dd HH:mm:ss""
    ""$timestamp - $Message"" | Out-File -FilePath $logFile -Append

    # Also output to console for real-time progress
    Write-Host $Message
}

# Function to schedule file for deletion on reboot
function Schedule-DeleteOnReboot {
    param([string]$Path)

    $code = '[DllImport(""kernel32.dll"", SetLastError=true, CharSet=CharSet.Unicode)] public static extern bool MoveFileEx(string lpExistingFileName, string lpNewFileName, int dwFlags);'
    if (-not ([System.Management.Automation.PSTypeName]'Win32.Kernel32').Type) {
        Add-Type -MemberDefinition $code -Name 'Kernel32' -Namespace 'Win32' -ErrorAction SilentlyContinue
    }
    return [Win32.Kernel32]::MoveFileEx($Path, $null, 4)  # 4 = MOVEFILE_DELAY_UNTIL_REBOOT
}

Write-Host ""Starting OneDrive removal process. See $logFile for details.""
Write-Log ""Starting OneDrive removal process""

# Get the interactive user when running as SYSTEM (not needed for regular user execution)
function Get-TargetUser {
    Write-Log ""Get-TargetUser: Starting user detection""

    try {
        $user = Get-WmiObject Win32_ComputerSystem | Select-Object -ExpandProperty UserName
        Write-Log ""Get-TargetUser: Win32_ComputerSystem returned: '$user'""
        if ($user -and $user -ne ""NT AUTHORITY\SYSTEM"") {
            $username = $user.Split('\')[1]
            Write-Log ""Get-TargetUser: Extracted username: '$username'""
            return $username
        }
        Write-Log ""Get-TargetUser: User is null or SYSTEM, trying fallback method""
    }
    catch {
        Write-Log ""Get-TargetUser: Win32_ComputerSystem failed: $($_.Exception.Message)""
    }

    # Fallback: find user running explorer.exe
    try {
        $explorer = Get-Process explorer -ErrorAction SilentlyContinue | Select-Object -First 1
        Write-Log ""Get-TargetUser: Explorer process found: $($explorer -ne $null)""
        if ($explorer) {
            $owner = $explorer.GetOwner()
            Write-Log ""Get-TargetUser: Explorer owner: Domain='$($owner.Domain)', User='$($owner.User)'""
            return $owner.User
        }
        Write-Log ""Get-TargetUser: No explorer process found""
    }
    catch {
        Write-Log ""Get-TargetUser: Explorer method failed: $($_.Exception.Message)""
    }

    Write-Log ""Get-TargetUser: No user found, returning null""
    return $null
}

# Get the user's SID for registry access 
function Get-UserSID {
    param($Username)
    try {
        $user = New-Object System.Security.Principal.NTAccount($Username)
        return $user.Translate([System.Security.Principal.SecurityIdentifier]).Value
    }
    catch {
        Write-Log ""Get-UserSID: Failed for user '$Username': $($_.Exception.Message)""
        return $null
    }
}

# Determine user profile to check
Write-Log ""Current environment: USERNAME='$env:USERNAME', USERPROFILE='$env:USERPROFILE'""

if ($env:USERNAME -eq ""SYSTEM"" -or $env:USERNAME -like ""*$"" -or $env:USERPROFILE -like ""*\system32\config\systemprofile"") {
    Write-Log ""Running as SYSTEM, attempting to detect target user""
    $targetUser = Get-TargetUser
    if ($targetUser) {
        $userProfilePath = ""C:\Users\$targetUser""
        Write-Log ""Running as SYSTEM, targeting user: '$targetUser', profile path: '$userProfilePath'""
    } else {
        Write-Log ""Running as SYSTEM but no target user found""
        $userProfilePath = $null
    }
} else {
    $targetUser = $env:USERNAME
    $userProfilePath = $env:USERPROFILE
    Write-Log ""Running as regular user: '$targetUser', profile path: '$userProfilePath'""
}

# Step 1: Remove OneDrive AppxPackage and check registry for OneDrive installation
Write-Log ""Removing OneDrive AppxPackage if present""
try {
    Get-AppxPackage -AllUsers *OneDriveSync* | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue
    Write-Log ""OneDrive AppxPackage removal completed""
}
catch {
    Write-Log ""AppxPackage removal failed or not found: $($_.Exception.Message)""
}

$uninstallExecuted = $false

# Check HKLM first (system-wide installation)
$hklmUninstallKey = ""HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\OneDriveSetup.exe""
Write-Log ""Checking HKLM uninstall registry key: $hklmUninstallKey""

try {
    $uninstallString = reg.exe query $hklmUninstallKey /v UninstallString 2>$null
    if ($LASTEXITCODE -eq 0 -and $uninstallString) {
        $uninstallLine = $uninstallString | Where-Object { $_ -match ""UninstallString"" } | Select-Object -First 1
        if ($uninstallLine -match ""REG_SZ\s+(.+)"") {
            $uninstallCommand = $matches[1].Trim()
            Write-Log ""Found HKLM uninstall command: $uninstallCommand""

            Write-Log ""Stopping OneDrive processes""
            Stop-Process -Name ""*OneDrive*"" -Force -ErrorAction SilentlyContinue | Out-Null

            Write-Log ""Executing HKLM registry-based uninstaller""

            if ($uninstallCommand -match '^""([^""]+)""(.*)') {
                $exePath = $matches[1]
                $arguments = $matches[2].Trim()
                Write-Log ""Command: '$exePath' Arguments: '$arguments'""
                Start-Process -FilePath $exePath -ArgumentList $arguments -WindowStyle Hidden -Wait | Out-Null
            } else {
                Write-Log ""Command: '$uninstallCommand'""
                cmd.exe /c $uninstallCommand 2>&1 | Out-Null
            }
            Write-Log ""HKLM registry-based uninstaller completed""
            $uninstallExecuted = $true
        } else {
            Write-Log ""Could not parse UninstallString from HKLM registry output""
        }
    } else {
        Write-Log ""OneDrive uninstall registry key not found in HKLM""
    }
}
catch {
    Write-Log ""HKLM registry-based uninstall failed: $($_.Exception.Message)""
}

# Check HKU if HKLM didn't execute (user-specific installation)
if (-not $uninstallExecuted -and $targetUser) {
    $userSID = Get-UserSID -Username $targetUser
    if ($userSID) {
        Write-Log ""User SID for '$targetUser': $userSID""

        $uninstallKey = ""HKU\$userSID\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\OneDriveSetup.exe""
        Write-Log ""Checking HKU uninstall registry key: $uninstallKey""

        try {
            $uninstallString = reg.exe query $uninstallKey /v UninstallString 2>$null
            if ($LASTEXITCODE -eq 0 -and $uninstallString) {
                $uninstallLine = $uninstallString | Where-Object { $_ -match ""UninstallString"" } | Select-Object -First 1
                if ($uninstallLine -match ""REG_SZ\s+(.+)"") {
                    $uninstallCommand = $matches[1].Trim()
                    Write-Log ""Found HKU uninstall command: $uninstallCommand""

                    Write-Log ""Stopping OneDrive processes""
                    Stop-Process -Name ""*OneDrive*"" -Force -ErrorAction SilentlyContinue | Out-Null

                    Write-Log ""Executing HKU registry-based uninstaller""

                    if ($uninstallCommand -match '^""([^""]+)""(.*)') {
                        $exePath = $matches[1]
                        $arguments = $matches[2].Trim()
                        Write-Log ""Command: '$exePath' Arguments: '$arguments'""
                        Start-Process -FilePath $exePath -ArgumentList $arguments -WindowStyle Hidden -Wait | Out-Null
                    } else {
                        Write-Log ""Command: '$uninstallCommand'""
                        cmd.exe /c $uninstallCommand 2>&1 | Out-Null
                    }
                    Write-Log ""HKU registry-based uninstaller completed""
                } else {
                    Write-Log ""Could not parse UninstallString from HKU registry output""
                }
            } else {
                Write-Log ""OneDrive uninstall registry key not found in HKU""
            }
        }
        catch {
            Write-Log ""HKU registry-based uninstall failed: $($_.Exception.Message)""
        }
    } else {
        Write-Log ""Could not get user SID for '$targetUser'""
    }
}

# Step 3: Always run cleanup tasks
Write-Log ""Starting cleanup tasks""

# 3.1: Delete OneDrive registry keys
$hklmUninstallKey = ""HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\OneDriveSetup.exe""
Write-Log ""Deleting HKLM OneDrive uninstall registry key: $hklmUninstallKey""
reg.exe delete $hklmUninstallKey /f 2>&1 | Out-Null
if ($LASTEXITCODE -eq 0) {
    Write-Log ""HKLM registry key deleted successfully""
} else {
    Write-Log ""HKLM registry key not found or already deleted""
}

if ($targetUser -and $userSID) {
    $uninstallKey = ""HKU\$userSID\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\OneDriveSetup.exe""
    Write-Log ""Deleting HKU OneDrive uninstall registry key: $uninstallKey""
    reg.exe delete $uninstallKey /f 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) {
        Write-Log ""HKU registry key deleted successfully""
    } else {
        Write-Log ""HKU registry key not found or already deleted""
    }
}

# 3.1b: Delete OneDrive main registry keys
Write-Log ""Deleting OneDrive configuration registry keys""

$systemRegistryPaths = @(
    ""HKLM\SOFTWARE\Microsoft\OneDrive"",
    ""HKLM\SOFTWARE\WOW6432Node\Microsoft\OneDrive""
)

foreach ($path in $systemRegistryPaths) {
    Write-Log ""Deleting registry key: $path""
    reg.exe delete $path /f 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) {
        Write-Log ""Registry key deleted successfully: $path""
    } else {
        Write-Log ""Registry key not found or already deleted: $path""
    }
}

if ($targetUser) {
    if (-not $userSID) {
        $userSID = Get-UserSID -Username $targetUser
    }

    if ($userSID) {
        Write-Log ""Deleting user-specific OneDrive registry keys for user: $targetUser (SID: $userSID)""
        $userRegistryPaths = @(
            ""HKU\$userSID\SOFTWARE\Microsoft\OneDrive"",
            ""HKU\$userSID\SOFTWARE\WOW6432Node\Microsoft\OneDrive""
        )

        foreach ($path in $userRegistryPaths) {
            Write-Log ""Deleting registry key: $path""
            reg.exe delete $path /f 2>&1 | Out-Null
            if ($LASTEXITCODE -eq 0) {
                Write-Log ""Registry key deleted successfully: $path""
            } else {
                Write-Log ""Registry key not found or already deleted: $path""
            }
        }
    } else {
        Write-Log ""Could not get user SID for '$targetUser', skipping user-specific registry cleanup""
    }
}

# 3.2: Delete OneDrive AppData folder
if ($userProfilePath) {
    $currentUserOneDrivePath = Join-Path $userProfilePath ""AppData\Local\Microsoft\OneDrive""
    Write-Log ""Checking OneDrive AppData folder: $currentUserOneDrivePath""

    if (Test-Path $currentUserOneDrivePath) {
        Write-Log ""Removing OneDrive folder for user: $targetUser""
        try {
            takeown /f $currentUserOneDrivePath /r /d y 2>&1 | Out-Null
            icacls $currentUserOneDrivePath /grant ""${env:USERNAME}:F"" /t 2>&1 | Out-Null
            Remove-Item $currentUserOneDrivePath -Recurse -Force -ErrorAction SilentlyContinue
            Write-Log ""OneDrive folder removed for user: $targetUser""
        }
        catch {
            Write-Log ""Failed to remove OneDrive folder for user: $targetUser - $($_.Exception.Message)""
        }
    } else {
        Write-Log ""OneDrive AppData folder not found""
    }
}

# 3.3: Delete OneDrive Start Menu entry
if ($userProfilePath) {
    $startMenuPath = Join-Path $userProfilePath ""AppData\Roaming\Microsoft\Windows\Start Menu\Programs\OneDrive.lnk""
    Write-Log ""Checking OneDrive Start Menu shortcut: $startMenuPath""

    if (Test-Path $startMenuPath) {
        Remove-Item $startMenuPath -Force -ErrorAction SilentlyContinue
        Write-Log ""OneDrive Start Menu shortcut removed""
    } else {
        Write-Log ""OneDrive Start Menu shortcut not found""
    }
}

if (Test-Path ""C:\ProgramData\Microsoft\Windows\Start Menu\Programs\OneDrive.lnk"") {
    Remove-Item ""C:\ProgramData\Microsoft\Windows\Start Menu\Programs\OneDrive.lnk"" -Force -ErrorAction SilentlyContinue
    Write-Log ""System-wide Start Menu shortcut removed""
}

# 3.4: Delete system OneDrive files
$systemPaths = @(
    ""C:\Windows\System32\OneDriveSetup.exe"",
    ""C:\Windows\SysWOW64\OneDriveSetup.exe"",
    ""C:\Program Files\Microsoft OneDrive"",
    ""C:\ProgramData\Microsoft OneDrive""
)

foreach ($path in $systemPaths) {
    if (Test-Path $path) {
        Write-Log ""Removing: $path""
        if (Test-Path $path -PathType Container) {
            takeown /f $path /r /d y 2>&1 | Out-Null
            icacls $path /grant ""${env:USERNAME}:F"" /t 2>&1 | Out-Null
        } else {
            takeown /f $path 2>&1 | Out-Null
            icacls $path /grant ""${env:USERNAME}:F"" 2>&1 | Out-Null
        }
        Remove-Item $path -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$remaining = $systemPaths | Where-Object { Test-Path $_ }
if ($remaining) {
    Write-Log ""Some files locked, scheduling for deletion on reboot""
    foreach ($path in $remaining) {
        if (Test-Path $path -PathType Container) {
            Get-ChildItem $path -Recurse -File -Force -ErrorAction SilentlyContinue | ForEach-Object {
                Schedule-DeleteOnReboot $_.FullName | Out-Null
            }
        } else {
            Schedule-DeleteOnReboot $path | Out-Null
        }
        Write-Log ""Scheduled for reboot deletion: $path""
    }
}

# 3.5: Delete OneDrive scheduled tasks
Write-Log ""Checking for OneDrive scheduled tasks""
try {
    $oneDriveTasks = Get-ScheduledTask -TaskName ""*OneDrive*"" -ErrorAction SilentlyContinue
    if ($oneDriveTasks) {
        foreach ($task in $oneDriveTasks) {
            # Skip the OneDriveRemoval task
            if ($task.TaskName -eq ""OneDriveRemoval"") {
                Write-Log ""Skipping OneDriveRemoval task: $($task.TaskName)""
                continue
            }
            
            Write-Log ""Found OneDrive scheduled task: $($task.TaskName) - State: $($task.State)""
            try {
                Unregister-ScheduledTask -TaskName $task.TaskName -TaskPath $task.TaskPath -Confirm:$false -ErrorAction SilentlyContinue
                Write-Log ""Deleted scheduled task: $($task.TaskName)""
            }
            catch {
                Write-Log ""Failed to delete scheduled task: $($task.TaskName) - $($_.Exception.Message)""
            }
        }
    } else {
        Write-Log ""No OneDrive scheduled tasks found""
    }
}
catch {
    Write-Log ""Failed to check scheduled tasks: $($_.Exception.Message)""
}

# 3.6: Configure default user registry to prevent OneDrive auto-install
$markerKey = ""HKLM\SOFTWARE\Winhance\OneDriveRemoval""
$markerValue = reg.exe query $markerKey /v ""DefaultUserConfigured"" 2>$null

if ($LASTEXITCODE -eq 0) {
    Write-Log ""Default user already configured on this machine, skipping this step.""
} else {
    Write-Log ""Configuring registry to prevent OneDrive auto-install for new users""
    reg.exe Load HKEY_USERS\Default ""C:\Users\Default\NTUSER.DAT"" 2>&1 | Out-Null
    reg.exe delete ""HKU\Default\SOFTWARE\Microsoft\Windows\CurrentVersion\Run"" /v ""OneDriveSetup"" /f 2>&1 | Out-Null
    reg.exe add ""HKU\Default\SOFTWARE\Microsoft\OneDrive"" /v ""EnableTHDFFeatures"" /t REG_DWORD /d ""0"" /f 2>&1 | Out-Null
    # Close regedit in case it is running so we can unload the hive
    Stop-Process -Name ""regedit"" -Force -ErrorAction SilentlyContinue
    reg.exe Unload HKEY_USERS\Default 2>&1 | Out-Null

    # Create marker to indicate this machine has been configured
    reg.exe add $markerKey /v ""DefaultUserConfigured"" /t REG_SZ /d ""$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"" /f 2>&1 | Out-Null
    Write-Log ""Default user configuration completed and marked""
}

Write-Log ""Done.""
Write-Host ""Done. See $logFile for details.""

";
    }
}

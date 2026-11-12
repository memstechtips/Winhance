namespace Winhance.Core.Features.SoftwareApps.Models;

public static class OneDriveRemovalScript
{
    public const string ScriptName = "OneDriveRemoval";
    public const string ScriptVersion = "1.9";

    public static string GetScript()
    {
        return @"
<#
  .SYNOPSIS
      Removes Microsoft OneDrive from Windows 10/11 systems.
      Script Version: " + ScriptVersion + @"

  .DESCRIPTION
      Detects and removes:
      - OneDrive AppxPackage (Microsoft.OneDriveSync)
      - Registry-based uninstallation using the HKLM uninstall entries, plus the HKCU entry when run as the signed-in user
      - OneDrive files and folders from every user profile's AppData folder. (NOTE: Userdata in %USERPROFILE%\OneDrive is preserved)
      - System-wide OneDrive installation files
      - OneDrive scheduled tasks
      - Start Menu shortcuts
      - Default user profile configuration to prevent auto-installation
      Runs in any context: user sessions, SYSTEM account, or scheduled tasks.
      With -Image it cleans a mounted offline Windows image instead: the hive roots are already-loaded registry paths.

  .NOTES
      Source: https://github.com/memstechtips/Winhance
      Requires Windows 10/11, PowerShell 5.1+ and Administrator privileges (auto-elevates).
#>

param([string]$Image = '',
      [string]$SoftwareRoot = 'HKEY_LOCAL_MACHINE\SOFTWARE',
      [string]$UserRoot = 'HKEY_CURRENT_USER')

If (-not $Image -and !([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]'Administrator')) {
    Try { Start-Process PowerShell.exe -ArgumentList ('-NoProfile -ExecutionPolicy Bypass -File ""{0}""' -f $PSCommandPath) -Verb RunAs }
    Catch { Write-Host 'Failed to run as Administrator. Please rerun with elevated privileges.' }
    Exit
}

$Offline = $Image -ne ''
$logFolder = 'C:\ProgramData\Winhance\Logs'
$logFile = ""$logFolder\OneDriveRemovalLog.txt""
$logHint = if ($Offline) { '' } else { "" See $logFile for details."" }
if (-not $Offline) { New-Item -ItemType Directory -Path $logFolder -Force | Out-Null }
$root = if ($Offline) { $Image.TrimEnd('\') } else { $env:SystemDrive }

function Write-Log([string]$Message) {
    if ($Offline) { Write-Host $Message; return }
    $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    if ((Test-Path $logFile) -and (Get-Item $logFile).Length -gt 512000) {
        Remove-Item $logFile -Force -ErrorAction SilentlyContinue
        ""$timestamp - Log rotated - previous log exceeded 500KB"" | Out-File -FilePath $logFile
    }
    ""$timestamp - $Message"" | Out-File -FilePath $logFile -Append
    Write-Host $Message
}

# A mounted image has no reboot to wait for, so only a live run queues deletions
function Schedule-DeleteOnReboot([string]$Path) {
    if (-not ([System.Management.Automation.PSTypeName]'Win32.Kernel32').Type) {
        Add-Type -MemberDefinition '[DllImport(""kernel32.dll"", SetLastError=true, CharSet=CharSet.Unicode)] public static extern bool MoveFileEx(string lpExistingFileName, string lpNewFileName, int dwFlags);' -Name 'Kernel32' -Namespace 'Win32' -ErrorAction SilentlyContinue
    }
    [Win32.Kernel32]::MoveFileEx($Path, [NullString]::Value, 4)  # 4 = MOVEFILE_DELAY_UNTIL_REBOOT
}

Write-Host ""Starting OneDrive removal process.$logHint""
Write-Log 'Starting OneDrive removal process'

# SYSTEM must never run a command read from a user's registry hive
$runningAsSystem = -not $Offline -and ($env:USERNAME -eq 'SYSTEM' -or $env:USERNAME -like '*$' -or $env:USERPROFILE -like '*\system32\config\systemprofile')

# Step 1: Remove OneDrive AppxPackage and check registry for OneDrive installation
if (-not $Offline) {
    Write-Log 'Removing OneDrive AppxPackage if present'
    try { Get-AppxPackage -AllUsers *OneDriveSync* | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue }
    catch { Write-Log ""AppxPackage removal failed or not found: $($_.Exception.Message)"" }
}

function Invoke-RegistryUninstall([string]$Key, [string]$Source) {
    Write-Log ""Checking $Source uninstall registry key: $Key""
    try {
        $output = reg.exe query $Key /v UninstallString 2>$null
        $line = if ($LASTEXITCODE -eq 0) { $output | Where-Object { $_ -match 'UninstallString' } | Select-Object -First 1 }
        if ($line -match 'REG_SZ\s+(.+)') {
            $command = $matches[1].Trim()
            Write-Log ""Found $Source uninstall command: $command""
            Write-Log 'Stopping OneDrive processes'
            Stop-Process -Name '*OneDrive*' -Force -ErrorAction SilentlyContinue | Out-Null
            if ($command -match '^""([^""]+)""(.*)') {
                Start-Process -FilePath $matches[1] -ArgumentList $matches[2].Trim() -WindowStyle Hidden -Wait | Out-Null
            } else {
                cmd.exe /c $command 2>&1 | Out-Null
            }
            Write-Log ""$Source registry-based uninstaller completed""
            return $true
        }
        Write-Log ""OneDrive uninstall string not found or not parsable in $Key""
    }
    catch { Write-Log ""$Source registry-based uninstall failed: $($_.Exception.Message)"" }
    return $false
}

# Check HKLM (system-wide installation) in both registry views
$hklmUninstallKeys = ""$SoftwareRoot\Microsoft\Windows\CurrentVersion\Uninstall\OneDriveSetup.exe"",
    ""$SoftwareRoot\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\OneDriveSetup.exe""
$uninstallExecuted = $false
foreach ($key in $hklmUninstallKeys | Where-Object { -not $Offline }) {
    if (Invoke-RegistryUninstall $key 'HKLM') { $uninstallExecuted = $true }
}

# A per-user install is only uninstalled by its own user. As SYSTEM, the cleanup below removes it.
if (-not $uninstallExecuted -and -not $Offline -and -not $runningAsSystem) {
    Invoke-RegistryUninstall 'HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\OneDriveSetup.exe' 'HKCU' | Out-Null
}

# Step 3: Always run cleanup tasks
Write-Log 'Starting cleanup tasks'
if (-not $Offline) { Stop-Process -Name '*OneDrive*' -Force -ErrorAction SilentlyContinue }

# 3.1: Delete OneDrive registry keys
foreach ($key in $hklmUninstallKeys + ""$SoftwareRoot\Microsoft\OneDrive"", ""$SoftwareRoot\WOW6432Node\Microsoft\OneDrive"") {
    reg.exe delete $key /f 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) { Write-Log ""Deleted registry key: $key"" }
}

# 3.1c: Delete the per-user OneDrive keys of every profile, loading the hives of users who are not signed in
function Remove-OneDriveUserKeys([string]$Hive) {
    foreach ($subKey in 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\OneDriveSetup.exe', 'SOFTWARE\Microsoft\OneDrive', 'SOFTWARE\WOW6432Node\Microsoft\OneDrive') {
        reg.exe delete ""$Hive\$subKey"" /f 2>&1 | Out-Null
        if ($LASTEXITCODE -eq 0) { Write-Log ""Deleted registry key: $Hive\$subKey"" }
    }
}

if ($Offline) {
    Remove-OneDriveUserKeys $UserRoot
} else {
    $loadedSids = @(Get-ChildItem Registry::HKEY_USERS -ErrorAction SilentlyContinue |
        Where-Object { $_.PSChildName -match '^S-1-5-21-' -and $_.PSChildName -notlike '*_Classes' } |
        ForEach-Object { $_.PSChildName })
    foreach ($sid in $loadedSids) { Remove-OneDriveUserKeys ""HKU\$sid"" }

    $profileList = Get-ChildItem 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList' -ErrorAction SilentlyContinue |
        Where-Object { $_.PSChildName -match '^S-1-5-21-' -and $_.PSChildName -notin $loadedSids }
    foreach ($entry in $profileList) {
        $hive = Join-Path (Get-ItemProperty $entry.PSPath).ProfileImagePath 'NTUSER.DAT'
        if (-not (Test-Path $hive)) { continue }
        reg.exe load HKU\WinhanceTemp $hive 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) { Write-Log ""Could not load $hive""; continue }
        Remove-OneDriveUserKeys 'HKU\WinhanceTemp'
        [gc]::Collect()
        reg.exe unload HKU\WinhanceTemp 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) { Write-Log ""Could not unload $hive"" }
    }
}

# 3.2: Delete OneDrive AppData folders and Start Menu entries of every user profile, including Default
$userProfiles = Get-ChildItem -Path ""$root\Users"" -Directory -Force -ErrorAction SilentlyContinue | Where-Object { Test-Path -Path ""$($_.FullName)\NTUSER.DAT"" }

# A user owns their profile, so any folder on the way to OneDrive can be a link into a system folder
function Test-NoLink([string]$Path, [string]$Stop) {
    for ($p = Get-Item -LiteralPath $Path -Force; $p -and $p.FullName -ne $Stop; $p = $p.Parent) { if ($p.Attributes -band 'ReparsePoint') { return $false } }
    $true
}

foreach ($userProfile in $userProfiles) {
    foreach ($folder in ""$($userProfile.FullName)\AppData\Local\Microsoft\OneDrive"", ""$($userProfile.FullName)\AppData\Roaming\Microsoft\OneDrive"") {
        if (Test-Path $folder) {
            if (-not (Test-NoLink $folder $userProfile.FullName)) { Write-Log ""Skipped, a link leads into it: $folder""; continue }
            if ($Offline) {
                takeown /f $folder /r /d y 2>&1 | Out-Null
                icacls $folder /grant '*S-1-5-32-544:F' /t 2>&1 | Out-Null
            }
            Remove-Item $folder -Recurse -Force -ErrorAction SilentlyContinue
            if (-not $Offline -and (Test-Path $folder)) {
                # Explorer holds a shell extension DLL in here. Queue the rest for the next boot: files first, then folders deepest first, because a folder only goes once it is empty.
                $items = Get-ChildItem $folder -Recurse -Force -ErrorAction SilentlyContinue
                @($items | Where-Object { -not $_.PSIsContainer }) + @($items | Where-Object PSIsContainer | Sort-Object { $_.FullName.Length } -Descending) + (Get-Item $folder) | ForEach-Object { Schedule-DeleteOnReboot $_.FullName | Out-Null }
            }
            Write-Log $(if (Test-Path $folder) { ""Could not fully remove: $folder"" } else { ""Removed: $folder"" })
        }
    }
    $startMenuLink = ""$($userProfile.FullName)\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\OneDrive.lnk""
    if (Test-Path $startMenuLink) { Remove-Item $startMenuLink -Force -ErrorAction SilentlyContinue; Write-Log ""Removed: $startMenuLink"" }
}

$systemLink = ""$root\ProgramData\Microsoft\Windows\Start Menu\Programs\OneDrive.lnk""
if (Test-Path $systemLink) { Remove-Item $systemLink -Force -ErrorAction SilentlyContinue; Write-Log 'System-wide Start Menu shortcut removed' }

# 3.4: Delete system OneDrive files
# Users can create the ProgramData folder, so takeown and icacls would follow their links
$userMadePath = ""$root\ProgramData\Microsoft OneDrive""
$systemPaths = ""$root\Windows\System32\OneDriveSetup.exe"", ""$root\Windows\SysWOW64\OneDriveSetup.exe"", ""$root\Program Files\Microsoft OneDrive"", ""$root\Program Files (x86)\Microsoft OneDrive"", $userMadePath

foreach ($path in $systemPaths | Where-Object { Test-Path $_ }) {
    Write-Log ""Removing: $path""
    $isFolder = Test-Path $path -PathType Container
    if ($path -ne $userMadePath) {
        takeown /f $path @(if ($isFolder) { '/r', '/d', 'y' }) 2>&1 | Out-Null
        icacls $path /grant ""${env:USERNAME}:F"" @(if ($isFolder) { '/t' }) 2>&1 | Out-Null
    }
    Remove-Item $path -Recurse -Force -ErrorAction SilentlyContinue
}

$remaining = $systemPaths | Where-Object { Test-Path $_ }
if ($remaining -and -not $Offline) {
    Write-Log 'Some files locked, scheduling for deletion on reboot'
    foreach ($path in $remaining) {
        $files = if (Test-Path $path -PathType Container) { (Get-ChildItem $path -Recurse -File -Force -ErrorAction SilentlyContinue).FullName } else { $path }
        foreach ($file in $files) { Schedule-DeleteOnReboot $file | Out-Null }
        Write-Log ""Scheduled for reboot deletion: $path""
    }
}

# 3.5: Delete OneDrive scheduled tasks
if (-not $Offline) {
    Write-Log 'Checking for OneDrive scheduled tasks'
    try {
        foreach ($task in Get-ScheduledTask -TaskName '*OneDrive*' -ErrorAction SilentlyContinue) {
            if ($task.TaskName -eq 'OneDriveRemoval') { Write-Log ""Skipping OneDriveRemoval task: $($task.TaskName)""; continue }
            try {
                Unregister-ScheduledTask -TaskName $task.TaskName -TaskPath $task.TaskPath -Confirm:$false -ErrorAction SilentlyContinue
                Write-Log ""Deleted scheduled task: $($task.TaskName)""
            }
            catch { Write-Log ""Failed to delete scheduled task: $($task.TaskName) - $($_.Exception.Message)"" }
        }
    }
    catch { Write-Log ""Failed to check scheduled tasks: $($_.Exception.Message)"" }
}

# 3.6: Configure default user registry to prevent OneDrive auto-install
$markerKey = ""$SoftwareRoot\Winhance\OneDriveRemoval""
$null = reg.exe query $markerKey /v DefaultUserConfigured 2>$null

if ($LASTEXITCODE -eq 0) {
    Write-Log 'Default user already configured on this machine, skipping this step.'
} else {
    Write-Log 'Configuring registry to prevent OneDrive auto-install for new users'
    $defaultUser = if ($Offline) { $UserRoot } else { 'HKU\Default' }
    if (-not $Offline) { reg.exe Load HKEY_USERS\Default 'C:\Users\Default\NTUSER.DAT' 2>&1 | Out-Null }
    reg.exe delete ""$defaultUser\SOFTWARE\Microsoft\Windows\CurrentVersion\Run"" /v OneDriveSetup /f 2>&1 | Out-Null
    reg.exe add ""$defaultUser\SOFTWARE\Microsoft\OneDrive"" /v EnableTHDFFeatures /t REG_DWORD /d 0 /f 2>&1 | Out-Null
    if (-not $Offline) {
        # Close regedit in case it is running so we can unload the hive
        Stop-Process -Name regedit -Force -ErrorAction SilentlyContinue
        reg.exe Unload HKEY_USERS\Default 2>&1 | Out-Null
    }
    reg.exe add $markerKey /v DefaultUserConfigured /t REG_SZ /d ""$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"" /f 2>&1 | Out-Null
    Write-Log 'Default user configuration completed and marked'
}

Write-Log 'Done.'
Write-Host ""Done.$logHint""
";
    }
}

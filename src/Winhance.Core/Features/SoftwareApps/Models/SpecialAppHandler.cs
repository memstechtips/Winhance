using System;
using System.IO;

namespace Winhance.Core.Features.SoftwareApps.Models;

/// <summary>
/// Represents a handler for special applications that require custom removal processes.
/// </summary>
public class SpecialAppHandler
{
    /// <summary>
    /// Gets or sets the unique identifier for the special handler type.
    /// </summary>
    public string HandlerType { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name of the application.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the application.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the script content for removing the application.
    /// </summary>
    public string RemovalScriptContent { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the scheduled task to create for preventing reinstallation.
    /// </summary>
    public string ScheduledTaskName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the application is currently installed.
    /// </summary>
    public bool IsInstalled { get; set; }

    /// <summary>
    /// Gets the path where the removal script will be saved.
    /// </summary>
    public string ScriptPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        "Winhance",
        "Scripts",
        $"{HandlerType}Removal.ps1");

    /// <summary>
    /// Gets a collection of predefined special app handlers.
    /// </summary>
    /// <returns>A collection of special app handlers.</returns>
    public static SpecialAppHandler[] GetPredefinedHandlers()
    {
        return new[]
        {
            new SpecialAppHandler
            {
                HandlerType = "Edge",
                DisplayName = "Microsoft Edge",
                Description = "Microsoft's web browser (requires special removal process)",
                RemovalScriptContent = GetEdgeRemovalScript(),
                ScheduledTaskName = "Winhance\\EdgeRemoval"
            },
            new SpecialAppHandler
            {
                HandlerType = "OneDrive",
                DisplayName = "OneDrive",
                Description = "Microsoft's cloud storage service (requires special removal process)",
                RemovalScriptContent = GetOneDriveRemovalScript(),
                ScheduledTaskName = "Winhance\\OneDriveRemoval"
            },
        };
    }

    private static string GetEdgeRemovalScript()
    {
        return @"
# EdgeRemoval.ps1
# Standalone script to remove Microsoft Edge
# Source: Winhance (https://github.com/memstechtips/Winhance)

# stop edge running
$stop = ""MicrosoftEdgeUpdate"", ""OneDrive"", ""WidgetService"", ""Widgets"", ""msedge"", ""msedgewebview2""
$stop | ForEach-Object { Stop-Process -Name $_ -Force -ErrorAction SilentlyContinue }
# uninstall copilot
Get-AppxPackage -allusers *Microsoft.Windows.Ai.Copilot.Provider* | Remove-AppxPackage
# disable edge updates regedit
reg add ""HKLM\SOFTWARE\Microsoft\EdgeUpdate"" /v ""DoNotUpdateToEdgeWithChromium"" /t REG_DWORD /d ""1"" /f | Out-Null
# allow edge uninstall regedit
reg add ""HKLM\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdateDev"" /v ""AllowUninstall"" /t REG_SZ /f | Out-Null
# new folder to uninstall edge
New-Item -Path ""$env:SystemRoot\SystemApps\Microsoft.MicrosoftEdge_8wekyb3d8bbwe"" -ItemType Directory -ErrorAction SilentlyContinue | Out-Null
# new file to uninstall edge
New-Item -Path ""$env:SystemRoot\SystemApps\Microsoft.MicrosoftEdge_8wekyb3d8bbwe"" -ItemType File -Name ""MicrosoftEdge.exe"" -ErrorAction SilentlyContinue | Out-Null
# find edge uninstall string
$regview = [Microsoft.Win32.RegistryView]::Registry32
$microsoft = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine, $regview).
OpenSubKey(""SOFTWARE\Microsoft"", $true)
$uninstallregkey = $microsoft.OpenSubKey(""Windows\CurrentVersion\Uninstall\Microsoft Edge"")
try {
    $uninstallstring = $uninstallregkey.GetValue(""UninstallString"") + "" --force-uninstall""
}
catch {
}
# uninstall edge
Start-Process cmd.exe ""/c $uninstallstring"" -WindowStyle Hidden -Wait
# remove folder file
Remove-Item -Recurse -Force ""$env:SystemRoot\SystemApps\Microsoft.MicrosoftEdge_8wekyb3d8bbwe"" -ErrorAction SilentlyContinue | Out-Null
# find edgeupdate.exe
$edgeupdate = @(); ""LocalApplicationData"", ""ProgramFilesX86"", ""ProgramFiles"" | ForEach-Object {
    $folder = [Environment]::GetFolderPath($_)
    $edgeupdate += Get-ChildItem ""$folder\Microsoft\EdgeUpdate\*.*.*.*\MicrosoftEdgeUpdate.exe"" -rec -ea 0
}
# find edgeupdate & allow uninstall regedit
$global:REG = ""HKCU:\SOFTWARE"", ""HKLM:\SOFTWARE"", ""HKCU:\SOFTWARE\Policies"", ""HKLM:\SOFTWARE\Policies"", ""HKCU:\SOFTWARE\WOW6432Node"", ""HKLM:\SOFTWARE\WOW6432Node"", ""HKCU:\SOFTWARE\WOW6432Node\Policies"", ""HKLM:\SOFTWARE\WOW6432Node\Policies""
foreach ($location in $REG) { Remove-Item ""$location\Microsoft\EdgeUpdate"" -recurse -force -ErrorAction SilentlyContinue }
# uninstall edgeupdate
foreach ($path in $edgeupdate) {
    if (Test-Path $path) { Start-Process -Wait $path -Args ""/unregsvc"" | Out-Null }
    do { Start-Sleep 3 } while ((Get-Process -Name ""setup"", ""MicrosoftEdge*"" -ErrorAction SilentlyContinue).Path -like ""*\Microsoft\Edge*"")
    if (Test-Path $path) { Start-Process -Wait $path -Args ""/uninstall"" | Out-Null }
    do { Start-Sleep 3 } while ((Get-Process -Name ""setup"", ""MicrosoftEdge*"" -ErrorAction SilentlyContinue).Path -like ""*\Microsoft\Edge*"")
}
# remove edgewebview regedit
cmd /c ""reg delete `""HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft EdgeWebView`"" /f >nul 2>&1""
cmd /c ""reg delete `""HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft EdgeWebView`"" /f >nul 2>&1""
# remove folders edge edgecore edgeupdate edgewebview temp
Remove-Item -Recurse -Force ""$env:SystemDrive\Program Files (x86)\Microsoft"" -ErrorAction SilentlyContinue | Out-Null
# remove edge shortcuts
Remove-Item -Recurse -Force ""$env:SystemDrive\Windows\System32\config\systemprofile\AppData\Roaming\Microsoft\Internet Explorer\Quick Launch\Microsoft Edge.lnk"" -ErrorAction SilentlyContinue | Out-Null
Remove-Item -Recurse -Force ""$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Microsoft Edge.lnk"" -ErrorAction SilentlyContinue | Out-Null

$fileSystemProfiles = Get-ChildItem -Path ""C:\Users"" -Directory | Where-Object { 
    $_.Name -notin @('Public', 'Default', 'Default User', 'All Users') -and 
    (Test-Path -Path ""$($_.FullName)\NTUSER.DAT"")
}

# Loop through each user profile and clean up Edge shortcuts
foreach ($profile in $fileSystemProfiles) {
    $userProfilePath = $profile.FullName
    
    # Define user-specific paths to clean
    $edgeShortcutPaths = @(
        ""$userProfilePath\AppData\Roaming\Microsoft\Internet Explorer\Quick Launch\Microsoft Edge.lnk"",
        ""$userProfilePath\Desktop\Microsoft Edge.lnk"",
        ""$userProfilePath\AppData\Roaming\Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar\Microsoft Edge.lnk"",
        ""$userProfilePath\AppData\Roaming\Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar\Tombstones\Microsoft Edge.lnk"",
        ""$userProfilePath\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Microsoft Edge.lnk""
    )

    # Remove Edge shortcuts for each user
    foreach ($path in $edgeShortcutPaths) {
        if (Test-Path -Path $path -PathType Leaf) {
            Remove-Item -Path $path -Force -ErrorAction SilentlyContinue
        }
    }
}

# Clean up common locations
$commonShortcutPaths = @(
    ""$env:PUBLIC\Desktop\Microsoft Edge.lnk"",
    ""$env:ALLUSERSPROFILE\Microsoft\Windows\Start Menu\Programs\Microsoft Edge.lnk"",
    ""C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Microsoft Edge.lnk""
)

foreach ($path in $commonShortcutPaths) {
    if (Test-Path -Path $path -PathType Leaf) {
        Remove-Item -Path $path -Force -ErrorAction SilentlyContinue
    }
}

# Removes Edge in Task Manager Startup Apps for All Users
# Get all user profiles on the system
$userProfiles = Get-CimInstance -ClassName Win32_UserProfile | 
Where-Object { -not $_.Special -and $_.SID -notmatch 'S-1-5-18|S-1-5-19|S-1-5-20' }

foreach ($profile in $userProfiles) {
    $sid = $profile.SID
    $hiveLoaded = $false

    if (-not (Test-Path ""Registry::HKEY_USERS\$sid"")) {
        $userRegPath = Join-Path $profile.LocalPath ""NTUSER.DAT""
        if (Test-Path $userRegPath) {
            reg load ""HKU\$sid"" $userRegPath | Out-Null
            $hiveLoaded = $true
            Start-Sleep -Seconds 2
        }
    }

    $runKeyPath = ""Registry::HKEY_USERS\$sid\Software\Microsoft\Windows\CurrentVersion\Run""

    if (Test-Path $runKeyPath) {
        $properties = Get-ItemProperty -Path $runKeyPath
        $edgeEntries = $properties.PSObject.Properties | 
        Where-Object { $_.Name -like 'MicrosoftEdgeAutoLaunch*' }

        foreach ($entry in $edgeEntries) {
            Remove-ItemProperty -Path $runKeyPath -Name $entry.Name -Force
        }
    }

    if ($hiveLoaded) {
        [gc]::Collect()
        Start-Sleep -Seconds 2
        reg unload ""HKU\$sid"" | Out-Null
    }
}
";
    }

    private static string GetOneDriveRemovalScript()
    {
        return @"
# OneDriveRemoval.ps1
# Standalone script to remove Microsoft OneDrive
# Source: Winhance (https://github.com/memstechtips/Winhance)

try {
    # Stop OneDrive processes
    $processesToStop = @(""OneDrive"", ""FileCoAuth"", ""FileSyncHelper"")
    foreach ($processName in $processesToStop) { 
        Get-Process -Name $processName -ErrorAction SilentlyContinue | 
        Stop-Process -Force -ErrorAction SilentlyContinue
    }
    Start-Sleep -Seconds 1
}
catch {
    # Continue if process stopping fails
}

# Check and execute uninstall strings from registry
$registryPaths = @(
    ""HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\OneDriveSetup.exe"",
    ""HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\OneDriveSetup.exe""
)

foreach ($regPath in $registryPaths) {
    try {
        if (Test-Path $regPath) {
            $uninstallString = (Get-ItemProperty -Path $regPath -ErrorAction Stop).UninstallString
            if ($uninstallString) {
                if ($uninstallString -match '^""([^""]+)""(.*)$') {
                    $exePath = $matches[1]
                    $args = $matches[2].Trim()
                    Start-Process -FilePath $exePath -ArgumentList $args -NoNewWindow -Wait -ErrorAction SilentlyContinue
                }
                else {
                    Start-Process -FilePath $uninstallString -NoNewWindow -Wait -ErrorAction SilentlyContinue
                }
            }
        }
    }
    catch {
        # Continue if registry operation fails
        continue
    }
}

try {
    # Remove OneDrive AppX package
    Get-AppxPackage -Name ""*OneDrive*"" -ErrorAction SilentlyContinue | 
    Remove-AppxPackage -ErrorAction SilentlyContinue
}
catch {
    # Continue if AppX removal fails
}

# Uninstall OneDrive using setup files
$oneDrivePaths = @(
    ""$env:SystemRoot\SysWOW64\OneDriveSetup.exe"",
    ""$env:SystemRoot\System32\OneDriveSetup.exe"",
    ""$env:LOCALAPPDATA\Microsoft\OneDrive\OneDrive.exe""
)

foreach ($path in $oneDrivePaths) {
    try {
        if (Test-Path $path) {
            Start-Process -FilePath $path -ArgumentList ""/uninstall"" -NoNewWindow -Wait -ErrorAction SilentlyContinue
        }
    }
    catch {
        # Continue if uninstall fails
        continue
    }
}

try {
    # Remove OneDrive scheduled tasks
    Get-ScheduledTask -ErrorAction SilentlyContinue | 
    Where-Object { $_.TaskName -match 'OneDrive' -and $_.TaskName -ne 'OneDriveRemoval' } | 
    ForEach-Object { 
        Unregister-ScheduledTask -TaskName $_.TaskName -Confirm:$false -ErrorAction SilentlyContinue 
    }
}
catch {
    # Continue if task removal fails
}

try {
    # Configure registry settings
    $regPath = ""HKLM:\SOFTWARE\Policies\Microsoft\OneDrive""
    if (-not (Test-Path $regPath)) {
        New-Item -Path $regPath -Force -ErrorAction SilentlyContinue | Out-Null
    }
    Set-ItemProperty -Path $regPath -Name ""KFMBlockOptIn"" -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue
    
    # Remove OneDrive from startup
    Remove-ItemProperty -Path ""HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run"" -Name ""OneDriveSetup"" -ErrorAction SilentlyContinue
    
    # Remove OneDrive from Navigation Pane
    Remove-Item -Path ""Registry::HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\{018D5C66-4533-4307-9B53-224DE2ED1FE6}"" -Recurse -Force -ErrorAction SilentlyContinue
}
catch {
    # Continue if registry operations fail
}

# Function to handle robust folder removal
function Remove-OneDriveFolder {
    param ([string]$folderPath)
    
    if (-not (Test-Path $folderPath)) {
        return
    }
    
    try {
        # Stop OneDrive processes if they're running
        Get-Process -Name ""OneDrive"" -ErrorAction SilentlyContinue | 
        Stop-Process -Force -ErrorAction SilentlyContinue
        
        # Take ownership and grant permissions
        $null = Start-Process ""takeown.exe"" -ArgumentList ""/F `""$folderPath`"" /R /A /D Y"" -Wait -NoNewWindow -PassThru -ErrorAction SilentlyContinue
        $null = Start-Process ""icacls.exe"" -ArgumentList ""`""$folderPath`"" /grant administrators:F /T"" -Wait -NoNewWindow -PassThru -ErrorAction SilentlyContinue
        
        # Try direct removal
        Remove-Item -Path $folderPath -Force -Recurse -ErrorAction SilentlyContinue
    }
    catch {
        try {
            # If direct removal fails, create and execute a cleanup batch file
            $batchPath = ""$env:TEMP\RemoveOneDrive_$(Get-Random).bat""
            $batchContent = @""
@echo off
timeout /t 2 /nobreak > nul
takeown /F ""$folderPath"" /R /A /D Y
icacls ""$folderPath"" /grant administrators:F /T
rd /s /q ""$folderPath""
del /F /Q ""%~f0""
""@
            Set-Content -Path $batchPath -Value $batchContent -Force -ErrorAction SilentlyContinue
            Start-Process ""cmd.exe"" -ArgumentList ""/c $batchPath"" -WindowStyle Hidden -ErrorAction SilentlyContinue
        }
        catch {
            # Continue if batch file cleanup fails
        }
    }
}

# Files to remove (single items)
$filesToRemove = @(
    ""$env:ALLUSERSPROFILE\Users\Default\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\OneDrive.lnk"",
    ""$env:ALLUSERSPROFILE\Users\Default\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\OneDrive.exe"",
    ""$env:PUBLIC\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\OneDrive.lnk"",
    ""$env:PUBLIC\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\OneDrive.exe"",
    ""$env:SystemRoot\System32\OneDriveSetup.exe"",
    ""$env:SystemRoot\SysWOW64\OneDriveSetup.exe"",
    ""$env:LOCALAPPDATA\Microsoft\OneDrive\OneDrive.exe"",
    ""$env:ProgramData\Microsoft\Windows\Start Menu\Programs\OneDrive.lnk""
)

# Remove single files
foreach ($file in $filesToRemove) {
    try {
        if (Test-Path $file) {
            Remove-Item $file -Force -ErrorAction SilentlyContinue
        }
    }
    catch {
        # Continue if file removal fails
        continue
    }
}

# Folders that need special handling
$foldersToRemove = @(
    ""$env:ProgramFiles\Microsoft\OneDrive"",
    ""$env:ProgramFiles\Microsoft OneDrive"",
    ""$env:LOCALAPPDATA\Microsoft\OneDrive""
)

# Remove folders with robust method
foreach ($folder in $foldersToRemove) {
    try {
        Remove-OneDriveFolder -folderPath $folder
    }
    catch {
        # Continue if folder removal fails
        continue
    }
}

# Additional cleanup for stubborn setup files
$setupFiles = @(
    ""$env:SystemRoot\System32\OneDriveSetup.exe"",
    ""$env:SystemRoot\SysWOW64\OneDriveSetup.exe""
)

foreach ($file in $setupFiles) {
    if (Test-Path $file) {
        try {
            # Take ownership and grant full permissions
            $null = Start-Process ""takeown.exe"" -ArgumentList ""/F `""$file`"""" -Wait -NoNewWindow -PassThru -ErrorAction SilentlyContinue
            $null = Start-Process ""icacls.exe"" -ArgumentList ""`""$file`"" /grant administrators:F"" -Wait -NoNewWindow -PassThru -ErrorAction SilentlyContinue
        
            # Attempt direct removal
            Remove-Item -Path $file -Force -ErrorAction SilentlyContinue
        
            # If file still exists, schedule it for deletion on next reboot
            if (Test-Path $file) {
                $pendingRename = ""$file.pending""
                Move-Item -Path $file -Destination $pendingRename -Force -ErrorAction SilentlyContinue
                Start-Process ""cmd.exe"" -ArgumentList ""/c del /F /Q `""$pendingRename`"""" -WindowStyle Hidden -ErrorAction SilentlyContinue
            }
        }
        catch {
            # Continue if cleanup fails
            continue
        }
    }
}
";
    }

}

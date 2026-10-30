namespace Winhance.Core.Features.SoftwareApps.Models;

public static class EdgeRemovalScript
{
    public const string ScriptName = "EdgeRemoval";

    public const string ScriptVersion = "1.8";

    private const string FullModeLine = "$RemoveWebView2 = $true";

    public static bool RemovesWebView2(string script) => script.Contains(FullModeLine, StringComparison.Ordinal);

    public static string GetScript(bool removeWebView2 = false)
    {
        return @"
<#
  .SYNOPSIS
      Removes Microsoft Edge (Legacy and Chromium versions) from Windows 10/11 systems.
      Script Version: " + ScriptVersion + @"

  .DESCRIPTION
      This script detects and removes Microsoft Edge installations including:
      - Legacy UWP Edge (pre-Chromium)
      - Chromium-based Edge (current version)
      " + (removeWebView2
            ? "- EdgeWebView2, EdgeUpdate and EdgeCore, so Widgets and apps that need WebView2 stop working"
            : "- EdgeUpdate and EdgeWebView2 are NOT removed, so WebView2 keeps getting updates") + @"

      This script is designed to run in any context: user sessions, SYSTEM account, or scheduled tasks.

  .NOTES
      Source: https://github.com/memstechtips/Winhance

      Requirements:
      - Windows 10/11
      - Administrator privileges (script will auto-elevate)
      - PowerShell 5.1 or higher

      Credits:
      - Legacy Edge removal based on work by ishad0w: https://gist.github.com/ishad0w/d25ca52eb04dbefba8087a344a69c79c
      - Chromium Edge removal based on work by FR33THY: https://github.com/FR33THYFR33THY/Ultimate-Windows-Optimization-Guide/blob/main/6%20Windows/14%20Edge.ps1
      - Edge protocol redirect based on OpenWebSearch by AveYo: https://github.com/AveYo/fox/blob/main/OpenWebSearch.cmd
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

" + (removeWebView2 ? FullModeLine : "$RemoveWebView2 = $false") + @"

# Setup logging
$logFolder = ""C:\ProgramData\Winhance\Logs""
$logFile = ""$logFolder\EdgeRemovalLog.txt""

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

# Helper function to get Legacy Edge packages
function Get-LegacyEdgePackages {
    $legacyRegPath = ""HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\Packages""
    return Get-ChildItem -Path $legacyRegPath -Name -ErrorAction SilentlyContinue | Where-Object { $_ -match ""Microsoft-Windows-Internet-Browser-Package"" -and $_ -match ""~~"" }
}

# Function to test if Legacy Edge is installed
function Test-LegacyEdgeInstalled {
    $packages = Get-LegacyEdgePackages

    if ($packages) {
        foreach ($package in $packages) {
            $packageInfo = & dism /online /Get-PackageInfo /PackageName:$package 2>$null
            if ($packageInfo -match ""State.*Installed"") {
                return $true
            }
        }
    }
    return $false
}

# Function to test if Chromium Edge is installed
function Test-ChromiumEdgeInstalled {
    # Only the Edge folder counts: EdgeUpdate and EdgeCore stay for WebView2 and Copilot
    foreach ($pf in @($env:ProgramFiles, ${env:ProgramFiles(x86)})) {
        if (Test-Path ""$pf\Microsoft\Edge"") {
            return $true
        }
    }

    # Fallback: Check installed programs
    try {
        $edgeApp = Get-WmiObject -Class Win32_InstalledStoreProgram -Filter ""Name like '%Microsoft.MicrosoftEdge.Stable%'"" -ErrorAction SilentlyContinue
        return $edgeApp -ne $null
    } catch {
        return $false
    }
}

# Function to stop Edge-related processes
function Stop-EdgeProcesses {
    Write-Log ""Stopping Edge-related processes and services""
    $stop = ""MicrosoftEdgeUpdate"", ""msedge"", ""Resume"", ""CrossDeviceResume""
    # WebView2 and the apps hosting it only close when WebView2 itself is being removed
    if ($RemoveWebView2) {
        $stop += ""msedgewebview2"", ""OneDrive"", ""WidgetService"", ""Widgets""
    }
    $stop | ForEach-Object {
        $processCount = (Get-Process -Name $_ -ErrorAction SilentlyContinue).Count
        if ($processCount -gt 0) {
            Stop-Process -Name $_ -Force -ErrorAction SilentlyContinue
            Write-Log ""Stopped $processCount instance(s) of $_""
        }
    }
}

# Function to remove Legacy Edge
function Remove-LegacyEdge {
    Write-Log ""Starting Legacy Edge/UWP Edge removal process""
    # Query registry for Edge Legacy package
    $packages = Get-LegacyEdgePackages
    $edgeLegacyPackageVersion = $packages | Select-Object -First 1
    $packagePath = ""HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\Packages\$edgeLegacyPackageVersion""
    # Set registry visibility
    Set-ItemProperty -Path $packagePath -Name ""Visibility"" -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue
    # Remove owners registry entries
    $ownersPath = ""$packagePath\Owners""
    if (Test-Path $ownersPath) { Remove-Item -Path $ownersPath -Recurse -Force -ErrorAction SilentlyContinue }
    Write-Log ""Removing Legacy Edge package via DISM (with 30-second timeout)""
    $dismProcess = Start-Process -FilePath ""dism.exe"" -ArgumentList ""/online"", ""/Remove-Package"", ""/PackageName:$edgeLegacyPackageVersion"" -NoNewWindow -PassThru

    if ($dismProcess -and $dismProcess.WaitForExit(30000)) {
        Write-Log ""DISM completed successfully""
    } elseif ($dismProcess) {
        Write-Log ""DISM timed out after 30 seconds, killing process and retrying once""
        $dismProcess.Kill()
        Start-Sleep 2

        # Retry once
        Write-Log ""Retrying DISM command""
        $retryProcess = Start-Process -FilePath ""dism.exe"" -ArgumentList ""/online"", ""/Remove-Package"", ""/PackageName:$edgeLegacyPackageVersion"" -NoNewWindow -PassThru

        if ($retryProcess -and $retryProcess.WaitForExit(30000)) {
            Write-Log ""DISM retry completed successfully""
        } elseif ($retryProcess) {
            Write-Log ""DISM retry also timed out, continuing with script""
            $retryProcess.Kill()
        } else {
            Write-Log ""DISM retry failed to start, continuing with script""
        }
    } else {
        Write-Log ""DISM failed to start, continuing with script""
    }
    # Remove Legacy UWP Edge package
    Write-Log ""Removing Legacy UWP Edge package""
    Get-AppxPackage -AllUsers Microsoft.MicrosoftEdge | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue | Out-Null
    Write-Log ""Legacy Edge/UWP Edge removal process completed""
}

# Function to remove Edge shortcuts
function Remove-EdgeShortcuts {
    Write-Log ""Starting Edge shortcuts cleanup""

    # Get ALL user profiles (no exclusions)
    $userProfiles = Get-ChildItem -Path ""C:\Users"" -Directory | Where-Object {
        (Test-Path -Path ""$($_.FullName)\NTUSER.DAT"")
    }

    # Build all shortcut paths to check
    $shortcutPaths = @()

    # Add user-specific paths (now includes Public, Default, etc.)
    foreach ($profile in $userProfiles) {
        $shortcutPaths += @(
            ""$($profile.FullName)\AppData\Roaming\Microsoft\Internet Explorer\Quick Launch\Microsoft Edge.lnk"",
            ""$($profile.FullName)\Desktop\Microsoft Edge.lnk"",
            ""$($profile.FullName)\AppData\Roaming\Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar\Microsoft Edge.lnk"",
            ""$($profile.FullName)\AppData\Roaming\Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar\Tombstones\Microsoft Edge.lnk"",
            ""$($profile.FullName)\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Microsoft Edge.lnk""
        )
    }

    # Add the single ProgramData path
    $shortcutPaths += ""C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Microsoft Edge.lnk""

    # Remove all shortcuts in one loop
    $removedCount = 0
    foreach ($path in $shortcutPaths) {
        if (Test-Path -Path $path -PathType Leaf) {
            Remove-Item -Path $path -Force -ErrorAction SilentlyContinue
            $removedCount++
        }
    }

    Write-Log ""Removed $removedCount Edge shortcut(s)""
}

function Install-EdgeProtocolRedirect {
    Write-Log ""Installing Edge protocol redirect using OpenWebSearch""
    $scriptsDir = ""C:\ProgramData\Winhance\OpenWebSearch""
    New-Item -ItemType Directory -Path $scriptsDir -Force -ErrorAction SilentlyContinue | Out-Null

    $stubTargetPath = ""$scriptsDir\ie_to_edge_stub.exe""
    if (!(Test-Path $stubTargetPath)) {
        Write-Log ""Warning: ie_to_edge_stub.exe not found at $stubTargetPath (should have been copied before Edge removal)""
        return
    }

    $openWebSearchContent = @""
@title OpenWebSearch 2023 & echo off
for /f %%E in ('""prompt `$E`$S& for %%e in (1) do rem""') do echo;%%E[2t 2>nul

call :reg_var ""HKCU\SOFTWARE\Microsoft\Windows\Shell\Associations\UrlAssociations\https\UserChoiceLatest\ProgId"" ProgID ProgID
if not defined ProgID call :reg_var ""HKCU\SOFTWARE\Microsoft\Windows\Shell\Associations\UrlAssociations\https\UserChoice"" ProgID ProgID
if /i ""%ProgID%"" neq ""MSEdgeHTM"" if defined ProgID goto :browser_found
for %%P in (""%ProgramFiles(x86)%\Microsoft\Edge\Application\msedge.exe"" ""%ProgramFiles%\Microsoft\Edge\Application\msedge.exe"") do if exist %%P (set ""Choice=%%~P""& goto :skip_browser)
set ""Choice=""
for %%R in (HKCU HKLM) do (
    for /f ""delims="" %%K in ('reg query ""%%R\SOFTWARE\Clients\StartMenuInternet"" 2^>nul') do (
        for /f ""skip=1 tokens=2*"" %%A in ('reg query ""%%K\shell\open\command"" /ve 2^>nul') do (
            echo ""%%B"" | findstr /i ""msedge ie_to_edge_stub iexplore"" >nul || (set ""Choice=%%~B"" & goto :skip_browser)
        )
    )
)
if not defined Choice exit /b
:browser_found
call :reg_var ""HKCR\%ProgID%\shell\open\command"" """" Browser
set Choice=& for %%. in (%Browser%) do if not defined Choice set ""Choice=%%~.""
:skip_browser

set ""URI="" & set ""URL="" & set ""NOOP=""

set ""CLI=%CMDCMDLINE:""=````%""
if defined CLI set ""CLI=%CLI:*ie_to_edge_stub.exe```` =%""
if defined CLI set ""CLI=%CLI:*ie_to_edge_stub.exe =%""
if defined CLI set ""CLI=%CLI:*msedge.exe```` =%""
if defined CLI set ""CLI=%CLI:*msedge.exe =%""
set ""FIX=%CLI:~-1%""
if defined CLI if ""%FIX%""=="" "" set ""CLI=%CLI:~0,-1%""
if defined CLI set ""RED=%CLI:microsoft-edge=%""
if defined CLI set ""URL=%CLI:http=%""
if ""%CLI%"" equ ""%RED%"" (set NOOP=1) else if ""%CLI%"" equ ""%URL%"" (set NOOP=1)
if defined NOOP exit /b

set ""URL=%CLI:*microsoft-edge=%""
set ""URL=http%URL:*http=%""
set ""FIX=%URL:~-2%""
if defined URL if ""%FIX%""==""````"" set ""URL=%URL:~0,-2%""
call :dec_url
start """" ""%Choice%"" ""%URL%""
exit

:reg_var
set {var}=& set {reg}=reg query ""%~1"" /v %2 /z /se "","" /f /e& if %2=="""" set {reg}=reg query ""%~1"" /ve /z /se "","" /f /e
for /f ""skip=2 tokens=* delims="" %%V in ('%{reg}% %4 %5 %6 %7 %8 %9 2^>nul') do if not defined {var} set ""{var}=%%V""
if not defined {var} (set {reg}=& set ""%~3=""& exit /b) else if %2=="""" set ""{var}=%{var}:*)    =%""
if not defined {var} (set {reg}=& set ""%~3=""& exit /b) else set {reg}=& set ""%~3=%{var}:*)    =%""& set {var}=& exit /b

:dec_url
set "".=%URL:!=}%"" & setlocal enabledelayedexpansion
set "".=!.:%%={!"" &set "".=!.:{3A=:!"" &set "".=!.:{2F=/!"" &set "".=!.:{3F=?!"" &set "".=!.:{23=#!"" &set "".=!.:{5B=[!"" &set "".=!.:{5D=]!""
set "".=!.:{40=@!""&set "".=!.:{21=}!"" &set "".=!.:{24=`$!"" &set "".=!.:{26=&!"" &set "".=!.:{27='!"" &set "".=!.:{28=(!"" &set "".=!.:{29=)!""
set "".=!.:{2A=*!""&set "".=!.:{2B=+!"" &set "".=!.:{2C=,!"" &set "".=!.:{3B=;!"" &set "".=!.:{3D==!"" &set "".=!.:{25=%%!""&set "".=!.:{20= !""
set "".=!.:{=%%!"" & endlocal& set ""URL=%.:}=!%"" & exit /b
""@

    $openWebSearchPath = ""$scriptsDir\OpenWebSearch.cmd""
    $openWebSearchContent | Out-File -FilePath $openWebSearchPath -Encoding ASCII -Force
    Write-Log ""Created OpenWebSearch.cmd at $openWebSearchPath""

    $buildNumber = [Environment]::OSVersion.Version.Build
    $conhostFlags = if ($buildNumber -gt 25179) { ""--width 1 --height 1"" } else { ""--headless"" }
    $conhostDebugger = ""$env:SystemRoot\system32\conhost.exe $conhostFlags $scriptsDir\OpenWebSearch.cmd""

    Write-Log ""Configuring registry entries for Edge protocol redirect""
    reg.exe add ""HKCR\microsoft-edge"" /f /ve /d ""URL:microsoft-edge"" 2>&1 | Out-Null
    reg.exe add ""HKCR\microsoft-edge"" /f /v ""URL Protocol"" /d `""`"" 2>&1 | Out-Null
    reg.exe add ""HKCR\microsoft-edge"" /f /v ""NoOpenWith"" /d `""`"" 2>&1 | Out-Null
    reg.exe add ""HKCR\microsoft-edge\shell\open\command"" /f /ve /d ""$stubTargetPath %1"" 2>&1 | Out-Null
    reg.exe add ""HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\ie_to_edge_stub.exe"" /f /v UseFilter /d 1 /t reg_dword 2>&1 | Out-Null
    reg.exe add ""HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\ie_to_edge_stub.exe\0"" /f /v FilterFullPath /d ""$stubTargetPath"" 2>&1 | Out-Null
    reg.exe add ""HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\ie_to_edge_stub.exe\0"" /f /v Debugger /d ""$conhostDebugger"" 2>&1 | Out-Null
    # Remove msedge.exe IFEO entries from previous versions to prevent Edge reinstall issues
    reg.exe delete ""HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\msedge.exe"" /f 2>&1 | Out-Null
    Write-Log ""Registry configuration completed""

    # Create repair script that re-sets protocol handler and MSEdgeHTM registry if Edge overwrites them
    $repairContent = @""
# OpenWebSearch Repair - re-sets protocol handler and MSEdgeHTM registry if Edge overwrites them
`$stubPath = ""$stubTargetPath""
`$owsPath = ""$openWebSearchPath""
if (-not (Test-Path `$stubPath)) { exit }
if (-not (Test-Path `$owsPath)) { exit }
`$cmd = (Get-ItemProperty ""Registry::HKEY_CLASSES_ROOT\microsoft-edge\shell\open\command"" -ErrorAction SilentlyContinue).'(default)'
if (`$cmd -and `$cmd -notlike ""*ie_to_edge_stub*"") {
    reg.exe add ""HKCR\microsoft-edge\shell\open\command"" /f /ve /d ""`$stubPath %1"" 2>&1 | Out-Null
}
`$htm = (Get-ItemProperty ""Registry::HKEY_CLASSES_ROOT\MSEdgeHTM\shell\open\command"" -ErrorAction SilentlyContinue).'(default)'
if (`$htm -and `$htm -notlike ""*ie_to_edge_stub*"") {
    reg.exe add ""HKCR\MSEdgeHTM\shell\open\command"" /f /ve /d ""```""`$stubPath```"" %1"" 2>&1 | Out-Null
}
""@
    $repairScriptPath = ""$scriptsDir\OpenWebSearchRepair.ps1""
    $repairContent | Out-File -FilePath $repairScriptPath -Encoding UTF8 -Force
    Write-Log ""Created OpenWebSearchRepair.ps1 at $repairScriptPath""

    # Register scheduled task to check and repair protocol handlers at logon
    $repairTaskName = ""OpenWebSearchRepair""
    $repairAction = New-ScheduledTaskAction -Execute ""powershell.exe"" -Argument ""-ExecutionPolicy Bypass -NoProfile -Command `""iex([IO.File]::ReadAllText('$repairScriptPath'))`""""
    $repairTrigger = New-ScheduledTaskTrigger -AtLogon
    $repairSettings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable
    $repairPrincipal = New-ScheduledTaskPrincipal -UserId ""SYSTEM"" -LogonType ServiceAccount -RunLevel Highest
    Register-ScheduledTask -TaskName $repairTaskName -TaskPath ""\Winhance\"" -Action $repairAction -Trigger $repairTrigger -Settings $repairSettings -Principal $repairPrincipal -Force | Out-Null
    Write-Log ""Registered OpenWebSearchRepair scheduled task (runs at logon)""

}

# Edge's uninstaller unpins its taskbar icon only for the user it runs as, and deleting the pinned .lnk does not
# unpin it, so a SYSTEM run leaves a blank icon. Taskband keeps the pins as two parallel lists: Favorites holds
# 0x00 + DWORD size + item per pin and ends in 0xFF, FavoritesResolve holds DWORD size + shell link per pin.
# Returns both lists without the Edge pin, or $null when there is no Edge pin or the lists do not line up
function Remove-EdgePinEntry([byte[]]$favorites, [byte[]]$resolve) {
    $edgeLink = [BitConverter]::ToString([Text.Encoding]::Unicode.GetBytes(""Microsoft Edge.lnk""))
    $favoriteEntries = @()
    $i = 0
    while ($i + 5 -le $favorites.Length -and $favorites[$i] -eq 0) {
        $size = 5 + [BitConverter]::ToUInt32($favorites, $i + 1)
        $favoriteEntries += ,@($i, $size)
        $i += $size
    }
    $resolveEntries = @()
    $j = 0
    while ($j + 4 -le $resolve.Length) {
        $size = 4 + [BitConverter]::ToUInt32($resolve, $j)
        if ($size -eq 4 -or $j + $size -gt $resolve.Length) { break }
        $resolveEntries += ,@($j, $size)
        $j += $size
    }
    if ($i -gt $favorites.Length -or $favoriteEntries.Count -ne $resolveEntries.Count) { return $null }

    $edgeIndex = -1
    for ($k = 0; $k -lt $favoriteEntries.Count; $k++) {
        $favoriteHex = [BitConverter]::ToString($favorites, $favoriteEntries[$k][0], $favoriteEntries[$k][1])
        $resolveHex = [BitConverter]::ToString($resolve, $resolveEntries[$k][0], $resolveEntries[$k][1])
        if ($favoriteHex.Contains($edgeLink) -and $resolveHex.Contains($edgeLink)) { $edgeIndex = $k; break }
    }
    if ($edgeIndex -lt 0) { return $null }

    $newFavorites = New-Object System.Collections.Generic.List[byte]
    $newResolve = New-Object System.Collections.Generic.List[byte]
    for ($k = 0; $k -lt $favoriteEntries.Count; $k++) {
        if ($k -eq $edgeIndex) { continue }
        $newFavorites.AddRange([byte[]]$favorites[$favoriteEntries[$k][0]..($favoriteEntries[$k][0] + $favoriteEntries[$k][1] - 1)])
        $newResolve.AddRange([byte[]]$resolve[$resolveEntries[$k][0]..($resolveEntries[$k][0] + $resolveEntries[$k][1] - 1)])
    }
    if ($i -lt $favorites.Length) { $newFavorites.AddRange([byte[]]$favorites[$i..($favorites.Length - 1)]) }
    if ($j -lt $resolve.Length) { $newResolve.AddRange([byte[]]$resolve[$j..($resolve.Length - 1)]) }
    @{ Favorites = $newFavorites.ToArray(); Resolve = $newResolve.ToArray() }
}

function Remove-EdgeTaskbarPins {
    Write-Log ""Removing Edge taskbar pins""
    $profileList = ""HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList""
    # Each profile is checked once, so a signed-out user's NTUSER.DAT is not loaded on every boot. A run that just
    # removed Edge checks them all again.
    $doneKey = ""HKLM:\SOFTWARE\Winhance\EdgeTaskbarUnpinned""
    if ($removedSomething) { Remove-Item $doneKey -Recurse -Force -ErrorAction SilentlyContinue }
    if (-not (Test-Path $doneKey)) { New-Item $doneKey -Force | Out-Null }
    $removedCount = 0
    $signedInSids = @()

    foreach ($sidKey in Get-ChildItem $profileList -ErrorAction SilentlyContinue) {
        $sid = $sidKey.PSChildName
        if ($null -ne (Get-ItemProperty $doneKey -Name $sid -ErrorAction SilentlyContinue)) { continue }
        $profilePath = (Get-ItemProperty $sidKey.PSPath -ErrorAction SilentlyContinue).ProfileImagePath
        if (-not $profilePath -or -not (Test-Path ""$profilePath\NTUSER.DAT"")) { continue }

        $hive = ""Registry::HKEY_USERS\$sid""
        $loaded = $false
        if (-not (Test-Path $hive)) {
            # Left mounted by a failed unload, or held open by another process: try again next boot
            if (Test-Path ""Registry::HKEY_USERS\WinhanceEdgePin"") { continue }
            reg.exe load ""HKU\WinhanceEdgePin"" ""$profilePath\NTUSER.DAT"" 2>&1 | Out-Null
            if ($LASTEXITCODE -ne 0) { Write-Log ""Could not load the registry hive of $sid""; continue }
            $hive = ""Registry::HKEY_USERS\WinhanceEdgePin""
            $loaded = $true
        }

        try {
            $taskband = ""$hive\Software\Microsoft\Windows\CurrentVersion\Explorer\Taskband""
            $values = Get-ItemProperty $taskband -ErrorAction SilentlyContinue
            if ($values.Favorites -and $values.FavoritesResolve) {
                $pins = Remove-EdgePinEntry $values.Favorites $values.FavoritesResolve
                if ($pins) {
                    Set-ItemProperty $taskband -Name Favorites -Value $pins.Favorites -Type Binary
                    Set-ItemProperty $taskband -Name FavoritesResolve -Value $pins.Resolve -Type Binary
                    $removedCount++
                    if (-not $loaded) { $signedInSids += $sid }
                }
            }
            New-ItemProperty $doneKey -Name $sid -Value 1 -PropertyType DWord -Force | Out-Null
        }
        finally {
            if ($loaded) {
                # reg unload fails while PowerShell still holds handles into the hive
                $values = $null
                [GC]::Collect()
                [GC]::WaitForPendingFinalizers()
                reg.exe unload ""HKU\WinhanceEdgePin"" 2>&1 | Out-Null
                if ($LASTEXITCODE -ne 0) { Write-Log ""Could not unload the registry hive of $sid"" }
            }
        }
    }

    Write-Log ""Removed the Edge taskbar pin for $removedCount user(s)""

    # A running Explorer keeps showing the pin it loaded at sign-in; Windows restarts the shell when it ends
    foreach ($process in Get-CimInstance Win32_Process -Filter ""Name='explorer.exe'"" -ErrorAction SilentlyContinue) {
        $owner = Invoke-CimMethod -InputObject $process -MethodName GetOwnerSid -ErrorAction SilentlyContinue
        if ($owner.Sid -in $signedInSids) {
            Stop-Process -Id $process.ProcessId -Force -ErrorAction SilentlyContinue
            Write-Log ""Restarted Explorer for $($owner.Sid)""
        }
    }
}

# Function to remove Chromium Edge
function Remove-ChromiumEdge {
    # Remove Chromium Edge
    Write-Log ""Starting Edge Chromium uninstallation process""
    # Folder and file to allow uninstall of Edge Chromium Browser
    Write-Log ""Creating temporary directory for Edge uninstallation""
    $edgePath = ""$env:SystemRoot\SystemApps\Microsoft.MicrosoftEdge_8wekyb3d8bbwe""
    New-Item -Path $edgePath -ItemType Directory -ErrorAction SilentlyContinue | Out-Null
    New-Item -Path $edgePath -ItemType File -Name ""MicrosoftEdge.exe"" -ErrorAction SilentlyContinue | Out-Null
    # Get Edge Uninstall Strings for Enterprise MSI version or normal version (they require different handling)
    Write-Log ""Searching for Edge uninstall strings in registry""
    $uninstallKeys = Get-ChildItem ""HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall""
    $edgeUninstallCount = 0
    foreach ($key in $uninstallKeys) {
        $displayName = (Get-ItemProperty $key.PSPath -ErrorAction SilentlyContinue).DisplayName
        if ($displayName -eq ""Microsoft Edge"") {
            $uninstallString = (Get-ItemProperty $key.PSPath).UninstallString
            if ($uninstallString) {
                $edgeUninstallCount++
                # Kill any Edge processes that may have respawned since initial stop
                Stop-EdgeProcesses
                if ($uninstallString -like ""*msiexec*"") {
                    # Uninstalls Enterprise MSI version
                    Write-Log ""Executing MSI uninstaller for Edge""
                    Start-Process cmd.exe ""/c $uninstallString /quiet"" -WindowStyle Hidden -Wait | Out-Null
                } else {
                    # Uninstalls normal version
                    Write-Log ""Executing standard uninstaller for Edge""
                    Start-Process cmd.exe ""/c $uninstallString --force-uninstall --silent"" -WindowStyle Hidden -Wait | Out-Null
                }
            }
        }
    }
    if ($edgeUninstallCount -eq 0) {
        Write-Log ""No Edge uninstall entries found in registry""
    } else {
        Write-Log ""Executed $edgeUninstallCount Edge uninstaller(s)""
    }
    # Remove UWP Edge Chromium package
    Write-Log ""Removing UWP Edge Chromium package""
    Get-AppxPackage -AllUsers Microsoft.MicrosoftEdge.Stable | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue | Out-Null
    # Cleanup: Remove folder and file we created earlier
    Write-Log ""Cleaning up temporary Edge directory""
    Remove-Item -Recurse -Force $edgePath -ErrorAction SilentlyContinue | Out-Null
    Write-Log ""Edge Chromium uninstallation process completed""

}

function Remove-EdgeRegistryKeys {
    Write-Log ""Starting comprehensive Edge registry cleanup""

    $directPaths = @(
        ""HKLM:\SOFTWARE\Microsoft\Edge"",
        ""HKLM:\SOFTWARE\WOW6432Node\Microsoft\Edge"",
        ""HKCU:\Software\Microsoft\Edge"",
        ""HKLM:\SOFTWARE\Clients\StartMenuInternet\Microsoft Edge"",
        ""HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\msedge.exe"",
        ""HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\MicrosoftEdge"",
        ""HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft Edge"",
        ""HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft Edge"",
        ""HKLM:\SYSTEM\CurrentControlSet\Services\Eventlog\Application\Edge""
    )

    $removedCount = 0
    foreach ($path in $directPaths) {
        if (Test-Path $path) {
            Remove-Item $path -Recurse -Force -ErrorAction SilentlyContinue
            $removedCount++
        }
    }
    Write-Log ""Removed $removedCount direct registry key(s)""

    $valuesToRemove = @(
        @{Path = ""HKLM:\SOFTWARE\RegisteredApplications""; Name = ""Microsoft Edge""},
        @{Path = ""HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\FeatureUsage\AppLaunch""; Name = ""MSEdge""},
        @{Path = ""HKCU:\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Compatibility Assistant\Store""; Name = ""C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe""}
    )

    $removedValuesCount = 0
    foreach ($item in $valuesToRemove) {
        if ((Test-Path $item.Path) -and (Get-ItemProperty -Path $item.Path -Name $item.Name -ErrorAction SilentlyContinue)) {
            Remove-ItemProperty -Path $item.Path -Name $item.Name -Force -ErrorAction SilentlyContinue
            $removedValuesCount++
        }
    }
    Write-Log ""Removed $removedValuesCount registry value(s)""

    $patterns = @(
        @{Root = ""HKLM:\SOFTWARE\Classes""; Pattern = ""MSEdge*""}
    )

    $removedPatternCount = 0
    foreach ($patternItem in $patterns) {
        if (Test-Path $patternItem.Root) {
            $matchedKeys = Get-ChildItem -Path $patternItem.Root -ErrorAction SilentlyContinue |
                Where-Object { $_.PSChildName -like $patternItem.Pattern }

            foreach ($key in $matchedKeys) {
                Remove-Item $key.PSPath -Recurse -Force -ErrorAction SilentlyContinue
                $removedPatternCount++
            }
        }
    }
    Write-Log ""Removed $removedPatternCount pattern-matched key(s)""

    $muiCachePath = ""HKCU:\SOFTWARE\Classes\Local Settings\Software\Microsoft\Windows\Shell\MuiCache""
    if (Test-Path $muiCachePath) {
        $properties = Get-ItemProperty -Path $muiCachePath -ErrorAction SilentlyContinue
        $removedMuiCount = 0
        if ($properties) {
            foreach ($prop in $properties.PSObject.Properties) {
                if ($prop.Name -like ""*Edge*"" -or $prop.Name -like ""*EdgeUpdate*"") {
                    Remove-ItemProperty -Path $muiCachePath -Name $prop.Name -Force -ErrorAction SilentlyContinue
                    $removedMuiCount++
                }
            }
        }
        Write-Log ""Removed $removedMuiCount MuiCache entry(ies)""
    }
}

function Remove-AdditionalEdgeFolders {
    Write-Log ""Starting additional Edge folder cleanup""

    $systemPaths = @(
        ""C:\Windows\Temp\MsEdgeCrashpad""
    )

    $removedCount = 0
    foreach ($path in $systemPaths) {
        if (Test-Path $path) {
            Write-Log ""Removing: $path""
            Remove-Item $path -Recurse -Force -ErrorAction SilentlyContinue
            $removedCount++
        }
    }

    $userProfiles = Get-ChildItem -Path ""C:\Users"" -Directory -ErrorAction SilentlyContinue |
        Where-Object { Test-Path ""$($_.FullName)\NTUSER.DAT"" }

    foreach ($profile in $userProfiles) {
        $edgeLocalPath = ""$($profile.FullName)\AppData\Local\Microsoft\Edge""
        if (Test-Path $edgeLocalPath) {
            Write-Log ""Removing: $edgeLocalPath""
            Remove-Item $edgeLocalPath -Recurse -Force -ErrorAction SilentlyContinue
            $removedCount++
        }
    }

    Write-Log ""Removed $removedCount additional Edge folder(s)""
}

function Test-WebView2Installed {
    $x86 = ${env:ProgramFiles(x86)}
    $paths = @(
        ""$x86\Microsoft\EdgeWebView"",
        ""$x86\Microsoft\EdgeUpdate"",
        ""$x86\Microsoft\EdgeCore"",
        ""HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate"",
        ""HKLM:\SYSTEM\CurrentControlSet\Services\edgeupdate"",
        ""HKLM:\SYSTEM\CurrentControlSet\Services\edgeupdatem""
    )
    return [bool]($paths | Where-Object { Test-Path $_ })
}

function Remove-WebView2AndEdgeUpdate {
    # EdgeUpdate goes first: while it exists it re-registers a deleted WebView2 and leaves it half installed
    Write-Log ""Removing EdgeUpdate services and tasks""
    foreach ($service in ""edgeupdate"", ""edgeupdatem"") {
        if (Get-Service -Name $service -ErrorAction SilentlyContinue) {
            Stop-Service -Name $service -Force -ErrorAction SilentlyContinue
            sc.exe delete $service 2>&1 | Out-Null
            Write-Log ""Deleted service: $service""
        }
    }
    Get-ScheduledTask -TaskName ""MicrosoftEdgeUpdate*"" -ErrorAction SilentlyContinue | ForEach-Object {
        Unregister-ScheduledTask -TaskName $_.TaskName -TaskPath $_.TaskPath -Confirm:$false -ErrorAction SilentlyContinue
        Write-Log ""Deleted scheduled task: $($_.TaskName)""
    }

    # Start search and Widgets hold WebView2 files open and Windows restarts them, so stop and delete in a loop
    $x86 = ${env:ProgramFiles(x86)}
    $folders = @(""$x86\Microsoft\EdgeUpdate"", ""$x86\Microsoft\EdgeWebView"", ""$x86\Microsoft\EdgeCore"", ""$env:ProgramData\Microsoft\EdgeUpdate"")
    for ($attempt = 1; $attempt -le 5 -and ($folders | Where-Object { Test-Path $_ }); $attempt++) {
        Get-Process -Name MicrosoftEdgeUpdate, msedgewebview2, SearchHost, Widgets, WidgetBoard, WidgetService -ErrorAction SilentlyContinue |
            Stop-Process -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
        $folders | Where-Object { Test-Path $_ } | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    }
    foreach ($folder in $folders) {
        if (Test-Path $folder) { Write-Log ""Could not fully remove: $folder"" } else { Write-Log ""Removed: $folder"" }
    }

    $keys = @(
        ""HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate"",
        ""HKLM:\SOFTWARE\Microsoft\EdgeUpdate"",
        ""HKCU:\Software\Microsoft\EdgeUpdate"",
        ""HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft EdgeWebView"",
        ""HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft EdgeWebView"",
        ""HKLM:\SYSTEM\CurrentControlSet\Services\EventLog\Application\edgeupdate"",
        ""HKLM:\SYSTEM\CurrentControlSet\Services\EventLog\Application\edgeupdatem""
    )
    $removedKeys = 0
    foreach ($key in $keys) {
        if (Test-Path $key) {
            Remove-Item $key -Recurse -Force -ErrorAction SilentlyContinue
            $removedKeys++
        }
    }
    Get-ChildItem -Path ""HKLM:\SOFTWARE\Classes"" -ErrorAction SilentlyContinue |
        Where-Object { $_.PSChildName -like ""MicrosoftEdgeUpdate*"" } |
        ForEach-Object { Remove-Item $_.PSPath -Recurse -Force -ErrorAction SilentlyContinue; $removedKeys++ }
    Write-Log ""Removed $removedKeys EdgeUpdate and WebView2 registry key(s)""
}

Write-Host ""Starting Edge removal process. See $logFile for details.""

# Check for Edge installations first
Write-Log ""Checking for Edge installations...""

$legacyInstalled = Test-LegacyEdgeInstalled
$chromiumInstalled = Test-ChromiumEdgeInstalled

$removedSomething = $false
$stubPath = $null

if (-not $legacyInstalled -and -not $chromiumInstalled) {
    Write-Log ""No Edge installations detected. Skipping removal.""
}

if ($chromiumInstalled) {
    Write-Log ""Chromium Edge detected. Finding ie_to_edge_stub.exe before removal.""

    $stubLocations = @(""$env:ProgramData\ie_to_edge_stub.exe"", ""$env:Public\ie_to_edge_stub.exe"")
    foreach ($loc in $stubLocations) {
        if (Test-Path $loc) {
            $stubPath = $loc
            Write-Log ""Found stub at: $loc""
            break
        }
    }

    if (!$stubPath) {
        $stubSearch = Get-ChildItem ""${env:ProgramFiles(x86)}\Microsoft\Edge"" -Filter ""ie_to_edge_stub.exe"" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($stubSearch) {
            $stubPath = $stubSearch.FullName
            Write-Log ""Found stub at: $stubPath""
        } else {
            Write-Log ""ie_to_edge_stub.exe not found in any location""
        }
    }

    if ($stubPath) {
        $scriptsDir = ""C:\ProgramData\Winhance\OpenWebSearch""
        New-Item -ItemType Directory -Path $scriptsDir -Force -ErrorAction SilentlyContinue | Out-Null
        Copy-Item $stubPath ""$scriptsDir\ie_to_edge_stub.exe"" -Force -ErrorAction SilentlyContinue
        Write-Log ""Copied ie_to_edge_stub.exe to $scriptsDir before Edge removal""
    }
}

if ($legacyInstalled) {
    Write-Log ""Legacy Edge detected. Proceeding with removal.""
    Stop-EdgeProcesses
    Remove-LegacyEdge
    $removedSomething = $true
}

if ($chromiumInstalled) {
    Write-Log ""Chromium Edge detected. Proceeding with removal.""
    Stop-EdgeProcesses
    Remove-ChromiumEdge
    $removedSomething = $true
}

# Only do cleanup if we removed something
if ($removedSomething) {
    # EdgeCore holds the files WebView2 and Copilot hard-link to, so only the WebView2 removal below deletes it
    Write-Log ""Starting cleanup of Microsoft Edge folders""
    $edgeFolders = Get-ChildItem -Path ""$env:SystemDrive\Program Files (x86)\Microsoft"" -Directory -Filter ""Edge"" -ErrorAction SilentlyContinue
    if ($edgeFolders) {
        Write-Log ""Found $($edgeFolders.Count) Edge-related folder(s) to remove""
        $edgeFolders | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
        Write-Log ""Cleanup of Microsoft Edge folders completed""
    } else {
        Write-Log ""No Edge-related folders found to clean up""
    }

    Remove-EdgeShortcuts
    Remove-EdgeRegistryKeys
    Remove-AdditionalEdgeFolders
} elseif (Test-Path ""HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft Edge"") {
    # winget still lists Edge as installed while this uninstall entry survives the files
    Write-Log ""Edge files are gone but its uninstall entry remains. Cleaning up registry.""
    Remove-EdgeRegistryKeys
}

if ($RemoveWebView2 -and (Test-WebView2Installed)) {
    Write-Log ""WebView2 or EdgeUpdate detected. Proceeding with removal.""
    Remove-WebView2AndEdgeUpdate
}

# Redirect MSEdgeHTM to our stub so links resolving through it go to the default browser
$stubPath = ""C:\ProgramData\Winhance\OpenWebSearch\ie_to_edge_stub.exe""
if (Test-Path $stubPath) {
    reg.exe add ""HKCR\MSEdgeHTM\shell\open\command"" /f /ve /d """"""`""$stubPath`"""""" %1"" 2>&1 | Out-Null
    Write-Log ""Redirected MSEdgeHTM to ie_to_edge_stub.exe""
} else {
    reg.exe delete ""HKCR\MSEdgeHTM"" /f 2>&1 | Out-Null
    Write-Log ""Removed MSEdgeHTM (stub not available)""
}

# Install or repair Edge protocol redirect (runs even if Edge was already removed, only acts if redirect is missing or broken)
Install-EdgeProtocolRedirect

if (-not (Test-ChromiumEdgeInstalled)) {
    Remove-EdgeTaskbarPins
}

# Always check for and delete Edge scheduled tasks
Write-Log ""Checking for Edge scheduled tasks""
try {
    $edgeTasks = Get-ScheduledTask -TaskName ""*Edge*"" -ErrorAction SilentlyContinue
    if ($edgeTasks) {
        foreach ($task in $edgeTasks) {
            # EdgeUpdate's own tasks keep WebView2 updated unless WebView2 is being removed too
            if ($task.TaskName -eq ""EdgeRemoval"" -or ($task.TaskName -like ""MicrosoftEdgeUpdate*"" -and -not $RemoveWebView2)) {
                Write-Log ""Skipping task: $($task.TaskName)""
                continue
            }
            
            Write-Log ""Found Edge scheduled task: $($task.TaskName) - State: $($task.State)""
            try {
                Unregister-ScheduledTask -TaskName $task.TaskName -TaskPath $task.TaskPath -Confirm:$false -ErrorAction SilentlyContinue
                Write-Log ""Deleted scheduled task: $($task.TaskName)""
            }
            catch {
                Write-Log ""Failed to delete scheduled task: $($task.TaskName) - $($_.Exception.Message)""
            }
        }
    } else {
        Write-Log ""No Edge scheduled tasks found""
    }
}
catch {
    Write-Log ""Failed to check scheduled tasks: $($_.Exception.Message)""
}

Write-Log ""Done.""
Write-Host ""Done. See $logFile for details.""

";
    }
}

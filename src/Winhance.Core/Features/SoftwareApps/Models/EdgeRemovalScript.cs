namespace Winhance.Core.Features.SoftwareApps.Models;

public static class EdgeRemovalScript
{
    public const string ScriptName = "EdgeRemoval";

    public const string ScriptVersion = "2.4";

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
      Detects and removes:
      - Legacy UWP Edge (pre-Chromium)
      - Chromium-based Edge (current version)
      " + (removeWebView2
            ? "- EdgeWebView2, EdgeUpdate and EdgeCore, so Widgets and apps that need WebView2 stop working"
            : "- EdgeUpdate and EdgeWebView2 are NOT removed, so WebView2 keeps getting updates") + @"
      Runs in any context: user sessions, SYSTEM account, or scheduled tasks.
      With -Image it cleans a mounted offline Windows image instead: the hive roots are already-loaded registry paths.

  .NOTES
      Source: https://github.com/memstechtips/Winhance
      Requires Windows 10/11, PowerShell 5.1+ and Administrator privileges (auto-elevates).

      Credits:
      - Legacy Edge removal based on work by ishad0w: https://gist.github.com/ishad0w/d25ca52eb04dbefba8087a344a69c79c
      - Chromium Edge removal based on work by FR33THY: https://github.com/FR33THYFR33THY/Ultimate-Windows-Optimization-Guide/blob/main/6%20Windows/14%20Edge.ps1
      - Edge protocol redirect based on OpenWebSearch by AveYo: https://github.com/AveYo/fox/blob/main/OpenWebSearch.cmd
#>

param([string]$Image = '',
      [string]$SoftwareRoot = 'HKEY_LOCAL_MACHINE\SOFTWARE',
      [string]$SystemRoot = 'HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet',
      [string]$UserRoot = 'HKEY_CURRENT_USER')

If (-not $Image -and !([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]'Administrator')) {
    Try { Start-Process PowerShell.exe -ArgumentList ('-NoProfile -ExecutionPolicy Bypass -File ""{0}""' -f $PSCommandPath) -Verb RunAs }
    Catch { Write-Host 'Failed to run as Administrator. Please rerun with elevated privileges.' }
    Exit
}

" + (removeWebView2 ? FullModeLine : "$RemoveWebView2 = $false") + @"

$Offline = $Image -ne ''
$logFolder = 'C:\ProgramData\Winhance\Logs'
$logFile = ""$logFolder\EdgeRemovalLog.txt""
$logHint = if ($Offline) { '' } else { "" See $logFile for details."" }
if (-not $Offline) { New-Item -ItemType Directory -Path $logFolder -Force | Out-Null }

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

function Invoke-Reg { reg.exe @args 2>&1 | Out-Null }

$root = if ($Offline) { $Image.TrimEnd('\') } else { $env:SystemDrive }
$programFiles = if ($Offline) { ""$root\Program Files"" } else { $env:ProgramFiles }
$x86 = if ($Offline) { ""$root\Program Files (x86)"" } else { ${env:ProgramFiles(x86)} }
$programData = if ($Offline) { ""$root\ProgramData"" } else { $env:ProgramData }
$windows = if ($Offline) { ""$root\Windows"" } else { $env:SystemRoot }
$users = ""$root\Users""
$sw = ""Registry::$SoftwareRoot""
$sys = ""Registry::$SystemRoot""
$usr = ""Registry::$UserRoot""
$classes = if ($Offline) { ""$SoftwareRoot\Classes"" } else { 'HKCR' }
# Where the redirect files are written, and the path the registry values point at once the image runs
$owsDir = ""$programData\Winhance\OpenWebSearch""
$runtimeStubPath = 'C:\ProgramData\Winhance\OpenWebSearch\ie_to_edge_stub.exe'
$cbsPackages = ""$sw\Microsoft\Windows\CurrentVersion\Component Based Servicing\Packages""
$webView2Folders = ""$x86\Microsoft\EdgeUpdate"", ""$x86\Microsoft\EdgeWebView"", ""$x86\Microsoft\EdgeCore""

function Remove-Path([string]$Path) {
    if ($Offline -and (Test-Path $Path)) {
        # TrustedInstaller owns these files in the image
        $isFolder = Test-Path $Path -PathType Container
        takeown /f $Path @(if ($isFolder) { '/r', '/d', 'y' }) 2>&1 | Out-Null
        icacls $Path /grant '*S-1-5-32-544:F' @(if ($isFolder) { '/t' }) 2>&1 | Out-Null
    }
    Remove-Item $Path -Recurse -Force -ErrorAction SilentlyContinue
}

function Get-LegacyEdgePackages {
    Get-ChildItem $cbsPackages -Name -ErrorAction SilentlyContinue | Where-Object { $_ -match 'Microsoft-Windows-Internet-Browser-Package' -and $_ -match '~~' }
}

function Test-LegacyEdgeInstalled {
    foreach ($package in Get-LegacyEdgePackages) {
        if ((& dism /online /Get-PackageInfo /PackageName:$package 2>$null) -match 'State.*Installed') { return $true }
    }
    return $false
}

function Test-ChromiumEdgeInstalled {
    # Only the Edge folder counts: EdgeUpdate and EdgeCore stay for WebView2 and Copilot
    foreach ($dir in $programFiles, $x86) {
        if (Test-Path ""$dir\Microsoft\Edge"") { return $true }
    }
    if ($Offline) { return $false }
    try { return $null -ne (Get-WmiObject -Class Win32_InstalledStoreProgram -Filter ""Name like '%Microsoft.MicrosoftEdge.Stable%'"" -ErrorAction SilentlyContinue) }
    catch { return $false }
}

function Stop-EdgeProcesses {
    Write-Log 'Stopping Edge-related processes and services'
    # A running updater reinstalls Edge from its provisioned package right after the removal
    Stop-Service -Name ""edgeupdate"", ""edgeupdatem"" -Force -ErrorAction SilentlyContinue
    $stop = ""MicrosoftEdgeUpdate"", ""msedge"", ""Resume"", ""CrossDeviceResume""
    # WebView2 and the apps hosting it only close when WebView2 itself is being removed
    if ($RemoveWebView2) { $stop += ""msedgewebview2"", ""OneDrive"", ""WidgetService"", ""Widgets"" }
    foreach ($name in $stop) {
        $count = (Get-Process -Name $name -ErrorAction SilentlyContinue).Count
        if ($count -gt 0) {
            Stop-Process -Name $name -Force -ErrorAction SilentlyContinue
            Write-Log ""Stopped $count instance(s) of $name""
        }
    }
}

function Remove-LegacyEdge {
    Write-Log 'Starting Legacy Edge/UWP Edge removal process'
    $package = Get-LegacyEdgePackages | Select-Object -First 1
    $packagePath = ""$cbsPackages\$package""
    Set-ItemProperty -Path $packagePath -Name Visibility -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue
    Remove-Item ""$packagePath\Owners"" -Recurse -Force -ErrorAction SilentlyContinue
    foreach ($attempt in 1, 2) {
        Write-Log ""Removing Legacy Edge package via DISM, attempt $attempt (30-second timeout)""
        $dism = Start-Process dism.exe -ArgumentList '/online', '/Remove-Package', ""/PackageName:$package"" -NoNewWindow -PassThru
        if (-not $dism) { break }
        if ($dism.WaitForExit(30000)) { Write-Log 'DISM completed successfully'; break }
        Write-Log 'DISM timed out after 30 seconds, killing process'
        $dism.Kill()
        Start-Sleep 2
    }
    Get-AppxPackage -AllUsers Microsoft.MicrosoftEdge | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue | Out-Null
    Write-Log 'Legacy Edge/UWP Edge removal process completed'
}

function Remove-EdgeShortcuts {
    Write-Log 'Starting Edge shortcuts cleanup'
    $quickLaunch = 'AppData\Roaming\Microsoft\Internet Explorer\Quick Launch'
    $shortcuts = @(""$programData\Microsoft\Windows\Start Menu\Programs\Microsoft Edge.lnk"", ""$users\Public\Desktop\Microsoft Edge.lnk"")
    # Default is a hidden folder, which only the image has to clean
    foreach ($userProfile in Get-ChildItem $users -Directory -Force:$Offline | Where-Object { Test-Path ""$($_.FullName)\NTUSER.DAT"" }) {
        $shortcuts += $quickLaunch, ""$quickLaunch\User Pinned\TaskBar"", ""$quickLaunch\User Pinned\TaskBar\Tombstones"", 'Desktop', 'AppData\Roaming\Microsoft\Windows\Start Menu\Programs' |
            ForEach-Object { ""$($userProfile.FullName)\$_\Microsoft Edge.lnk"" }
    }
    $removed = 0
    foreach ($path in $shortcuts) {
        if (Test-Path -Path $path -PathType Leaf) { Remove-Item -Path $path -Force -ErrorAction SilentlyContinue; $removed++ }
    }
    Write-Log ""Removed $removed Edge shortcut(s)""
}

function Install-EdgeProtocolRedirect {
    Write-Log 'Installing Edge protocol redirect using OpenWebSearch'
    $scriptsDir = $owsDir
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

    $build = if ($Offline) { [int](Get-ItemProperty ""$sw\Microsoft\Windows NT\CurrentVersion"").CurrentBuildNumber } else { [Environment]::OSVersion.Version.Build }
    $conhostFlags = if ($build -gt 25179) { '--width 1 --height 1' } else { '--headless' }
    $conhost = if ($Offline) { 'C:\Windows\system32\conhost.exe' } else { ""$env:SystemRoot\system32\conhost.exe"" }
    $ifeo = ""$SoftwareRoot\Microsoft\Windows NT\CurrentVersion\Image File Execution Options""
    Write-Log 'Configuring registry entries for Edge protocol redirect'
    Invoke-Reg add ""$classes\microsoft-edge"" /f /ve /d 'URL:microsoft-edge'
    Invoke-Reg add ""$classes\microsoft-edge"" /f /v 'URL Protocol' /d `""`""
    Invoke-Reg add ""$classes\microsoft-edge"" /f /v 'NoOpenWith' /d `""`""
    Invoke-Reg add ""$classes\microsoft-edge\shell\open\command"" /f /ve /d ""$runtimeStubPath %1""
    Invoke-Reg add ""$ifeo\ie_to_edge_stub.exe"" /f /v UseFilter /d 1 /t reg_dword
    Invoke-Reg add ""$ifeo\ie_to_edge_stub.exe\0"" /f /v FilterFullPath /d $runtimeStubPath
    Invoke-Reg add ""$ifeo\ie_to_edge_stub.exe\0"" /f /v Debugger /d ""$conhost $conhostFlags C:\ProgramData\Winhance\OpenWebSearch\OpenWebSearch.cmd""
    # Remove msedge.exe IFEO entries from previous versions to prevent Edge reinstall issues
    Invoke-Reg delete ""$ifeo\msedge.exe"" /f
    # The repair task is registered by the run at setup time
    if ($Offline) { return }

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

    Register-ScheduledTask -TaskName 'OpenWebSearchRepair' -TaskPath '\Winhance\' -Force `
        -Action (New-ScheduledTaskAction -Execute powershell.exe -Argument ""-ExecutionPolicy Bypass -NoProfile -Command `""iex([IO.File]::ReadAllText('$repairScriptPath'))`"""") `
        -Trigger (New-ScheduledTaskTrigger -AtLogon) `
        -Settings (New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable) `
        -Principal (New-ScheduledTaskPrincipal -UserId SYSTEM -LogonType ServiceAccount -RunLevel Highest) | Out-Null
    Write-Log 'Registered OpenWebSearchRepair scheduled task (runs at logon)'
}

# Edge's uninstaller unpins its taskbar icon only for the user it runs as, and deleting the pinned .lnk does not
# unpin it, so a SYSTEM run leaves a blank icon. A running Explorer drops the stale pin at the next sign-in.
# Taskband keeps the pins as two parallel lists: Favorites holds 0x00 + DWORD size + item per pin and ends in 0xFF,
# FavoritesResolve holds DWORD size + shell link per pin.
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
}

function Remove-ChromiumEdge {
    Write-Log 'Starting Edge Chromium uninstallation process'
    # The uninstaller only runs with this folder and file in place
    $edgePath = ""$env:SystemRoot\SystemApps\Microsoft.MicrosoftEdge_8wekyb3d8bbwe""
    New-Item -Path $edgePath -ItemType Directory -ErrorAction SilentlyContinue | Out-Null
    New-Item -Path $edgePath -ItemType File -Name MicrosoftEdge.exe -ErrorAction SilentlyContinue | Out-Null
    # The Enterprise MSI and the normal version need different uninstall arguments
    $uninstallCount = 0
    foreach ($key in Get-ChildItem 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall') {
        $entry = Get-ItemProperty $key.PSPath -ErrorAction SilentlyContinue
        if ($entry.DisplayName -ne 'Microsoft Edge' -or -not $entry.UninstallString) { continue }
        $uninstallCount++
        # Kill any Edge processes that may have respawned since initial stop
        Stop-EdgeProcesses
        if ($entry.UninstallString -like '*msiexec*') {
            Write-Log 'Executing MSI uninstaller for Edge'
            $flags = '/quiet'
        } else {
            Write-Log 'Executing standard uninstaller for Edge'
            $flags = '--force-uninstall --silent'
        }
        Start-Process cmd.exe ""/c $($entry.UninstallString) $flags"" -WindowStyle Hidden -Wait | Out-Null
    }
    Write-Log ""Executed $uninstallCount Edge uninstaller(s)""
    # While it stays provisioned Windows registers it again for every user
    Write-Log 'Deprovisioning and removing UWP Edge Chromium package'
    Get-AppxProvisionedPackage -Online | Where-Object { $_.DisplayName -eq 'Microsoft.MicrosoftEdge.Stable' } | Remove-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue | Out-Null
    Get-AppxPackage -AllUsers Microsoft.MicrosoftEdge.Stable | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue | Out-Null
    Remove-Item -Recurse -Force $edgePath -ErrorAction SilentlyContinue | Out-Null
    Write-Log 'Edge Chromium uninstallation process completed'
}

# Windows leaves the MicrosoftEdge key owned by TrustedInstaller, so even an administrator can only delete it after
# taking ownership, which needs the take-ownership and restore privileges switched on first. Process.SetPrivilege is
# .NET Framework's own helper for that; an Add-Type P/Invoke failed to compile inside the WIMUtil image build
function Remove-OwnedRegistryKey([string]$path) {
    $setPrivilege = [System.Diagnostics.Process].GetMethod('SetPrivilege', [Reflection.BindingFlags]'NonPublic,Static')
    'SeTakeOwnershipPrivilege', 'SeRestorePrivilege' | ForEach-Object { [void]$setPrivilege.Invoke($null, [object[]]([string]$_, 2)) }
    $subKey = $path -replace '^Registry::HKEY_LOCAL_MACHINE\\'
    $admins = [Security.Principal.NTAccount]'BUILTIN\Administrators'
    $key = [Microsoft.Win32.Registry]::LocalMachine.OpenSubKey($subKey, 'ReadWriteSubTree', 'TakeOwnership')
    $acl = New-Object Security.AccessControl.RegistrySecurity
    $acl.SetOwner($admins)
    $key.SetAccessControl($acl)
    $key.Close()
    $key = [Microsoft.Win32.Registry]::LocalMachine.OpenSubKey($subKey, 'ReadWriteSubTree', 'ChangePermissions')
    $acl = $key.GetAccessControl()
    $acl.AddAccessRule((New-Object Security.AccessControl.RegistryAccessRule($admins, 'FullControl', 'ContainerInherit', 'None', 'Allow')))
    $key.SetAccessControl($acl)
    $key.Close()
    [Microsoft.Win32.Registry]::LocalMachine.DeleteSubKeyTree($subKey)
}

function Remove-RegistryKeys([string[]]$paths, [string]$classPattern) {
    $paths += Get-ChildItem ""$sw\Classes"" -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -like $classPattern } | ForEach-Object PSPath
    $removed = 0
    foreach ($path in $paths) {
        if (-not (Test-Path $path)) { continue }
        Remove-Item $path -Recurse -Force -ErrorAction SilentlyContinue
        if ((Test-Path $path) -and $path -like 'Registry::HKEY_LOCAL_MACHINE\*') { try { Remove-OwnedRegistryKey $path } catch { Write-Log ""Could not remove ${path}: $($_.Exception.Message)"" } }
        $removed++
    }
    $removed
}

function Remove-EdgeRegistryKeys {
    Write-Log 'Starting comprehensive Edge registry cleanup'
    $removed = Remove-RegistryKeys @(
        ""$sw\Microsoft\Edge"",
        ""$sw\WOW6432Node\Microsoft\Edge"",
        ""$usr\Software\Microsoft\Edge"",
        ""$sw\Clients\StartMenuInternet\Microsoft Edge"",
        ""$sw\Microsoft\Windows\CurrentVersion\App Paths\msedge.exe"",
        ""$sw\Microsoft\Windows\CurrentVersion\MicrosoftEdge"",
        ""$sw\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft Edge"",
        ""$sw\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft Edge"",
        ""$sys\Services\Eventlog\Application\Edge""
    ) 'MSEdge*'
    Write-Log ""Removed $removed registry key(s)""

    foreach ($value in ""$sw\RegisteredApplications|Microsoft Edge"",
        ""$usr\Software\Microsoft\Windows\CurrentVersion\Explorer\FeatureUsage\AppLaunch|MSEdge"",
        ""$usr\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Compatibility Assistant\Store|C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"") {
        $path, $name = $value -split '\|'
        Remove-ItemProperty -Path $path -Name $name -Force -ErrorAction SilentlyContinue
    }

    $muiCache = ""$usr\SOFTWARE\Classes\Local Settings\Software\Microsoft\Windows\Shell\MuiCache""
    (Get-ItemProperty $muiCache -ErrorAction SilentlyContinue).PSObject.Properties | Where-Object { $_.Name -like '*Edge*' } |
        ForEach-Object { Remove-ItemProperty -Path $muiCache -Name $_.Name -Force -ErrorAction SilentlyContinue }
}

function Remove-AdditionalEdgeFolders {
    Write-Log 'Starting additional Edge folder cleanup'
    $folders = @(""$windows\Temp\MsEdgeCrashpad"") + (Get-ChildItem $users -Directory -Force:$Offline -ErrorAction SilentlyContinue |
        Where-Object { Test-Path ""$($_.FullName)\NTUSER.DAT"" } | ForEach-Object { ""$($_.FullName)\AppData\Local\Microsoft\Edge"" })
    $removed = 0
    foreach ($folder in $folders) {
        if (Test-Path $folder) {
            Write-Log ""Removing: $folder""
            Remove-Item $folder -Recurse -Force -ErrorAction SilentlyContinue
            $removed++
        }
    }
    Write-Log ""Removed $removed additional Edge folder(s)""
}

function Test-WebView2Installed {
    [bool](($webView2Folders + ""$sw\WOW6432Node\Microsoft\EdgeUpdate"", ""$sys\Services\edgeupdate"", ""$sys\Services\edgeupdatem"") |
        Where-Object { Test-Path $_ })
}

function Remove-WebView2AndEdgeUpdate {
    $folders = $webView2Folders + ""$programData\Microsoft\EdgeUpdate""
    if ($Offline) {
        $folders | Where-Object { Test-Path $_ } | ForEach-Object { Remove-Path $_ }
    } else {
        # EdgeUpdate goes first: while it exists it re-registers a deleted WebView2 and leaves it half installed
        Write-Log 'Removing EdgeUpdate services and tasks'
        foreach ($service in 'edgeupdate', 'edgeupdatem') {
            if (Get-Service -Name $service -ErrorAction SilentlyContinue) {
                Stop-Service -Name $service -Force -ErrorAction SilentlyContinue
                sc.exe delete $service 2>&1 | Out-Null
                Write-Log ""Deleted service: $service""
            }
        }
        Get-ScheduledTask -TaskName 'MicrosoftEdgeUpdate*' -ErrorAction SilentlyContinue | ForEach-Object {
            Unregister-ScheduledTask -TaskName $_.TaskName -TaskPath $_.TaskPath -Confirm:$false -ErrorAction SilentlyContinue
            Write-Log ""Deleted scheduled task: $($_.TaskName)""
        }

        # Start search and Widgets hold WebView2 files open and Windows restarts them, so stop and delete in a loop
        for ($attempt = 1; $attempt -le 5 -and ($folders | Where-Object { Test-Path $_ }); $attempt++) {
            Get-Process -Name MicrosoftEdgeUpdate, msedgewebview2, SearchHost, Widgets, WidgetBoard, WidgetService -ErrorAction SilentlyContinue |
                Stop-Process -Force -ErrorAction SilentlyContinue
            Start-Sleep -Seconds 2
            $folders | Where-Object { Test-Path $_ } | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
    foreach ($folder in $folders) {
        if (Test-Path $folder) { Write-Log ""Could not fully remove: $folder"" } else { Write-Log ""Removed: $folder"" }
    }

    $keys = @(
        ""$sw\WOW6432Node\Microsoft\EdgeUpdate"",
        ""$sw\Microsoft\EdgeUpdate"",
        ""$usr\Software\Microsoft\EdgeUpdate"",
        ""$sw\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft EdgeWebView"",
        ""$sw\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft EdgeWebView"",
        ""$sys\Services\EventLog\Application\edgeupdate"",
        ""$sys\Services\EventLog\Application\edgeupdatem""
    )
    # A live run deletes the services with sc.exe
    if ($Offline) { $keys += ""$sys\Services\edgeupdate"", ""$sys\Services\edgeupdatem"" }
    $removed = Remove-RegistryKeys $keys 'MicrosoftEdgeUpdate*'
    Write-Log ""Removed $removed EdgeUpdate and WebView2 registry key(s)""
}

Write-Host ""Starting Edge removal process.$logHint""
Write-Log 'Checking for Edge installations...'

$legacyInstalled = -not $Offline -and (Test-LegacyEdgeInstalled)
$chromiumInstalled = Test-ChromiumEdgeInstalled
$removedSomething = $legacyInstalled -or $chromiumInstalled
if (-not $removedSomething) { Write-Log 'No Edge installations detected. Skipping removal.' }

if ($chromiumInstalled) {
    Write-Log 'Chromium Edge detected. Finding ie_to_edge_stub.exe before removal.'
    $savedStub = ""$owsDir\ie_to_edge_stub.exe""
    if (Test-Path $savedStub) {
        Write-Log ""Stub already saved at: $savedStub""
    } else {
        $stubPath = (Get-ChildItem ""$x86\Microsoft\Edge"" -Filter ie_to_edge_stub.exe -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1).FullName
        $signature = if ($stubPath) { Get-AuthenticodeSignature $stubPath }
        if ($stubPath -and $signature.Status -eq 'Valid' -and $signature.SignerCertificate.Subject -match 'O=Microsoft Corporation') {
            Write-Log ""Found stub at: $stubPath""
            New-Item -ItemType Directory -Path $owsDir -Force -ErrorAction SilentlyContinue | Out-Null
            Copy-Item $stubPath $savedStub -Force -ErrorAction SilentlyContinue
            Write-Log ""Copied ie_to_edge_stub.exe to $owsDir before Edge removal""
        } else {
            Write-Log 'No Microsoft-signed ie_to_edge_stub.exe found in the Edge folder'
        }
    }
}

if ($legacyInstalled) {
    Write-Log 'Legacy Edge detected. Proceeding with removal.'
    Stop-EdgeProcesses
    Remove-LegacyEdge
}

if ($chromiumInstalled) {
    Write-Log 'Chromium Edge detected. Proceeding with removal.'
    if ($Offline) {
        Remove-Path ""$programFiles\Microsoft\Edge""
    } else {
        Stop-EdgeProcesses
        Remove-ChromiumEdge
    }
}

if ($removedSomething) {
    # EdgeCore holds the files WebView2 and Copilot hard-link to, so only the WebView2 removal below deletes it
    Write-Log 'Starting cleanup of Microsoft Edge folders'
    Remove-Path ""$x86\Microsoft\Edge""
    Remove-EdgeShortcuts
    Remove-EdgeRegistryKeys
    Remove-AdditionalEdgeFolders
} elseif (Test-Path ""$sw\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft Edge"") {
    # winget still lists Edge as installed while this uninstall entry survives the files
    Write-Log 'Edge files are gone but its uninstall entry remains. Cleaning up registry.'
    Remove-EdgeRegistryKeys
}

if ($RemoveWebView2 -and (Test-WebView2Installed)) {
    Write-Log 'WebView2 or EdgeUpdate detected. Proceeding with removal.'
    Remove-WebView2AndEdgeUpdate
}

# Redirect MSEdgeHTM to our stub so links resolving through it go to the default browser
$stubPath = ""$owsDir\ie_to_edge_stub.exe""
if (Test-Path $stubPath) {
    Invoke-Reg add ""$classes\MSEdgeHTM\shell\open\command"" /f /ve /d """"""`""$runtimeStubPath`"" %1""
    Write-Log 'Redirected MSEdgeHTM to ie_to_edge_stub.exe'
} else {
    Invoke-Reg delete ""$classes\MSEdgeHTM"" /f
    Write-Log 'Removed MSEdgeHTM (stub not available)'
}

# Install or repair Edge protocol redirect (runs even if Edge was already removed, only acts if redirect is missing or broken)
Install-EdgeProtocolRedirect

if (-not $Offline -and -not (Test-ChromiumEdgeInstalled)) {
    Remove-EdgeTaskbarPins
}

if (-not $Offline) {
    Write-Log 'Checking for Edge scheduled tasks'
    try {
        foreach ($task in Get-ScheduledTask -TaskName '*Edge*' -ErrorAction SilentlyContinue) {
            # EdgeUpdate's own tasks keep WebView2 updated unless WebView2 is being removed too
            if ($task.TaskName -eq 'EdgeRemoval' -or ($task.TaskName -like ""MicrosoftEdgeUpdate*"" -and -not $RemoveWebView2)) {
                Write-Log ""Skipping task: $($task.TaskName)""
                continue
            }
            try {
                Unregister-ScheduledTask -TaskName $task.TaskName -TaskPath $task.TaskPath -Confirm:$false -ErrorAction SilentlyContinue
                Write-Log ""Deleted scheduled task: $($task.TaskName)""
            }
            catch { Write-Log ""Failed to delete scheduled task: $($task.TaskName) - $($_.Exception.Message)"" }
        }
    }
    catch { Write-Log ""Failed to check scheduled tasks: $($_.Exception.Message)"" }
}

Write-Log ""Done.$logHint""
";
    }
}

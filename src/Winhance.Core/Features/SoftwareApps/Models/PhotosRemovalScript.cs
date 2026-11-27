using System.Text;

namespace Winhance.Core.Features.SoftwareApps.Models;

public static class PhotosRemovalScript
{
    public const string CompanionPackage = "Microsoft.OneDrive.Photos";

    public static bool Includes(IEnumerable<string> packages) => packages.Contains("Microsoft.Windows.Photos", StringComparer.OrdinalIgnoreCase);

    // Runs under the owning user's limited token, never under SYSTEM or an elevated user.
    private const string UserScript = """
        $ErrorActionPreference = 'Stop'
        $key = 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\ODPhotosOneDriveSetup.exe'
        if (-not (Test-Path -LiteralPath $key)) { exit 0 }
        $entry = Get-ItemProperty -LiteralPath $key
        if ($entry.DisplayVersion -notmatch '^\d+\.\d+\.\d+\.\d+$') { throw 'Invalid OneDrive Photos version' }
        $setup = Join-Path $env:LOCALAPPDATA "Microsoft\OneDrive\$($entry.DisplayVersion)\OneDriveSetup.exe"
        if (-not (Test-Path -LiteralPath $setup -PathType Leaf)) {
            # A prior full OneDrive removal can leave the companion's registration with no executable behind it.
            $oneDriveKeys = @('HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\OneDriveSetup.exe',
                'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\OneDriveSetup.exe',
                'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\OneDriveSetup.exe')
            $oneDriveFiles = @((Join-Path $env:LOCALAPPDATA 'Microsoft\OneDrive\OneDrive.exe'),
                (Join-Path $env:ProgramFiles 'Microsoft OneDrive\OneDrive.exe'))
            if (${env:ProgramFiles(x86)}) { $oneDriveFiles += Join-Path ${env:ProgramFiles(x86)} 'Microsoft OneDrive\OneDrive.exe' }
            if (@($oneDriveKeys + $oneDriveFiles | Where-Object { Test-Path -LiteralPath $_ }).Count -gt 0) {
                throw 'OneDrive Photos installer missing while OneDrive is still installed.'
            }
            Get-AppxPackage -Name 'Microsoft.OneDrive.Photos' | Remove-AppxPackage
            if (Get-AppxPackage -Name 'Microsoft.OneDrive.Photos') { throw 'Orphan OneDrive Photos package could not be removed.' }
            Remove-Item -LiteralPath $key
            exit 0
        }
        $signature = Get-AuthenticodeSignature -LiteralPath $setup
        if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch '(^|, )CN=Microsoft Corporation(,|$)') {
            throw 'OneDrive Photos installer is not signed by Microsoft'
        }
        $process = Start-Process -FilePath $setup -ArgumentList '/uninstall','/partialodphotosremoval' -Wait -PassThru
        # OneDrive setup can return a nonzero code even when the companion has been removed.
        if (Test-Path -LiteralPath $key) { throw "OneDrive Photos uninstall failed (exit $($process.ExitCode))" }
        """;

    public static string GetScript() => $$"""
        # Remove standard Photos and its separate OneDrive companion, without uninstalling OneDrive itself.
        $ErrorActionPreference = 'Stop'
        $ProgressPreference = 'SilentlyContinue'
        # Both the full OneDrive removal and this partial uninstaller touch its AppData installation.
        $gate = [Threading.Mutex]::new($false, 'Global\WinhanceOneDriveRemoval')
        try {
        try { $held = $gate.WaitOne([TimeSpan]::FromMinutes(5)) } catch [Threading.AbandonedMutexException] { $held = $true }
        if (-not $held) { throw 'Timed out waiting for OneDrive removal.' }
        $userAction = '{{Convert.ToBase64String(Encoding.Unicode.GetBytes(UserScript))}}'
        $keySuffix = 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\ODPhotosOneDriveSetup.exe'
        $profiles = Get-CimInstance Win32_UserProfile | Where-Object { -not $_.Special -and $_.SID -match '^S-1-\d+(-\d+)+$' }
        $failed = $false
        $companionOwners = @(Get-AppxPackage -AllUsers -Name 'Microsoft.OneDrive.Photos' | ForEach-Object PackageUserInformation |
            Where-Object InstallState -eq 'Installed' | ForEach-Object { [string]$_.UserSecurityId })
        foreach ($profile in $profiles) {
            $sid = $profile.SID
            $key = "Registry::HKEY_USERS\$sid\$keySuffix"
            if (-not $profile.Loaded) {
                if ($sid -in $companionOwners) { $failed = $true; Write-Warning "OneDrive Photos owner $sid is not signed in" }
                continue
            }
            if (-not (Test-Path -LiteralPath $key)) { continue }
            # The installer lives in AppData. Running it in a limited interactive task also works from SYSTEM.
            $name = "WinhancePhotosOnce-$sid-$([Guid]::NewGuid().ToString('N'))"
            try {
                $action = New-ScheduledTaskAction -Execute "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -Argument "-NoProfile -EncodedCommand $userAction"
                $principal = New-ScheduledTaskPrincipal -UserId $sid -LogonType Interactive -RunLevel Limited
                $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit (New-TimeSpan -Minutes 3)
                Register-ScheduledTask -TaskName $name -Action $action -Principal $principal -Settings $settings | Out-Null
                $started = Get-Date
                Start-ScheduledTask -TaskName $name
                $deadline = $started.AddSeconds(120)
                do {
                    Start-Sleep -Seconds 1
                    $task = Get-ScheduledTask -TaskName $name
                    $info = Get-ScheduledTaskInfo -TaskName $name
                    if ($task.State -ne 'Running' -and $task.State -ne 'Queued' -and $info.LastRunTime -ge $started.AddSeconds(-2)) { break }
                } while ((Get-Date) -lt $deadline)
                if ($task.State -in 'Running','Queued' -or (Test-Path -LiteralPath $key)) {
                    throw "OneDrive Photos is still installed for $sid (task result $($info.LastTaskResult))"
                }
                Write-Host "Removed OneDrive Photos for $sid"
            } catch {
                Write-Warning $_.Exception.Message
                $failed = $true
            } finally {
                Stop-ScheduledTask -TaskName $name -ErrorAction SilentlyContinue
                Unregister-ScheduledTask -TaskName $name -Confirm:$false -ErrorAction SilentlyContinue
            }
        }
        # Do not strip the companion registration out from under a user whose installer could not run.
        $packages = @('Microsoft.Windows.Photos')
        if (-not $failed) { $packages += 'Microsoft.OneDrive.Photos' }
        foreach ($name in $packages) {
            Get-AppxProvisionedPackage -Online | Where-Object DisplayName -eq $name | ForEach-Object {
                Remove-AppxProvisionedPackage -Online -PackageName $_.PackageName | Out-Null
            }
            Get-AppxPackage -AllUsers -Name $name | ForEach-Object {
                Remove-AppxPackage -AllUsers -Package $_.PackageFullName
            }
            if (Get-AppxPackage -AllUsers -Name $name) { $failed = $true }
        }
        if ($failed) { throw 'Photos removal incomplete; the owning user must be signed in to remove the OneDrive companion.' }
        Write-Host 'Photos removal complete.'
        } finally {
            if ($held) { $gate.ReleaseMutex() }
            $gate.Dispose()
        }
        """.ReplaceLineEndings("\n");
}

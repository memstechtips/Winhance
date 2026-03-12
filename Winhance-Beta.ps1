# PowerShell script that downloads the latest beta (pre-release) version of Winhance and runs the Installer

function Get-FileFromWeb {
    param ([Parameter(Mandatory)][string]$URL, [Parameter(Mandatory)][string]$File)
    function Show-Progress {
        param ([Parameter(Mandatory)][Single]$TotalValue, [Parameter(Mandatory)][Single]$CurrentValue, [Parameter(Mandatory)][string]$ProgressText, [Parameter()][int]$BarSize = 10, [Parameter()][switch]$Complete)
        $percent = $CurrentValue / $TotalValue
        $percentComplete = $percent * 100
        if ($psISE) { Write-Progress "$ProgressText" -id 0 -percentComplete $percentComplete }
        else { Write-Host -NoNewLine "`r$ProgressText $(''.PadRight($BarSize * $percent, [char]9608).PadRight($BarSize, [char]9617)) $($percentComplete.ToString('##0.00').PadLeft(6)) % " }
    }
    try {
        $request = [System.Net.HttpWebRequest]::Create($URL)
        $response = $request.GetResponse()
        if ($response.StatusCode -eq 401 -or $response.StatusCode -eq 403 -or $response.StatusCode -eq 404) { throw "Remote file either doesn't exist, is unauthorized, or is forbidden for '$URL'." }
        if ($File -match '^\.\\') { $File = Join-Path (Get-Location -PSProvider 'FileSystem') ($File -Split '^\.')[1] }
        if ($File -and !(Split-Path $File)) { $File = Join-Path (Get-Location -PSProvider 'FileSystem') $File }
        if ($File) { $fileDirectory = $([System.IO.Path]::GetDirectoryName($File)); if (!(Test-Path($fileDirectory))) { [System.IO.Directory]::CreateDirectory($fileDirectory) | Out-Null } }
        [long]$fullSize = $response.ContentLength
        [byte[]]$buffer = new-object byte[] 1048576
        [long]$total = [long]$count = 0
        $reader = $response.GetResponseStream()
        $writer = new-object System.IO.FileStream $File, 'Create'
        do {
            $count = $reader.Read($buffer, 0, $buffer.Length)
            $writer.Write($buffer, 0, $count)
            $total += $count
            if ($fullSize -gt 0) { Show-Progress -TotalValue $fullSize -CurrentValue $total -ProgressText " Downloading Winhance Beta" }
        } while ($count -gt 0)
    }
    finally {
        $reader.Close()
        $writer.Close()
    }
}

$installerPath = "C:\ProgramData\Winhance\Unattend\WinhanceBetaInstaller.exe"

try {
    Write-Host "Checking for latest Winhance beta release..." -ForegroundColor Cyan

    # Query GitHub API for the latest pre-release
    $releases = Invoke-RestMethod -Uri "https://api.github.com/repos/memstechtips/Winhance/releases" -Headers @{ "User-Agent" = "Winhance-Beta-Downloader" }
    $betaRelease = $releases | Where-Object { $_.prerelease -eq $true } | Select-Object -First 1

    if (-not $betaRelease) {
        Write-Host "No beta release found." -ForegroundColor Yellow
        Write-Host "Press any key to exit..." -ForegroundColor Yellow
        $null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
        exit
    }

    # Find the beta installer asset
    $asset = $betaRelease.assets | Where-Object { $_.name -eq "Winhance.Installer.Beta.exe" } | Select-Object -First 1

    if (-not $asset) {
        Write-Host "Beta installer not found in release $($betaRelease.tag_name)." -ForegroundColor Red
        Write-Host "Press any key to exit..." -ForegroundColor Yellow
        $null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
        exit
    }

    $downloadUrl = $asset.browser_download_url
    Write-Host "Found beta release: $($betaRelease.tag_name)" -ForegroundColor Green

    # Prompt user for installation type
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "       Winhance Beta Installer" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  [1] Normal Installation" -ForegroundColor White
    Write-Host "  [2] Portable" -ForegroundColor White
    Write-Host ""

    do {
        $choice = Read-Host "Enter your choice (1 or 2)"
    } while ($choice -ne '1' -and $choice -ne '2')

    $installType = if ($choice -eq '1') { "Normal" } else { "Portable" }
    Write-Host ""
    Write-Host "Downloading Winhance Beta ($installType)..." -ForegroundColor Cyan
    Get-FileFromWeb -URL $downloadUrl -File $installerPath
    Write-Host ""
    Write-Host "Download completed successfully!" -ForegroundColor Green

    if ($choice -eq '1') {
        # Normal installation - silent with desktop and start menu shortcuts
        Write-Host "Installing Winhance Beta..." -ForegroundColor Cyan
        Start-Process -FilePath $installerPath -ArgumentList "/SILENT /SUPPRESSMSGBOXES /MERGETASKS=`"regularinstall\desktopicon,regularinstall\startmenuicon`"" -Wait
        Write-Host "Installation completed." -ForegroundColor Green
        $appPath = Join-Path $env:ProgramFiles "Winhance\Winhance.exe"
        if (Test-Path $appPath) {
            Write-Host "Launching Winhance..." -ForegroundColor Cyan
            Start-Process -FilePath $appPath
        }
    } else {
        # Portable installation - extract to Desktop
        Write-Host ""
        Write-Host "Extracting Winhance Beta Portable to Desktop..." -ForegroundColor Cyan
        Start-Process -FilePath $installerPath -ArgumentList "/SILENT /SUPPRESSMSGBOXES /TASKS=`"portableinstall`"" -Wait
        Write-Host "Portable installation completed." -ForegroundColor Green
        Write-Host ""
        Write-Host "Winhance Portable has been extracted to your Desktop." -ForegroundColor Cyan
        Write-Host "You can move the Winhance folder to your desired location if needed." -ForegroundColor Cyan
        $appPath = Join-Path ([System.Environment]::GetFolderPath('Desktop')) "Winhance\Winhance.exe"
        if (Test-Path $appPath) {
            Write-Host "Launching Winhance..." -ForegroundColor Cyan
            Start-Process -FilePath $appPath
        }
    }
} catch {
    Write-Host ""
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "Press any key to exit..." -ForegroundColor Yellow
    $null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
}

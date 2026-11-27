namespace Winhance.Core.Features.SoftwareApps.Models;

public static class CopilotRemovalScript
{
    public static bool Includes(IEnumerable<string> packages) =>
        packages.Contains("Microsoft.Copilot", StringComparer.OrdinalIgnoreCase) ||
        packages.Contains("Microsoft.MicrosoftOfficeHub", StringComparer.OrdinalIgnoreCase);

    public static string GetScript() => """
        $ErrorActionPreference = 'Stop'
        $gate = [Threading.Mutex]::new($false, 'Global\WinhanceCopilotRemoval')
        try {
        try { $held = $gate.WaitOne([TimeSpan]::FromMinutes(2)) } catch [Threading.AbandonedMutexException] { $held = $true }
        if (-not $held) { throw 'Timed out waiting for Copilot removal.' }
        $removeConsumer = $packages -contains 'Microsoft.Copilot'
        # On current Windows, OfficeHub can be the consumer Copilot wrapper. Older OfficeHub is a separate app.
        $wrappers = @(Get-AppxPackage -AllUsers -Name 'Microsoft.MicrosoftOfficeHub' | Where-Object {
            try {
                $manifest = Get-AppxPackageManifest -Package $_.PackageFullName
                $manifest.Package.Applications.Application.Executable -contains 'copilotapp_proxy.exe'
            } catch { $false }
        })
        if (($packages -contains 'Microsoft.MicrosoftOfficeHub') -and $wrappers) { $removeConsumer = $true }
        if (-not $removeConsumer) { return }

        $key = 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft Copilot'
        $root = Join-Path ${env:ProgramFiles(x86)} 'Microsoft\Copilot\Application'
        $failed = $false
        if (Test-Path -LiteralPath $key) {
            try {
                # Code 60 is Chromium setup's singleton acquisition failure. Edge or WebView2 may be updating.
                $retryClock = [Diagnostics.Stopwatch]::StartNew()
                while ((Test-Path -LiteralPath $key) -and $retryClock.Elapsed.TotalSeconds -lt 120) {
                    # An updater may have replaced the registered installer since the last attempt.
                    $entry = Get-ItemProperty -LiteralPath $key
                    if ($entry.DisplayVersion -notmatch '^\d+\.\d+\.\d+\.\d+$') { throw 'Invalid Copilot version' }
                    $setup = Join-Path $root "$($entry.DisplayVersion)\Installer\copilot_setup.exe"
                    if (-not (Test-Path -LiteralPath $setup -PathType Leaf)) { throw 'Copilot installer not found' }
                    $signature = Get-AuthenticodeSignature -LiteralPath $setup
                    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch '(^|, )CN=Microsoft Corporation(,|$)') {
                        throw 'Copilot uninstaller is not signed by Microsoft'
                    }
                    if ($retryClock.Elapsed.TotalSeconds -ge 120) { break }
                    $process = Start-Process -FilePath $setup -ArgumentList '--uninstall','--mscopilot','--channel=stable','--system-level','--verbose-logging','--force-uninstall','--silent' -PassThru -Wait
                    Write-Host "Copilot setup returned $($process.ExitCode)"
                    if ($process.ExitCode -ne 60 -or -not (Test-Path -LiteralPath $key)) { break }
                    $remaining = [Math]::Floor(120 - $retryClock.Elapsed.TotalSeconds)
                    if ($remaining -le 0) { break }
                    Start-Sleep -Seconds ([Math]::Min(15, $remaining))
                }
            } catch {
                Write-Warning $_.Exception.Message
                $failed = $true
            }
        }
        $executables = @(Get-ChildItem -Path (Join-Path $root 'copilotapp.exe'), (Join-Path $root '*\copilotapp.exe') -File -ErrorAction SilentlyContinue)
        if ($failed -or (Test-Path -LiteralPath $key) -or $executables) {
            throw 'The desktop Copilot installation remains. The vendor uninstaller did not remove it.'
        }
        foreach ($wrapper in $wrappers) {
            Remove-AppxPackage -AllUsers -Package $wrapper.PackageFullName
        }
        } finally {
            if ($held) { $gate.ReleaseMutex() }
            $gate.Dispose()
        }
        """.ReplaceLineEndings("\n");
}

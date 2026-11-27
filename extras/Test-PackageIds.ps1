# Lists External Apps whose winget ids are all gone from the winget source, plus spare ids that are gone.
# build-and-package.ps1 runs it as a warning: the app already hides such apps at runtime, this says which to fix.
# Exit code: 0 every app still has a live id, 1 an app has none.
# `--exact` matches case-sensitively and the source's casing drifts (eloston.ungoogled-chromium), so search and match here.
$catalogs = Join-Path $PSScriptRoot '..\src\Winhance.Core\Features\SoftwareApps\Catalogs'
$apps = Get-ChildItem $catalogs -Filter 'ExternalAppDefinitions.*.cs' |
    Select-String -Pattern 'WinGetPackageId = \[(.*?)\]' -AllMatches |
    ForEach-Object { $_.Matches } |
    ForEach-Object { , @([regex]::Matches($_.Groups[1].Value, '"([^"]+)"') | ForEach-Object { $_.Groups[1].Value }) }

$gone = @{}
foreach ($id in ($apps | ForEach-Object { $_ } | Sort-Object -Unique)) {
    $out = winget search --id $id --source winget --accept-source-agreements --disable-interactivity 2>$null | Out-String
    if ($out -notmatch ('(?im)(?<=\s)' + [regex]::Escape($id) + '(?=\s)')) { $gone[$id] = $true }
}

$dead = @($apps | Where-Object { @($_ | Where-Object { -not $gone[$_] }).Count -eq 0 } | ForEach-Object { $_ -join ', ' })
$spare = @($gone.Keys | Where-Object { $id = $_; $apps | Where-Object { $_ -contains $id -and @($_ | Where-Object { -not $gone[$_] }).Count -gt 0 } } | Sort-Object)

"Checked $($apps.Count) apps."
if ($spare) { "Gone, but the app has another live id (kept for detecting older installs):"; $spare | ForEach-Object { "  $_" } }
if ($dead) { "No live winget id left:"; $dead | ForEach-Object { "  $_" }; exit 1 }
"Every app has a live winget id."

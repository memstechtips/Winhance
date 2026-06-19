# run-catalog-harness.ps1
# Runs the new-catalog equivalence harness test(s) and writes the full output to a log file
# the agent can read (the repo is a shared drive, so extras\catalog-harness-results.txt here
# is the same file the agent sees at /srv/projects/winhance/extras/catalog-harness-results.txt).
#
# The project stays on Z:\ - this uses an EXPLICIT project path so testhost launches correctly.
# (A solution-wide `dotnet test --filter ...` fans out to every test project and aborts launching
# testhost.exe from the share; pointing at one project's .csproj avoids that, same as
# run-winhance-tests.ps1.)
#
# USAGE:
#   .\run-catalog-harness.ps1                              # run all *Equivalence* harness tests
#   .\run-catalog-harness.ps1 -Filter RegistryToggleEquivalence
param (
    [string]$Filter = "Equivalence"
)

$ErrorActionPreference = "Continue"
$solutionDir = Resolve-Path "$PSScriptRoot\.."
$proj    = Join-Path $solutionDir "tests\Winhance.Infrastructure.Tests\Winhance.Infrastructure.Tests.csproj"
$logFile = Join-Path $PSScriptRoot "catalog-harness-results.txt"
# Build the test output to a LOCAL dir so testhost.exe launches from C:\ - the Z:\ share blocks
# launching executables (testhost.exe -> "Access is denied"). Source stays on Z:\; only build output is local.
$outDir  = Join-Path $env:TEMP "winhance-catalog-harness"

Write-Host ""
Write-Host ("=" * 60) -ForegroundColor Cyan
Write-Host "  Catalog Equivalence Harness" -ForegroundColor Cyan
Write-Host "  filter : $Filter" -ForegroundColor Cyan
Write-Host "  log    : $logFile" -ForegroundColor Cyan
Write-Host ("=" * 60) -ForegroundColor Cyan

# Fresh log each run, with a header line.
"# Catalog equivalence harness run: $(Get-Date -Format o)  (filter: $Filter)" |
    Out-File -FilePath $logFile -Encoding utf8

# Run the harness; stream output live (Tee -Variable) AND capture it, then append as UTF-8.
# (Tee-Object -FilePath writes UTF-16 on PowerShell 5.1, which is unreadable to the agent; capturing
# and writing via Out-File -Encoding utf8 keeps the log plain UTF-8. --verbosity minimal cuts build noise
# while still showing build errors and the test [MATCH]/[DIFF] lines.)
dotnet test "$proj" --filter $Filter --output "$outDir" --verbosity minimal --logger "console;verbosity=detailed" 2>&1 |
    Tee-Object -Variable harnessLines

$code = $LASTEXITCODE
$harnessLines | Out-File -FilePath $logFile -Append -Encoding utf8

Write-Host ""
if ($code -eq 0) {
    Write-Host "  Result: all harness assertions passed (new == old)." -ForegroundColor Green
}
else {
    Write-Host "  Result: harness reported differences or an error - see the [DIFF] lines in the log." -ForegroundColor Yellow
}
Write-Host "  Full output written to: $logFile" -ForegroundColor Green
Write-Host "  The agent reads it at /srv/projects/winhance/extras/catalog-harness-results.txt" -ForegroundColor DarkGray
Write-Host ""

exit $code

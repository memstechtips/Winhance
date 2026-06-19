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

Write-Host ""
Write-Host ("=" * 60) -ForegroundColor Cyan
Write-Host "  Catalog Equivalence Harness" -ForegroundColor Cyan
Write-Host "  filter : $Filter" -ForegroundColor Cyan
Write-Host "  log    : $logFile" -ForegroundColor Cyan
Write-Host ("=" * 60) -ForegroundColor Cyan

# Fresh log each run, with a header line.
"# Catalog equivalence harness run: $(Get-Date -Format o)  (filter: $Filter)" |
    Out-File -FilePath $logFile -Encoding utf8

# Run the harness; show output live AND append it to the log.
dotnet test "$proj" --filter $Filter --logger "console;verbosity=detailed" 2>&1 |
    Tee-Object -FilePath $logFile -Append

$code = $LASTEXITCODE

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

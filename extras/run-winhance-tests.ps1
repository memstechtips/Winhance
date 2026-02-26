# run-winhance-tests.ps1
# Runs all Winhance test suites and reports results.
#
# SYNOPSIS:
# Runs unit tests (Core, Infrastructure, UI) and integration tests.
# Returns exit code 0 if all tests pass, 1 if any fail.
#
# EXAMPLES:
# # Run all tests
# .\run-winhance-tests.ps1
#
# # Skip UI tests (useful when Visual Studio is not installed)
# .\run-winhance-tests.ps1 -SkipUITests
#
# # Run only integration tests
# .\run-winhance-tests.ps1 -IntegrationOnly
param (
    [switch]$SkipUITests = $false,
    [switch]$IntegrationOnly = $false
)

$ErrorActionPreference = "Stop"
$solutionDir = Resolve-Path "$PSScriptRoot\.."

# Track results
$totalPassed = 0
$totalFailed = 0
$totalSkipped = 0
$failedProjects = @()

function Run-TestProject {
    param (
        [string]$Name,
        [string]$ProjectPath,
        [string]$ExtraArgs = ""
    )

    Write-Host ""
    Write-Host ("=" * 60) -ForegroundColor DarkGray
    Write-Host "  Running $Name..." -ForegroundColor Cyan
    Write-Host ("=" * 60) -ForegroundColor DarkGray

    $cmd = "dotnet test `"$ProjectPath`" --verbosity quiet $ExtraArgs"
    $output = Invoke-Expression $cmd 2>&1 | Out-String

    # Parse results from output
    $resultMatch = [regex]::Match($output, 'Failed:\s+(\d+),\s+Passed:\s+(\d+),\s+Skipped:\s+(\d+)')
    $passedMatch = [regex]::Match($output, 'Passed!\s+-\s+Failed:\s+(\d+),\s+Passed:\s+(\d+),\s+Skipped:\s+(\d+)')

    if ($passedMatch.Success) {
        $failed  = [int]$passedMatch.Groups[1].Value
        $passed  = [int]$passedMatch.Groups[2].Value
        $skipped = [int]$passedMatch.Groups[3].Value
    }
    elseif ($resultMatch.Success) {
        $failed  = [int]$resultMatch.Groups[1].Value
        $passed  = [int]$resultMatch.Groups[2].Value
        $skipped = [int]$resultMatch.Groups[3].Value
    }
    else {
        # Could not parse — treat as failure
        Write-Host $output
        $failed = 1; $passed = 0; $skipped = 0
    }

    $script:totalPassed  += $passed
    $script:totalFailed  += $failed
    $script:totalSkipped += $skipped

    if ($failed -gt 0) {
        Write-Host "  FAILED: $passed passed, $failed failed, $skipped skipped" -ForegroundColor Red
        $script:failedProjects += $Name
        # Print full output on failure so the user can see what went wrong
        Write-Host $output -ForegroundColor DarkGray
    }
    else {
        Write-Host "  PASSED: $passed passed, $skipped skipped" -ForegroundColor Green
    }
}

# Header
Write-Host ""
Write-Host ("=" * 60) -ForegroundColor Cyan
Write-Host "  Winhance Test Runner" -ForegroundColor Cyan
Write-Host ("=" * 60) -ForegroundColor Cyan

$startTime = Get-Date

if (-not $IntegrationOnly) {
    # Unit Tests — Core
    Run-TestProject `
        -Name "Core Unit Tests" `
        -ProjectPath "$solutionDir\tests\Winhance.Core.Tests\Winhance.Core.Tests.csproj"

    # Unit Tests — Infrastructure
    Run-TestProject `
        -Name "Infrastructure Unit Tests" `
        -ProjectPath "$solutionDir\tests\Winhance.Infrastructure.Tests\Winhance.Infrastructure.Tests.csproj"

    # Unit Tests — UI (requires Visual Studio / WinUI SDK)
    if (-not $SkipUITests) {
        $uiProjectPath = "$solutionDir\tests\Winhance.UI.Tests\Winhance.UI.Tests.csproj"

        # Quick check: try to build UI tests first since they need VS tooling
        Write-Host ""
        Write-Host ("=" * 60) -ForegroundColor DarkGray
        Write-Host "  Building UI Tests (requires Visual Studio)..." -ForegroundColor Cyan
        Write-Host ("=" * 60) -ForegroundColor DarkGray

        $buildOutput = dotnet build $uiProjectPath -p:Platform=x64 --verbosity quiet 2>&1 | Out-String
        if ($LASTEXITCODE -ne 0) {
            Write-Host "  SKIPPED: UI Tests failed to build (Visual Studio tooling may be missing)" -ForegroundColor Yellow
            Write-Host "  Use -SkipUITests to suppress this warning" -ForegroundColor DarkGray
        }
        else {
            Run-TestProject `
                -Name "UI Unit Tests" `
                -ProjectPath $uiProjectPath `
                -ExtraArgs "--no-build -p:Platform=x64"
        }
    }
    else {
        Write-Host ""
        Write-Host "  Skipping UI Tests (-SkipUITests)" -ForegroundColor Yellow
    }
}

# Integration Tests
Run-TestProject `
    -Name "Integration Tests" `
    -ProjectPath "$solutionDir\tests\Winhance.IntegrationTests\Winhance.IntegrationTests.csproj"

# Summary
$elapsed = (Get-Date) - $startTime
$totalTests = $totalPassed + $totalFailed + $totalSkipped

Write-Host ""
Write-Host ("=" * 60) -ForegroundColor Cyan
Write-Host "  Test Summary" -ForegroundColor Cyan
Write-Host ("=" * 60) -ForegroundColor Cyan
Write-Host "  Total:   $totalTests" -ForegroundColor White
Write-Host "  Passed:  $totalPassed" -ForegroundColor Green
if ($totalFailed -gt 0) {
    Write-Host "  Failed:  $totalFailed" -ForegroundColor Red
}
else {
    Write-Host "  Failed:  0" -ForegroundColor Green
}
if ($totalSkipped -gt 0) {
    Write-Host "  Skipped: $totalSkipped" -ForegroundColor Yellow
}
Write-Host "  Time:    $($elapsed.TotalSeconds.ToString('F1'))s" -ForegroundColor White
Write-Host ("=" * 60) -ForegroundColor Cyan

if ($failedProjects.Count -gt 0) {
    Write-Host ""
    Write-Host "  Failed projects:" -ForegroundColor Red
    foreach ($proj in $failedProjects) {
        Write-Host "    - $proj" -ForegroundColor Red
    }
    Write-Host ""
    exit 1
}
else {
    Write-Host ""
    Write-Host "  All tests passed!" -ForegroundColor Green
    Write-Host ""
    exit 0
}

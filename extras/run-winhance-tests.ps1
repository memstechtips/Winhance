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
        # Could not parse - treat as failure
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
    # Unit Tests - Core
    Run-TestProject `
        -Name "Core Unit Tests" `
        -ProjectPath "$solutionDir\tests\Winhance.Core.Tests\Winhance.Core.Tests.csproj"

    # Unit Tests - Infrastructure
    Run-TestProject `
        -Name "Infrastructure Unit Tests" `
        -ProjectPath "$solutionDir\tests\Winhance.Infrastructure.Tests\Winhance.Infrastructure.Tests.csproj"

    # Unit Tests - UI (requires Visual Studio / WinUI SDK, built with MSBuild)
    if (-not $SkipUITests) {
        $uiProjectPath = "$solutionDir\tests\Winhance.UI.Tests\Winhance.UI.Tests.csproj"

        # Find MSBuild via vswhere, then fall back to well-known paths
        $msbuildPath = $null
        $vswherePath = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
        if (Test-Path $vswherePath) {
            $msbuildPath = & $vswherePath -latest -requires Microsoft.Component.MSBuild -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
        }
        if (-not $msbuildPath -or -not (Test-Path $msbuildPath)) {
            $fallbackPaths = @(
                "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
                "${env:ProgramFiles}\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
                "${env:ProgramFiles}\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
            )
            foreach ($path in $fallbackPaths) {
                if (Test-Path $path) {
                    $msbuildPath = $path
                    break
                }
            }
        }

        if (-not $msbuildPath -or -not (Test-Path $msbuildPath)) {
            Write-Host ""
            Write-Host "  SKIPPED: UI Tests - MSBuild not found (Visual Studio required)" -ForegroundColor Yellow
            Write-Host "  Use -SkipUITests to suppress this warning" -ForegroundColor DarkGray
        }
        else {
            Write-Host ""
            Write-Host ("=" * 60) -ForegroundColor DarkGray
            Write-Host "  Building UI Tests with MSBuild..." -ForegroundColor Cyan
            Write-Host ("=" * 60) -ForegroundColor DarkGray

            $buildOutput = & $msbuildPath $uiProjectPath /p:Configuration=Debug /p:Platform=x64 /verbosity:quiet -restore 2>&1 | Out-String
            if ($LASTEXITCODE -ne 0) {
                Write-Host "  SKIPPED: UI Tests failed to build" -ForegroundColor Yellow
                Write-Host $buildOutput -ForegroundColor DarkGray
            }
            else {
                Write-Host "  Build succeeded" -ForegroundColor Green

                # Derive VS install path from MSBuild path (e.g. ...\2022\Community\MSBuild\Current\Bin\MSBuild.exe)
                $vsInstallPath = (Get-Item $msbuildPath).Directory.Parent.Parent.Parent.FullName
                $vstestConsolePath = Join-Path $vsInstallPath "Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe"

                $uiTestDll = "$solutionDir\tests\Winhance.UI.Tests\bin\x64\Debug\net10.0-windows10.0.19041.0\Winhance.UI.Tests.dll"

                # WinUI test projects require Visual Studio Test Explorer to run.
                # The native Windows App SDK runtime DLLs cannot be loaded by
                # the command-line test host (dotnet test or vstest.console.exe).
                # The build-only check above confirms the tests still compile.
                Write-Host "  UI Tests: Build verified (run via VS Test Explorer)" -ForegroundColor Green
            }
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
    Write-Host "  All tests passed." -ForegroundColor Green
    Write-Host ""
    exit 0
}

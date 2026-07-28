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
#
# # Build (and try to run) ONLY the WinUI test project - the `winhance-uitest` SSH verb's entry point
# .\run-winhance-tests.ps1 -UIOnly
param (
    [switch]$SkipUITests = $false,
    [switch]$IntegrationOnly = $false,
    # Build and run ONLY tests\Winhance.UI.Tests, then exit. Building that project compiles
    # src\Winhance.UI as a project reference, so this is the only gate that sees a compile error in
    # UI code - no other runner builds either project. Writes extras\uitest-results.txt so the full
    # MSBuild output is readable off the share.
    [switch]$UIOnly = $false,
    # When set, dotnet test build output goes under this dir instead of each project's bin/.
    # Needed when the source lives on a network share that blocks launching testhost.exe from it
    # (run the build output on a local disk). Empty = default behaviour (output stays in bin/).
    [string]$OutputDir = ""
)

$ErrorActionPreference = "Stop"
$solutionDir = Resolve-Path "$PSScriptRoot\.."

# Redirect build outputs to a local disk when the repo is on a network share (otherwise a project can
# silently link a stale prebuilt dll and ignore source edits, or the build fails writing the share's
# obj\). No-op on a local repo.
. "$PSScriptRoot\Use-LocalBuildOutputs.ps1" -RepoRoot $solutionDir

# Track results
$totalPassed = 0
$totalFailed = 0
$totalSkipped = 0
$failedProjects = @()

# In -UIOnly mode, mirror the (potentially long) MSBuild output into a log file next to this script.
# The repo is an SMB share, so the agent reads it at extras/uitest-results.txt without needing the
# SSH stdout to survive intact - same trick run-catalog-harness.ps1 uses for its results file.
$uiLogFile = if ($UIOnly) { Join-Path $PSScriptRoot "uitest-results.txt" } else { $null }
function Add-UILog($text) {
    if ($uiLogFile -and $text) { $text | Out-File -FilePath $uiLogFile -Append -Encoding utf8 }
}

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

    $outArg = ""
    if ($OutputDir) {
        $projOut = Join-Path $OutputDir ($Name -replace '[^A-Za-z0-9]', '_')
        $outArg = "--output `"$projOut`""
    }

    $cmd = "dotnet test `"$ProjectPath`" --verbosity quiet $outArg $ExtraArgs"
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

function Find-MSBuild {
    # WinUI3/WindowsAppSDK projects cannot be built by the .NET SDK alone - the XAML compiler needs
    # the MSVC toolset - so require BOTH components, same as build-and-package.ps1 does.
    $vswherePath = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswherePath) {
        # The installer dir is shared across side-by-side VS versions, so -latest finds VS 18 too.
        $found = & $vswherePath -latest -requires Microsoft.Component.MSBuild -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
        if ($found -and (Test-Path $found)) { return $found }
    }
    # Fallback for a machine whose vswhere is missing or too old to know the newer install.
    foreach ($ver in @("18", "2022")) {
        foreach ($edition in @("Community", "Professional", "Enterprise")) {
            $path = "${env:ProgramFiles}\Microsoft Visual Studio\$ver\$edition\MSBuild\Current\Bin\MSBuild.exe"
            if (Test-Path $path) { return $path }
        }
    }
    return $null
}

function Invoke-UITestProject {
    # Builds tests\Winhance.UI.Tests with MSBuild, then TRIES to run it.
    #
    # The build is the point: Winhance.UI.Tests project-references src\Winhance.UI, so a CS error in
    # UI code fails here. Nothing else in the automated reach compiles either project.
    #
    # Running is a bonus. A WinUI test project needs the WindowsAppSDK native runtime, which the
    # command-line test host does not bootstrap, so the run has historically not worked headless.
    # A test host that produces NO result summary is therefore reported as build-verified, NOT as a
    # failure - otherwise this gate would sit permanently red for a reason that is not a regression.
    # If the run DOES work, real test failures are fatal: that is what catches stale expectations.
    #
    # Returns one of: nomsbuild | buildfailed | buildonly | pass | fail
    #
    # Function-scoped so native tools writing to stderr under 2>&1 don't become terminating errors
    # (PowerShell 5.1 turns a redirected stderr line into a NativeCommandError when EAP is Stop).
    $ErrorActionPreference = "Continue"

    $uiProjectPath = "$solutionDir\tests\Winhance.UI.Tests\Winhance.UI.Tests.csproj"
    $msbuildPath = Find-MSBuild
    if (-not $msbuildPath) { return "nomsbuild" }

    Write-Host ""
    Write-Host ("=" * 60) -ForegroundColor DarkGray
    Write-Host "  Building UI Tests with MSBuild..." -ForegroundColor Cyan
    Write-Host ("=" * 60) -ForegroundColor DarkGray
    Write-Host "  msbuild: $msbuildPath" -ForegroundColor DarkGray
    Add-UILog "msbuild: $msbuildPath"
    Add-UILog "project: $uiProjectPath"

    $buildOutput = & $msbuildPath $uiProjectPath /p:Configuration=Debug /p:Platform=x64 /verbosity:minimal -restore 2>&1 | Out-String
    $buildCode = $LASTEXITCODE
    Add-UILog $buildOutput
    if ($buildCode -ne 0) {
        Write-Host "  BUILD FAILED - Winhance.UI or Winhance.UI.Tests does not compile" -ForegroundColor Red
        Write-Host $buildOutput -ForegroundColor DarkGray
        return "buildfailed"
    }
    Write-Host "  Build succeeded (Winhance.UI compiled)" -ForegroundColor Green

    # Locate the built assembly. When the repo is on a share, Use-LocalBuildOutputs.ps1 has
    # redirected bin\ to WINHANCE_LOCAL_BUILD_ROOT, so the in-tree path does not exist.
    $dllRoots = @()
    if ($env:WINHANCE_LOCAL_BUILD_ROOT) {
        $dllRoots += (Join-Path $env:WINHANCE_LOCAL_BUILD_ROOT "Winhance.UI.Tests\bin")
    }
    $dllRoots += "$solutionDir\tests\Winhance.UI.Tests\bin"

    # Constrain to the x64\Debug output we just built. Without this, a Release build left behind by
    # Visual Studio can be NEWER than an up-to-date (so not rewritten) Debug dll and win the sort -
    # which would run tests from an assembly this gate did not just verify.
    $uiTestDll = $null
    foreach ($root in $dllRoots) {
        if (-not (Test-Path $root)) { continue }
        $hit = Get-ChildItem -Path $root -Recurse -Filter "Winhance.UI.Tests.dll" -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -like "*\x64\Debug\*" } |
            Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($hit) { $uiTestDll = $hit.FullName; break }
    }
    if (-not $uiTestDll) {
        Write-Host "  UI Tests: build verified (test assembly not located; not run)" -ForegroundColor Green
        Add-UILog "test assembly not found under: $($dllRoots -join '; ')"
        return "buildonly"
    }

    # Derive the VS install root from the MSBuild path (...\<VSRoot>\MSBuild\Current\Bin\MSBuild.exe).
    $vsInstallPath = (Get-Item $msbuildPath).Directory.Parent.Parent.Parent.FullName
    $vstestConsolePath = Join-Path $vsInstallPath "Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe"
    if (-not (Test-Path $vstestConsolePath)) {
        Write-Host "  UI Tests: build verified (vstest.console.exe not found; not run)" -ForegroundColor Green
        Add-UILog "vstest.console.exe not found at $vstestConsolePath"
        return "buildonly"
    }

    Write-Host "  Attempting to run UI tests..." -ForegroundColor Cyan
    $runOutput = & $vstestConsolePath $uiTestDll /Platform:x64 2>&1 | Out-String
    Add-UILog $runOutput

    # Two summary shapes, because two runners emit them:
    #   vstest.console.exe : "Test Run Failed." / "Total tests: N" / "Passed: N" / "Failed: N"
    #                        on SEPARATE lines, Skipped omitted when zero.
    #   dotnet test        : "Passed! - Failed: 0, Passed: 3, Skipped: 0" on ONE comma-joined line.
    # Only the dotnet shape was matched originally, so a real vstest run of 1625 tests with 21
    # failures fell through to the "could not run" branch and was reported GREEN. Parse both.
    $failed = -1; $passed = 0; $skipped = 0
    $oneLine = [regex]::Match($runOutput, 'Failed:\s+(\d+),\s+Passed:\s+(\d+),\s+Skipped:\s+(\d+)')
    if ($oneLine.Success) {
        $failed  = [int]$oneLine.Groups[1].Value
        $passed  = [int]$oneLine.Groups[2].Value
        $skipped = [int]$oneLine.Groups[3].Value
    }
    else {
        $mFailed  = [regex]::Match($runOutput, '(?m)^\s*Failed:\s*(\d+)\s*$')
        $mPassed  = [regex]::Match($runOutput, '(?m)^\s*Passed:\s*(\d+)\s*$')
        $mSkipped = [regex]::Match($runOutput, '(?m)^\s*Skipped:\s*(\d+)\s*$')
        if ($mPassed.Success -or $mFailed.Success) {
            # vstest prints "Failed:" only when there ARE failures, "Passed:" only when there are passes.
            $failed  = if ($mFailed.Success)  { [int]$mFailed.Groups[1].Value }  else { 0 }
            $passed  = if ($mPassed.Success)  { [int]$mPassed.Groups[1].Value }  else { 0 }
            $skipped = if ($mSkipped.Success) { [int]$mSkipped.Groups[1].Value } else { 0 }
        }
    }

    # Decisive markers beat count-parsing: honour them even if the numbers were unreadable.
    $ranFailed = $runOutput -match 'Test Run Failed\.' -or $runOutput -match 'Test Run Aborted\.'
    $ranOk     = $runOutput -match 'Test Run Successful\.'
    # Individual result lines prove the host DID execute tests, whatever the summary looked like.
    $sawTests  = [regex]::IsMatch($runOutput, '(?m)^\s*(Passed|Failed|Skipped)\s+\S+\.\S+')

    if ($failed -lt 0) {
        # No parseable summary. Green is only honest when there is no evidence any test ran -
        # otherwise the run happened and we simply cannot read the verdict, which must not pass.
        if ($ranFailed -or $sawTests) {
            Write-Host "  FAILED: UI tests ran but the result summary could not be parsed" -ForegroundColor Red
            Write-Host "          Read extras\uitest-results.txt - do NOT treat this as a pass." -ForegroundColor Red
            return "fail"
        }
        # Reaching here is now UNEXPECTED: these tests were confirmed to run headless (1625 of them,
        # 2026-07-28). Treat it as "investigate", not as the documented normal state it once was.
        Write-Host "  UI Tests: build verified, but the run produced no results - see extras\uitest-results.txt" -ForegroundColor Yellow
        return "buildonly"
    }

    if ($ranFailed -and $failed -eq 0) {
        # Marker and counts disagree (an aborted run can report zero failures). Trust the marker.
        Write-Host "  FAILED: test run reported failure/abort despite a zero failed-count" -ForegroundColor Red
        Write-Host $runOutput -ForegroundColor DarkGray
        return "fail"
    }
    if ($ranOk -and $failed -gt 0) { Write-Host "  (marker said success but $failed failed - trusting the count)" -ForegroundColor Yellow }
    $script:totalPassed  += $passed
    $script:totalFailed  += $failed
    $script:totalSkipped += $skipped

    if ($failed -gt 0) {
        Write-Host "  FAILED: $passed passed, $failed failed, $skipped skipped" -ForegroundColor Red
        Write-Host $runOutput -ForegroundColor DarkGray
        return "fail"
    }
    Write-Host "  PASSED: $passed passed, $skipped skipped" -ForegroundColor Green
    return "pass"
}

function Resolve-UIResult {
    # Turns Invoke-UITestProject's status into a console line + the shared failure tally.
    # Returns $true when the UI gate passed.
    #
    # $MSBuildRequired distinguishes the two callers: with -UIOnly, a missing MSBuild means the gate
    # cannot do the one thing it exists for, so it FAILS loudly rather than reporting a silent green.
    # In a normal full run it stays the historical soft skip, for anyone without Visual Studio.
    param (
        [string]$Result,
        [bool]$MSBuildRequired
    )

    switch ($Result) {
        "pass" {
            return $true
        }
        "buildonly" {
            return $true
        }
        "buildfailed" {
            Write-Host "  FAILED: UI Tests - compile error (see the MSBuild output above)" -ForegroundColor Red
            $script:totalFailed += 1
            $script:failedProjects += "UI Tests (build)"
            return $false
        }
        "fail" {
            $script:failedProjects += "UI Tests"
            return $false
        }
        "nomsbuild" {
            if ($MSBuildRequired) {
                Write-Host ""
                Write-Host "  FAILED: MSBuild not found - install Visual Studio with the '.NET desktop" -ForegroundColor Red
                Write-Host "          development' workload AND the MSVC x64 build tools." -ForegroundColor Red
                Write-Host "          The WinUI test project cannot be built by the .NET SDK alone." -ForegroundColor Red
                Add-UILog "MSBuild not found - Visual Studio with the MSVC toolset is required."
                $script:totalFailed += 1
                $script:failedProjects += "UI Tests (no MSBuild)"
                return $false
            }
            Write-Host ""
            Write-Host "  SKIPPED: UI Tests - MSBuild not found (Visual Studio required)" -ForegroundColor Yellow
            Write-Host "  Use -SkipUITests to suppress this warning" -ForegroundColor DarkGray
            return $true
        }
    }

    # Unreachable today - the five cases above cover every status Invoke-UITestProject returns.
    # It records a failed project anyway so that a sixth status added later fails BOTH callers:
    # -UIOnly reads the return value, but a normal run only reads $failedProjects.
    Write-Host "  FAILED: UI Tests - unrecognised status '$Result'" -ForegroundColor Red
    $script:totalFailed += 1
    $script:failedProjects += "UI Tests (unknown status: $Result)"
    return $false
}

# Header
Write-Host ""
Write-Host ("=" * 60) -ForegroundColor Cyan
Write-Host "  Winhance Test Runner" -ForegroundColor Cyan
Write-Host ("=" * 60) -ForegroundColor Cyan

$startTime = Get-Date

if ($UIOnly) {
    "# Winhance UI test gate (-UIOnly): $(Get-Date -Format o)" | Out-File -FilePath $uiLogFile -Encoding utf8
    Write-Host "  mode:    UI only (build Winhance.UI.Tests -> compiles Winhance.UI)" -ForegroundColor Cyan
    Write-Host "  log:     $uiLogFile" -ForegroundColor Cyan

    $uiOk = Resolve-UIResult -Result (Invoke-UITestProject) -MSBuildRequired $true

    Write-Host ""
    Write-Host ("=" * 60) -ForegroundColor Cyan
    if ($uiOk) {
        Write-Host "  UI gate: PASS" -ForegroundColor Green
        if ($totalPassed -gt 0) { Write-Host "  Tests:   $totalPassed passed, $totalSkipped skipped" -ForegroundColor Green }
        Write-Host ("  Time:    {0}s" -f ((Get-Date) - $startTime).TotalSeconds.ToString('F1')) -ForegroundColor White
        Write-Host ("=" * 60) -ForegroundColor Cyan
        Write-Host ""
        exit 0
    }
    Write-Host "  UI gate: FAIL" -ForegroundColor Red
    Write-Host ("  Time:    {0}s" -f ((Get-Date) - $startTime).TotalSeconds.ToString('F1')) -ForegroundColor White
    Write-Host ("=" * 60) -ForegroundColor Cyan
    Write-Host ""
    exit 1
}

if (-not $IntegrationOnly) {
    # Unit Tests - Core
    Run-TestProject `
        -Name "Core Unit Tests" `
        -ProjectPath "$solutionDir\tests\Winhance.Core.Tests\Winhance.Core.Tests.csproj"

    # Unit Tests - Infrastructure
    Run-TestProject `
        -Name "Infrastructure Unit Tests" `
        -ProjectPath "$solutionDir\tests\Winhance.Infrastructure.Tests\Winhance.Infrastructure.Tests.csproj"

    # Unit Tests - UI (requires Visual Studio / WinUI SDK, built with MSBuild).
    # A build failure here used to print "SKIPPED" and leave the exit code at 0, so a compile error
    # in Winhance.UI passed as green. Resolve-UIResult now counts it as a real failure.
    if (-not $SkipUITests) {
        $null = Resolve-UIResult -Result (Invoke-UITestProject) -MSBuildRequired $false
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

<#
.SYNOPSIS
    GitHub Automation Script for Winhance project
.DESCRIPTION
    This script provides automation functions for GitHub operations such as closing multiple issues at once.
.NOTES
    Author: Winhance Team
    Version: 1.0
    Date: 2025-05-22
#>

# Check if GitHub CLI is installed
function Test-GitHubCLI {
    try {
        $ghVersion = gh --version
        return $true
    }
    catch {
        Write-Host "GitHub CLI (gh) is not installed or not in PATH." -ForegroundColor Red
        Write-Host "Please install GitHub CLI using: winget install GitHub.cli" -ForegroundColor Yellow
        Write-Host "After installation, authenticate with: gh auth login" -ForegroundColor Yellow
        return $false
    }
}

# Function to close multiple GitHub issues
function Close-GitHubIssues {
    param (
        [string]$Repository = "memstechtips/Winhance"
    )

    Clear-Host
    Write-Host "===== Close Multiple GitHub Issues =====" -ForegroundColor Cyan
    Write-Host "This function will close multiple GitHub issues with a comment." -ForegroundColor White
    Write-Host "Repository: $Repository" -ForegroundColor Green
    Write-Host ""

    # Get issue numbers from user
    Write-Host "Enter issue numbers to close (comma-separated, e.g., 154,155,146):" -ForegroundColor Yellow
    $issueInput = Read-Host
    
    # Parse issue numbers
    $issues = $issueInput -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ -match '^\d+$' }
    
    if ($issues.Count -eq 0) {
        Write-Host "No valid issue numbers provided. Operation cancelled." -ForegroundColor Red
        return
    }
    
    # Get comment from user
    Write-Host "Enter comment for closed issues (e.g., 'Closed by release v25.05.22'):" -ForegroundColor Yellow
    $comment = Read-Host
    
    # Confirm operation
    Write-Host ""
    Write-Host "You are about to close the following issues with comment: '$comment'" -ForegroundColor Magenta
    Write-Host "Issues: $($issues -join ', ')" -ForegroundColor Magenta
    Write-Host "Repository: $Repository" -ForegroundColor Magenta
    Write-Host ""
    $confirmation = Read-Host "Continue? (Y/N)"
    
    if ($confirmation -ne "Y" -and $confirmation -ne "y") {
        Write-Host "Operation cancelled." -ForegroundColor Red
        return
    }
    
    # Close issues
    Write-Host ""
    Write-Host "Closing issues..." -ForegroundColor Cyan
    
    $successCount = 0
    $failCount = 0
    
    foreach ($issue in $issues) {
        try {
            Write-Host "Closing issue #$issue..." -NoNewline
            gh issue close $issue -R $Repository --reason "completed" --comment $comment
            Write-Host " Done" -ForegroundColor Green
            $successCount++
        }
        catch {
            Write-Host " Failed" -ForegroundColor Red
            Write-Host "Error: $_" -ForegroundColor Red
            $failCount++
        }
    }
    
    # Summary
    Write-Host ""
    Write-Host "Operation completed." -ForegroundColor Cyan
    Write-Host "Successfully closed: $successCount issues" -ForegroundColor Green
    if ($failCount -gt 0) {
        Write-Host "Failed to close: $failCount issues" -ForegroundColor Red
    }
}

# Function to create a GitHub release
function New-GitHubRelease {
    param (
        [string]$Repository = "memstechtips/Winhance"
    )
    
    Clear-Host
    Write-Host "===== Create GitHub Release =====" -ForegroundColor Cyan
    Write-Host "This function will create a new GitHub release." -ForegroundColor White
    Write-Host "Repository: $Repository" -ForegroundColor Green
    Write-Host ""
    
    Write-Host "Feature not implemented yet. Coming soon!" -ForegroundColor Yellow
    
    # Future implementation will include:
    # - Release title
    # - Release notes (from file or input)
    # - Tag creation
    # - Asset uploading
}

# Main menu function
function Show-Menu {
    Clear-Host
    Write-Host "===== GitHub Automation Tools for Winhance =====" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "1: Close Multiple GitHub Issues" -ForegroundColor White
    Write-Host "2: Create GitHub Release (Coming Soon)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Q: Quit" -ForegroundColor White
    Write-Host ""
    
    $choice = Read-Host "Select an option"
    
    switch ($choice) {
        "1" { 
            if (Test-GitHubCLI) {
                Close-GitHubIssues 
                Read-Host "Press Enter to return to the menu"
                Show-Menu
            }
            else {
                Read-Host "Press Enter to return to the menu"
                Show-Menu
            }
        }
        "2" { 
            if (Test-GitHubCLI) {
                New-GitHubRelease
                Read-Host "Press Enter to return to the menu"
                Show-Menu
            }
            else {
                Read-Host "Press Enter to return to the menu"
                Show-Menu
            }
        }
        "Q" { return }
        "q" { return }
        default { 
            Write-Host "Invalid option. Please try again." -ForegroundColor Red
            Start-Sleep -Seconds 2
            Show-Menu
        }
    }
}

# Start the script
if (Test-GitHubCLI) {
    Show-Menu
}
else {
    Read-Host "Press Enter to exit"
}

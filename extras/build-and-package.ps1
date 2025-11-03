# build-and-package.ps1
# Script to automate the build and installer creation process for Winhance
#
# SYNOPSIS:
# This script builds the Winhance application and creates an installer.
# It also supports code signing using certificates from the Windows certificate store.
#
# EXAMPLES:
# # Basic usage (will prompt for signing)
# .\build-and-package.ps1
#
# # Automatically sign with interactive certificate selection
# .\build-and-package.ps1 -SignApplication
#
# # Sign with a specific certificate (if you know the thumbprint)
# .\build-and-package.ps1 -SignApplication -CertificateThumbprint "your-certificate-thumbprint"
#
# # Sign with a certificate matching a subject name
# .\build-and-package.ps1 -SignApplication -CertificateSubject "Your Company Name"
#
# # Create a beta version
# .\build-and-package.ps1 -Beta
param (
    [string]$Version = (Get-Date -Format "yy.MM.dd"),
    [string]$OutputDir = "$PSScriptRoot\..\installer-output",
    [string]$CertificateSubject = "",
    [string]$CertificateThumbprint = "",
    [switch]$SignApplication = $false,
    [switch]$Beta = $false
)

$ErrorActionPreference = "Stop"
$scriptRoot = $PSScriptRoot
$solutionDir = Resolve-Path "$scriptRoot\.."
$projectPath = "$solutionDir\src\Winhance.WPF\Winhance.WPF.csproj"

# Function to find and select a code signing certificate
function Get-SigningCertificate {
    param (
        [string]$Subject,
        [string]$Thumbprint
    )

    # If thumbprint is provided, use it directly
    if ($Thumbprint) {
        $cert = Get-ChildItem -Path Cert:\CurrentUser\My | Where-Object { $_.Thumbprint -eq $Thumbprint }
        if ($cert) {
            return $cert
        }
        else {
            Write-Host "Certificate with thumbprint '$Thumbprint' not found." -ForegroundColor Red
        }
    }

    # If subject is provided, try to find matching certificates
    if ($Subject) {
        $certs = Get-ChildItem -Path Cert:\CurrentUser\My | Where-Object { $_.Subject -like "*$Subject*" }
        if ($certs -and $certs.Count -gt 0) {
            if ($certs.Count -eq 1) {
                return $certs[0]
            }
            else {
                Write-Host "Multiple certificates found with subject '$Subject'. Please select one:" -ForegroundColor Yellow
                for ($i = 0; $i -lt $certs.Count; $i++) {
                    Write-Host "[$i] $($certs[$i].Subject) (Thumbprint: $($certs[$i].Thumbprint))" -ForegroundColor Cyan
                }
                $selection = Read-Host "Enter the number of the certificate to use"
                if ($selection -match '^\d+$' -and [int]$selection -ge 0 -and [int]$selection -lt $certs.Count) {
                    return $certs[[int]$selection]
                }
                else {
                    Write-Host "Invalid selection." -ForegroundColor Red
                    return $null
                }
            }
        }
        else {
            Write-Host "No certificates found with subject '$Subject'." -ForegroundColor Red
        }
    }

    # If no certificate found yet, list all certificates and let user select
    $certs = Get-ChildItem -Path Cert:\CurrentUser\My | Where-Object { $_.HasPrivateKey -and ($_.EnhancedKeyUsageList -and $_.EnhancedKeyUsageList.ObjectId -contains "1.3.6.1.5.5.7.3.3") }
    
    if ($certs -and $certs.Count -gt 0) {
        Write-Host "Available code signing certificates:" -ForegroundColor Green
        for ($i = 0; $i -lt $certs.Count; $i++) {
            Write-Host "[$i] $($certs[$i].Subject) (Thumbprint: $($certs[$i].Thumbprint))" -ForegroundColor Cyan
        }
        $selection = Read-Host "Enter the number of the certificate to use (or press Enter to skip signing)"
        if ($selection -match '^\d+$' -and [int]$selection -ge 0 -and [int]$selection -lt $certs.Count) {
            return $certs[[int]$selection]
        }
        elseif ($selection -eq "") {
            Write-Host "Signing skipped." -ForegroundColor Yellow
            return $null
        }
        else {
            Write-Host "Invalid selection. Signing skipped." -ForegroundColor Red
            return $null
        }
    }
    else {
        Write-Host "No code signing certificates found in your certificate store." -ForegroundColor Red
        return $null
    }
}

# Function to sign a file using signtool
function Set-FileSignature {
    param (
        [string]$FilePath,
        [System.Security.Cryptography.X509Certificates.X509Certificate2]$Certificate,
        [string]$TimestampServer = "http://timestamp.digicert.com"
    )

    if (-not $Certificate) {
        Write-Host "No certificate provided for signing." -ForegroundColor Yellow
        return $false
    }

    if (-not (Test-Path $FilePath)) {
        Write-Host ("File not found: {0}" -f $FilePath) -ForegroundColor Red
        return $false
    }

    $signtoolPath = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe"
    if (-not (Test-Path $signtoolPath)) {
        # Try to find signtool in other Windows Kit directories
        $possiblePaths = Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin\*\x64\signtool.exe" -ErrorAction SilentlyContinue
        if ($possiblePaths.Count -gt 0) {
            $signtoolPath = $possiblePaths[0].FullName
        }
        else {
            Write-Host "signtool.exe not found. Please ensure Windows SDK is installed." -ForegroundColor Red
            return $false
        }
    }

    Write-Host ("Signing {0}..." -f $FilePath) -ForegroundColor Cyan
    $thumbprint = $Certificate.Thumbprint
    
    # Sign the file using the certificate from the store
    $signCommand = "& '$signtoolPath' sign /tr '$TimestampServer' /td sha256 /fd sha256 /sha1 $thumbprint '$FilePath'"
    
    try {
        $result = Invoke-Expression $signCommand
        if ($LASTEXITCODE -eq 0) {
            Write-Host ("Successfully signed {0}" -f $FilePath) -ForegroundColor Green
            return $true
        }
        else {
            Write-Host ("Failed to sign {0}. Error code: {1}" -f $FilePath, $LASTEXITCODE) -ForegroundColor Red
            Write-Host $result -ForegroundColor Red
            return $false
        }
    }
    catch {
        Write-Host ("Error signing {0}: {1}" -f $FilePath, $_.Exception.Message) -ForegroundColor Red
        return $false
    }
}

$publishOutputPath = "$solutionDir\src\Winhance.WPF\bin\Release\net9.0-windows"
$innoSetupScript = "$scriptRoot\Winhance.Installer.iss"
$dotNetRuntimePath = "$scriptRoot\prerequisites\windowsdesktop-runtime-9.0.10-win-x64.exe"
$tempInnoScript = "$env:TEMP\Winhance.Installer.temp.iss"

# Declare certificate variable at script scope so it's accessible throughout
$certificate = $null
$shouldSign = $false

# Ensure output directory exists
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

Write-Host "Building Winhance v$Version..." -ForegroundColor Cyan

# Modify version if Beta flag is set
if ($Beta) {
    # For NuGet compatibility, use proper SemVer format with prerelease tag
    $displayVersion = "$Version-beta"
    $nugetVersion = "$Version-beta"
    Write-Host "Building beta version: v$displayVersion" -ForegroundColor Cyan
}
else {
    $displayVersion = $Version
    $nugetVersion = $Version
}

# Update version in csproj file
Write-Host "Updating version in project file..." -ForegroundColor Green
$csprojPath = "$solutionDir\src\Winhance.WPF\Winhance.WPF.csproj"
$csprojContent = Get-Content -Path $csprojPath -Raw

# Update version properties in csproj
# AssemblyVersion and FileVersion must be numeric only (no -beta suffix)
$csprojContent = $csprojContent -replace '<Version>.*?</Version>', "<Version>$nugetVersion</Version>"
$csprojContent = $csprojContent -replace '<FileVersion>.*?</FileVersion>', "<FileVersion>$Version</FileVersion>"
$csprojContent = $csprojContent -replace '<AssemblyVersion>.*?</AssemblyVersion>', "<AssemblyVersion>$Version</AssemblyVersion>"
$csprojContent = $csprojContent -replace '<InformationalVersion>.*?</InformationalVersion>', "<InformationalVersion>v$displayVersion</InformationalVersion>"

# Write updated csproj content
Set-Content -Path $csprojPath -Value $csprojContent

# Step 1: Clean the solution
Write-Host "Cleaning solution..." -ForegroundColor Green
dotnet clean "$projectPath" --configuration Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to clean solution" -ForegroundColor Red
    exit 1
}

# Step 2: Build the solution
Write-Host "Building solution..." -ForegroundColor Green
dotnet build "$projectPath" --configuration Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to build solution" -ForegroundColor Red
    exit 1
}

# Step 3: Publish the application
Write-Host "Publishing application..." -ForegroundColor Green
dotnet publish "$projectPath" --configuration Release --runtime win-x64 --self-contained false -p:PublishSingleFile=false -p:PublishReadyToRun=true
if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to publish application" -ForegroundColor Red
    exit 1
}

# Step 3.5: Sign the application executable
$mainExecutable = "$publishOutputPath\Winhance.exe"

# Check if signing is requested
if ($SignApplication -or (Read-Host "Do you want to sign the application? (y/n)").ToLower() -eq 'y') {
    $certificate = Get-SigningCertificate -Subject $CertificateSubject -Thumbprint $CertificateThumbprint

    if ($certificate) {
        Write-Host "Selected certificate: $($certificate.Subject)" -ForegroundColor Green
        Write-Host "Thumbprint: $($certificate.Thumbprint)" -ForegroundColor Green

        # Sign the main executable
        $signResult = Set-FileSignature -FilePath $mainExecutable -Certificate $certificate

        if ($signResult) {
            $shouldSign = $true
            Write-Host "Application executable signed successfully." -ForegroundColor Green
        }
        else {
            Write-Host "Warning: Failed to sign the application. Continuing with unsigned application..." -ForegroundColor Yellow
        }
    }
    else {
        Write-Host "No certificate selected. Continuing with unsigned application..." -ForegroundColor Yellow
    }
}
else {
    Write-Host "Skipping application signing..." -ForegroundColor Yellow
}

# Step 4: Update the InnoSetup script with correct paths
Write-Host "Preparing InnoSetup script..." -ForegroundColor Green
$innoContent = Get-Content -Path $innoSetupScript -Raw

# Update version
$innoContent = $innoContent -replace '#define MyAppVersion ".*"', "#define MyAppVersion `"$displayVersion`""
# Update AppVerName to include version in the installer header
$innoContent = $innoContent -replace 'AppVerName=Winhance', "AppVerName=Winhance v$displayVersion"

# Update paths
$publishPath = $publishOutputPath.Replace("\", "\\")
$outputPath = $OutputDir.Replace("\", "\\")
$licensePath = "$solutionDir\LICENSE.txt".Replace("\", "\\")
$iconPath = "$solutionDir\src\Winhance.WPF\Resources\AppIcons\winhance-rocket.ico".Replace("\", "\\")

$innoContent = $innoContent -replace 'LicenseFile=C:\\Winhance\\LICENSE.txt', "LicenseFile=$licensePath"
$innoContent = $innoContent -replace 'OutputDir=C:\\Winhance\\installer-output', "OutputDir=$outputPath"
$innoContent = $innoContent -replace 'SetupIconFile=C:\\Winhance\\src\\Winhance.WPF\\Resources\\AppIcons\\winhance-rocket.ico', "SetupIconFile=$iconPath"
$innoContent = $innoContent -replace 'Source: "C:\\Winhance\\src\\Winhance.WPF\\bin\\Release\\net9.0-windows\\win-x64\\', "Source: `"$publishPath\\"
$innoContent = $innoContent -replace 'Source: "C:\\Winhance\\extras\\prerequisites\\', "Source: `"$scriptRoot\\prerequisites\\"

# Write the updated script to a temporary file
Set-Content -Path $tempInnoScript -Value $innoContent

# Step 5: Run the InnoSetup compiler
Write-Host "Creating installer..." -ForegroundColor Green
$innoCompiler = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $innoCompiler)) {
    Write-Host "InnoSetup compiler not found at $innoCompiler" -ForegroundColor Yellow
    $innoCompiler = "C:\Program Files\Inno Setup 6\ISCC.exe"
    if (-not (Test-Path $innoCompiler)) {
        Write-Host "InnoSetup compiler not found. Please install Inno Setup 6 or update the script with the correct path." -ForegroundColor Red
        exit 1
    }
}

& $innoCompiler $tempInnoScript
if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to create installer" -ForegroundColor Red
    exit 1
}

# Clean up
Remove-Item $tempInnoScript -Force

# Sign the installer if the executable was signed
$installerPath = "$OutputDir\Winhance.Installer.exe"
if ($shouldSign -and $certificate -and (Test-Path $installerPath)) {
    Write-Host "Signing the installer..." -ForegroundColor Cyan
    $installerSignResult = Set-FileSignature -FilePath $installerPath -Certificate $certificate

    if ($installerSignResult) {
        Write-Host "Installer successfully signed." -ForegroundColor Green
    }
    else {
        Write-Host "Warning: Failed to sign the installer." -ForegroundColor Yellow
    }
}
elseif (-not $shouldSign) {
    Write-Host "Skipping installer signing (executable was not signed)." -ForegroundColor Yellow
}

Write-Host "Build and packaging completed successfully!" -ForegroundColor Cyan
Write-Host "Installer created at: $installerPath" -ForegroundColor Green

# Display signing status summary
if ($shouldSign) {
    Write-Host "`nSigning Summary:" -ForegroundColor Cyan
    Write-Host "  Certificate: $($certificate.Subject)" -ForegroundColor Green
    Write-Host "  Executable: Signed" -ForegroundColor Green
    if ($installerSignResult) {
        Write-Host "  Installer: Signed" -ForegroundColor Green
    }
    else {
        Write-Host "  Installer: Failed to sign" -ForegroundColor Red
    }
}
else {
    Write-Host "`nSigning Summary: No files were signed" -ForegroundColor Yellow
}
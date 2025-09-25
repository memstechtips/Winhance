# WARP.md

This file provides guidance to WARP (warp.dev) when working with code in this repository.

Project overview
- Winhance is a Windows desktop app (WPF, .NET 9) that optimizes and customizes Windows 10/11. The solution contains three projects:
  - Winhance.Core: domain models, interfaces, and cross-cutting abstractions
  - Winhance.Infrastructure: concrete implementations (registry, PowerShell, WinGet, OS services)
  - Winhance.WPF: UI (views, view models), DI composition, and app host

Prerequisites
- Windows 10/11 with .NET SDK 9 installed
- Optional for packaging: Inno Setup 6 and Windows 10/11 SDK (signtool) if signing

Common commands (PowerShell)
- Restore
  ```powershell path=null start=null
  dotnet restore Winhance.sln
  ```
- Build (solution)
  ```powershell path=null start=null
  dotnet build Winhance.sln -c Debug
  dotnet build Winhance.sln -c Release
  ```
- Run the app (WPF project)
  ```powershell path=null start=null
  dotnet run --project src/Winhance.WPF/Winhance.WPF.csproj -c Debug
  ```
- Publish (framework-dependent, as used by packaging script)
  ```powershell path=null start=null
  dotnet publish src/Winhance.WPF/Winhance.WPF.csproj \
    -c Release --runtime win-x64 --self-contained false \
    -p:PublishSingleFile=false -p:PublishReadyToRun=true
  ```
- Format/lint (C# analyzers run on build; use dotnet format for code style)
  ```powershell path=null start=null
  dotnet format
  ```
- Tests
  - No test projects are present in this repository today. If/when tests are added:
    - Run all tests: dotnet test
    - Run a single test by filter (example):
      ```powershell path=null start=null
      dotnet test --filter FullyQualifiedName~Namespace.TypeName.TestMethod
      ```

Build and package (installer)
- End-to-end build + installer creation
  - Script: extras/build-and-package.ps1
  - Produces: installer-output/Winhance.Installer.exe
  - Examples:
    ```powershell path=null start=null
    # Basic (prompts for signing during execution)
    ./extras/build-and-package.ps1

    # Mark as beta and auto-apply version from date (default pattern yy.MM.dd)
    ./extras/build-and-package.ps1 -Beta

    # Sign app and installer (requires cert in CurrentUser\My)
    ./extras/build-and-package.ps1 -SignApplication -CertificateSubject "Your Company"
    # or, by thumbprint
    ./extras/build-and-package.ps1 -SignApplication -CertificateThumbprint "<THUMBPRINT>"
    ```
- What the script does (high level)
  - Updates version fields in src/Winhance.WPF/Winhance.WPF.csproj
  - dotnet clean/build/publish for Winhance.WPF (net9.0-windows, win-x64)
  - Optionally signs Winhance.exe and the final installer via signtool
  - Writes a temp Inno Setup script from extras/Winhance.Installer.iss and compiles it with ISCC.exe

Release workflow
- GitHub Actions: .github/workflows/create-release.yml
  - Trigger: push tags matching v*
  - Creates a draft GitHub Release with simplified notes and enables auto-generated notes
  - If you plan to ship from CI, tag a commit like v25.05.28 (matching the versioning used by the packaging script)

High-level architecture and flow
- Composition and hosting
  - Entry-point DI wiring lives in WPF: Features/Common/Extensions/DI/CompositionRoot.cs
    - services.AddCoreServices() → interfaces/abstractions only
    - services.AddInfrastructureServices() → concrete implementations and system services
    - services.AddDomainServices(), AddConfigurationServices(), AddUIServices(), AddViewModels(), AddViews()
    - CreateWinhanceHost() builds a default Host with the above registrations
- Layers
  - Core (net9.0-windows): interfaces for logging, navigation, configuration, registry, PowerShell, search, progress, etc., plus domain events and models
  - Infrastructure (net9.0-windows): implementations for OS/Windows integration
    - Registry (WindowsRegistryService), PowerShell (PowerShellExecutionService), Scheduled Tasks, Windows version/UI/theme services
    - WinGet integration under Features/SoftwareApps/Services/WinGet/* (installer script, verification methods, output parsing)
    - EventBus implementation for domain/UI communication
  - WPF (net9.0-windows, UseWPF=true): Views, ViewModels (CommunityToolkit.Mvvm), converters, resources, navigation
    - DI extension methods to register infrastructure/core/ui services and view mappings (InfrastructureServicesExtensions.cs, CoreServicesExtensions.cs)
    - MainViewModel coordinates navigation, progress, dialogs, configuration import/export
- Feature slices (verticals)
  - Optimize: power plan management, privacy, updates, explorer, sound, security, notifications
  - Customize: theme, taskbar, start menu, explorer customizations
  - SoftwareApps: Windows Apps (capabilities/features) and External Apps via WinGet, with discovery, install/uninstall, and verification
- Cross-cutting patterns
  - Event-driven updates via IEventBus (Infrastructure.Features.Common.Events.EventBus)
  - Progress and logging surfaced to the UI (ITaskProgressService, ILogService)
  - Navigation orchestrated via a FrameNavigationService that maps routes to Views/ViewModels
  - Configuration import/export and a unified configuration dialog for applying groups of settings

Important notes from repository docs
- README highlights
  - Targets Windows 10/11; distributed installer includes both installable and portable modes
  - Public website and Releases host official binaries; security info provided per-version in README
- Claude rules (./.claude/settings.local.json)
  - Local agent permissions allow common .NET and shell commands (dotnet build/clean/restore, git, grep, etc.)
  - Default mode: acceptEdits. No repo-wide coding standards are enforced here beyond typical C# analyzers and formatting

Troubleshooting tips (build/run)
- Ensure .NET SDK 9 is installed (dotnet --version). The app targets net9.0-windows
- If packaging fails, verify Inno Setup 6 is installed and ISCC.exe is on the expected path
- Running with dotnet run launches a WinExe WPF application; no console output is expected unless explicitly written

# Winhance Development Guide

Winhance is a C# WPF application for debloating, optimizing, and customizing Windows 10/11. It uses Clean Architecture with a three-layer structure.

## Build and Run

**Build the solution:**
```bash
dotnet build Winhance.sln
```

**Run the application:**
```bash
dotnet run --project src/Winhance.WPF/Winhance.WPF.csproj
```

**Build release version:**
```bash
dotnet build Winhance.sln -c Release
```

**Create installer (requires PowerShell on Windows):**
```powershell
.\extras\build-and-package.ps1
```

The build script supports options like `-SignApplication`, `-Beta`, and custom `-Version`.

## Architecture

### Three-Layer Structure

```
src/
├── Winhance.Core/          # Domain layer - interfaces, models, enums
├── Winhance.Infrastructure/ # Infrastructure - service implementations
└── Winhance.WPF/           # Presentation - ViewModels, Views, UI services
```

**Winhance.Core** contains:
- Interfaces (e.g., `IConfigurationService`, `IPowerShellExecutionService`)
- Domain models and DTOs
- Enums and events
- NO implementations or dependencies on other layers

**Winhance.Infrastructure** contains:
- Concrete service implementations
- Windows API interactions
- PowerShell execution logic
- Registry and file system operations

**Winhance.WPF** contains:
- ViewModels (using CommunityToolkit.Mvvm)
- XAML views
- UI-specific services
- Dependency injection setup

### Feature Organization

Features are organized by domain area in each layer:
- `AdvancedTools/` - WIMUtil, autounattend.xml generation
- `Common/` - Shared services, models, utilities
- `Customize/` - Windows theme, taskbar, Start Menu, Explorer
- `Optimize/` - Privacy, gaming, power, updates, notifications
- `SoftwareApps/` - Windows apps, external apps (WinGet)
- `UI/` - Notification service, UI helpers (WPF layer only)

## Dependency Injection

Services are registered in `src/Winhance.WPF/Features/Common/Extensions/DI/CompositionRoot.cs`.

Registration flows in dependency order:
1. `AddCoreServices()` - Core abstractions
2. `AddInfrastructureServices()` - Infrastructure implementations  
3. `AddDomainServices()` - Domain services
4. `AddUIServices()` - UI layer services
5. `AddViewModels()` - ViewModels (Singleton for main features, Transient for dialogs)
6. `AddViews()` - View registrations

**When adding new services:**
- Define the interface in `Winhance.Core/Features/{Feature}/Interfaces/`
- Implement in `Winhance.Infrastructure/Features/{Feature}/Services/`
- Register in the appropriate `*ServicesExtensions.cs` file
- Inject via constructor in ViewModels or other services

## MVVM Pattern

Uses **CommunityToolkit.Mvvm** (not Prism or other frameworks).

**Base ViewModels:**
- `BaseViewModel` - All ViewModels inherit from this
- `BaseFeatureViewModel` - For feature ViewModels with common functionality
- `BaseSettingsFeatureViewModel` - For settings pages (Optimize, Customize)
- `ObservableObject` from CommunityToolkit provides INotifyPropertyChanged

**Commands:**
Use `[RelayCommand]` attribute from CommunityToolkit.Mvvm for commands:
```csharp
[RelayCommand]
private async Task DoSomethingAsync()
{
    // Implementation
}
```

This auto-generates `DoSomethingCommand` property.

## PowerShell Execution

Windows modifications are primarily done via PowerShell scripts executed through `IPowerShellExecutionService`.

**Pattern:**
```csharp
await _powerShellService.ExecuteScriptInNewWindowAsync(
    script: "Your-PowerShell-Script",
    windowTitle: "Descriptive Title",
    // Optional: progress reporter, cancellation token
);
```

Scripts execute in elevated PowerShell windows to ensure proper permissions.

## Localization

Localization files are JSON files in `src/Winhance.WPF/Localization/`.

**Format:** Each file (e.g., `en.json`, `de.json`) contains key-value pairs.

**To add a translation:**
1. Add key to all language files (use English as base)
2. AI translations (Gemini) are acceptable for initial versions
3. Native speakers can submit PRs for corrections

**Languages supported:** 23+ languages including English, German, French, Spanish, Japanese, Chinese (Simplified/Traditional), etc.

## Key Conventions

### Naming
- Services: `{Feature}Service` (e.g., `TaskbarService`, `PowerShellExecutionService`)
- ViewModels: `{Feature}ViewModel` (e.g., `OptimizeViewModel`, `WindowsAppsViewModel`)
- Interfaces: `I{Name}` (standard C# convention)

### Project References
- Core has NO dependencies on Infrastructure or WPF
- Infrastructure references Core only
- WPF references both Core and Infrastructure

### Nullable Reference Types
Enabled via `<Nullable>enable</Nullable>` in all projects. Use nullable annotations appropriately.

### Target Framework
.NET 9.0 Windows (`net9.0-windows`)

## Configuration Files

Winhance supports saving/loading configuration via `.winhance` files (JSON format).

**Location:** Users can save configurations to export settings and import on other systems.

**Recommended config:** Embedded resource at `src/Winhance.WPF/Resources/Configs/Winhance_Recommended_Config.winhance`

## Single Instance Application

Uses a named Mutex to prevent multiple instances:
```csharp
const string MutexName = "Winhance_SingleInstance_Mutex_{GUID}";
```

## Version Information

Version is set in `src/Winhance.WPF/Winhance.WPF.csproj`:
```xml
<Version>26.01.26</Version>
```
Format: `YY.MM.DD` (last two digits of year, month, day)

## Commit Message Format

When creating releases, the workflow categorizes commits by prefix:
- `feat:` - New features
- `fix:` - Bug fixes
- `refactor:` - Code refactoring
- `docs:` - Documentation changes
- `perf:` - Performance improvements
- `style:` - Code style changes
- `chore:` - Maintenance tasks

Use these prefixes for automatic release notes generation.

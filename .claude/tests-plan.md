# Winhance Test Plan

Date: 2026-02-20
Updated: 2026-02-25 (v8 remediation: 10 findings fixed — OperationCanceledException propagation in StoreDownloadService, null-forgiving removal in DirectDownloadService, ConPtyProcess handle leak guard, WinGetComSession volatile fields, ManagementObject disposal in 3 services, null guards in UpdateService/PowerService, unnecessary async removed from 7 domain services, CancellationToken propagation in AppInstallationService, CompatibleSettingsRegistry double-invocation eliminated; test plan corrections: InfrastructureServicesExtensions interface count 37→46, SrClientApi added to P/Invoke table (7 classes), WimUtilServiceTests + ScriptPreambleSectionTests added, >1000 lines claim corrected)

---

## Current State

The project has **zero tests**. No test projects, no test frameworks, no test infrastructure.

### Dead Code Cleanup (2026-02-21)

Before implementing tests, two rounds of dead code audits were performed:

**Round 1** — **36 files deleted**, **2 files edited** (~1,400+ lines removed):
- 7 dead interfaces (WPF-era navigation, abandoned UI management)
- 4 orphaned service pairs (interface + implementation, never registered in DI)
- 6 dead exception files (elaborate hierarchy never thrown/caught)
- 480-line `OutputParser` utility (old powercfg.exe text parsing, superseded by PowerProf.dll)
- Various dead models, events, and enums

**Round 2** — **13 files deleted**, **4 files edited** (~900 lines removed):
- 3 dead interface + implementation pairs registered in DI but never injected (`IScriptDetectionService`, `ISettingsRegistry`, `IDomainDependencyService`)
- 2 dead model types (`WinGetProgress` class, `LegacyCapability` record)
- 5 dead UI components (1 converter, 1 utility class, 3 behaviors)
- Removed 3 stale DI registrations, 1 stale XAML resource entry
- Fixed unreachable duplicate null check in `StoreDownloadService`
- Removed unused constructor parameter in `SystemBackupService`

All removed code was verified to have zero references across the entire solution. Both Core and Infrastructure build cleanly after each round.

**Round 3** — **2 files deleted**, **2 files edited** (~190 lines removed):
- 1 dead utility class: `ModelMapper` (interface + implementation, never registered/referenced)
- 1 dead UI helper: `VisualTreeHelpers` (4 visual tree methods, never called — codebase uses built-in `VisualTreeHelper` directly)
- Removed dead `LogService.Initialize(IWindowsVersionService)` method, dead `_versionService` field, and unreachable OS version logging branch in `StartLog()`
- Removed obsolete `RemovalScript.ScriptPath` property (all callers use `ActualScriptPath`)
- Verified: `ConfigurationItem` obsolete properties (`SelectedValue`, `CustomProperties`) intentionally kept for JSON deserialization backward compatibility

**Round 4** — **16 files deleted**, **~30 files edited** (~1,500 lines removed):
- 6 WinGet verification subsystem files (CompositeInstallationVerifier, 3 verification methods, 2 interfaces) — entire unused verification pipeline
- 3 dead model/event files (LogMessageEventArgs, PowerShellProgressData, TaskProgressEventArgs) — only consumers were dead interface methods
- 2 dead enum files (PowerShellStreamType, DialogType) — became fully orphaned after removing their callers
- 1 dead DI extension file (CoreServicesExtensions) — contained only dead registrations
- Removed ~24 dead methods from interfaces and implementations: `ILogService` (3), `IDialogService` (4), `ITaskProgressService` (3), `OperationResult` (8 factory methods + entire non-generic class), `ValidationHelper` (4), `DriverCategorizer` (1), `WinGetService` (1), `CompatibleSettingsRegistry` (1), `MainWindow.xaml.cs` (3), `NavSidebar.xaml.cs` (1), `DomainServicesExtensions` (1)
- Removed 6 dead properties/fields: `OperationResult<T>.IsCancelled`, `OperationResult<T>.ErrorDetails`, `ConfirmationRequest.Context`, `ConfirmationResponse.Context`, `CompatibleSettingsRegistry._rawSettings`, `StoreDownloadService.SupportedArchitectures`
- Removed dead enum values: `InstallStatus.Failed`, `InstallStatus.Pending`
- Removed 5 dead XAML glyph resources from FeatureIcons.xaml
- Cleaned ~250 lines of dead `StringKeys` constants (9 nested classes, 7 helper methods, 30 unused Localized properties)
- Trimmed ~15 dead interface methods across 6 services: `IInternetConnectivityService`, `IWindowsVersionService`, `IHardwareDetectionService`, `ISystemBackupService`, `IDomainServiceRouter`, `IExternalAppsService`
- Removed unused `ILogService` dependency from `DomainServiceRouter` constructor

**Round 5** — **0 files deleted**, **~15 files edited** (~600 lines removed):
- Removed 15 completely dead methods from 6 interfaces + implementations:
  - `IDialogService` (3): `ShowUnifiedConfigurationSaveDialogAsync`, `ShowUnifiedConfigurationImportDialogAsync`, `ShowOperationResult`
  - `ITaskProgressService` (1): `RequestSkipNext`
  - `IWinGetService` (2): `IsPackageInstalledAsync`, `EnsureWinGetUpToDateAsync`
  - `IWindowsAppsService` (3 remaining after R4): `RemoveAppxPackagesAsync`, `RemoveCapabilitiesBatchAsync`, `DisableOptionalFeaturesBatchAsync`
  - `IPowerService` (2): `GetSettingValueAsync`, `ApplyAdvancedPowerSettingAsync`
  - `IAppOperationService` (2): `UninstallExternalAppAsync`, `UninstallExternalAppsAsync`
- Internalized 4 interface methods that were only called internally (removed from interface, made `private`):
  - `IWindowsRegistryService.CreateKey` → `private` in `WindowsRegistryService`
  - `IPowerService.SetActivePowerPlanAsync` → `private` in `PowerService`
  - `ILocalizationService.GetAvailableLanguages` → `private` in `LocalizationService`
  - `ITaskProgressService.AddLogMessage` → `private` in `TaskProgressService`
- Removed dead enum value: `ImportOption.None` (nullable pattern used instead)

**Round 6** — **1 file deleted**, **3 files edited** (~45 lines removed):
- 3 dead event classes (published via `IEventBus.Publish()` but zero subscribers anywhere):
  - `AppInstalledEvent` — 4 publish calls removed from `AppOperationService`
  - `AppRemovedEvent` — 3 publish calls + 1 dead log line removed from `AppOperationService`
  - `TooltipsBulkLoadedEvent` — entire file deleted from Core, 1 publish call removed from `TooltipRefreshEventHandler`
- 2 dead XAML icon resources removed from `FeatureIcons.xaml`:
  - `AdvancedToolsIconGlyph` (replaced by Material Design path icon)
  - `BookOpenVariantOutlineIconPath` (unused "Docs" icon)
- Removed unused `IEventBus` constructor parameter from `AppOperationService`

**Round 7** — **0 files deleted**, **4 files edited** (~68 lines removed):
- Removed write-only `VersionInfo.DownloadUrl` property (assigned in `VersionService` but never read — download URL accessed directly via private field)
- Removed unused `FeatureIds.Security` constant (0 references via the constant; Security settings grouped under Privacy feature)
- Removed ~65 lines of commented-out brightness `SettingDefinition` blocks from `PowerOptimizations.cs` (display-brightness, display-dimmed-brightness, adaptive-brightness)

**Round 8** — **1 file deleted**, **~57 files edited** (~1,500 lines removed):
- Removed 20 dead interface methods across 11 interfaces: `IPowerSettingsQueryService` (3), `IPowerSettingsValidationService` (1), `IScheduledTaskService` (3), `IWallpaperService` (1), `IWindowsUIManagementService` (2), `IGlobalSettingsRegistry` (3), `IAppLoadingService` (4), `IStartupNotificationService` (1), `IConfigReviewService` (1), `IDialogService` (1 overload), `ITaskProgressService` (1 property)
- Deleted `InstallStatus` enum file (orphaned after removing its only consumers on `IAppLoadingService`)
- Removed entire `BackupStatus` class + 3 write-only properties from `BackupResult` (`RestorePointExisted`, `SystemRestoreEnabled`, `Warnings`) and all their writes in `SystemBackupService`
- Removed 2 dead enum values: `UninstallMethod.CustomScript`, `DetectionSource.None`
- Removed `OperationResult<T>.CreateFailure`, `OperationResult<T>.Failed()` (parameterless), `OperationResult<T>.ConfirmationRequest` property
- Removed dead properties: `TaskProgressDetail.AdditionalInfo`, `SettingStateResult.IsRegistryValueNotSet`, `PowerPlan.SourceGuid`, `SettingTooltipData.RegistrySetting`, `ImageDetectionResult.HasWimOnly`/`HasEsdOnly`
- Removed `FeatureDefinition.IconName` and `SortOrder` positional record parameters + updated 12 constructor calls
- Removed 5 dead constants from `CustomPropertyKeys` (`Id`, `GroupName`, `Description`, `SliderValue`, `SliderLabels`)
- Internalized 8 internal-only interface methods (removed from interface, made private): `IAppLoadingService.GetBatchInstallStatusAsync`, `IBloatRemovalService.CleanupBloatRemovalArtifactsAsync`, `IStoreDownloadService.DownloadPackageAsync`, `IApplicationCloseService` (3 methods), `IAppUninstallService.DetermineUninstallMethodAsync`, `IPowerSettingsQueryService.GetPowerSettingCapabilitiesAsync`
- Removed dead Infrastructure methods: `WinGetService.FindInstalledPackageAsync`, `PowerService.FindNewlyCreatedPlanGuidAsync`, `PowerService.RefreshCompatiblePowerSettingsAsync`, `AppLoadingService.GetItemInstallStatusAsync`
- Removed dead P/Invoke `SendMessage`, 4 unused `SPI_*` constants, and shadowed `HWND_BROADCAST`/`WM_SETTINGCHANGE` fields from `WindowsUIManagementService`
- Removed `FeatureVisibilityChangedEventArgs` class + `VisibilityChanged` event (raised but never subscribed to)
- Removed `HandleDomainContextSettingAsync` from `ISettingsFeatureViewModel` and `BaseSettingsFeatureViewModel`
- Removed `LocalizeSettingGroup` from `ISettingLocalizationService`
- Removed 3 dead commands + backing methods from `AppItemViewModel` (`InstallCommand`, `UninstallCommand`, `OpenWebsiteCommand`) + 5 dead properties (`PackageName`, `Version`, `IsInstalling`, `IsUninstalling`, `Status`) — simplified constructor from 6 to 3 params
- Removed dead `_disposalCancellationTokenSource` from `BaseViewModel`
- Removed unused `SettingsGroup(string key)` single-arg constructor
- Removed 2 dead alias properties from `SoftwareAppsViewModel` (`ReviewWindowsAppsBanner`, `ReviewExternalAppsBanner`)

Full analysis documented in `dead-code.md`. Codebase verified clean across all three layers via comprehensive method-level and property-level scan. Core and Infrastructure build with 0 errors.

### Overall Testability Assessment: WELL TESTABLE

The codebase has **strong DI infrastructure and interface-based architecture**, with **~90% of application logic testable today** without changes (up from ~60% pre-Phase C, ~75% pre-OM, ~80% pre-v3, ~85% pre-v4, ~88% post-v4 quick/medium wins). The remaining **~10% is tightly coupled to Windows APIs** (P/Invoke, COM/WinRT) that can't be mocked without further abstraction. v4 medium effort items resolved: `MoreMenuViewModel` now fully testable (F-18/F-19), 3 services decoupled from static `App.MainWindow` (F-22). Post-v4 significant: `WinGetService` decomposed into 4 focused services behind interfaces (F-26), `SoftwareAppsPage.HelpButton_Click` service locator eliminated (F-23/SR-3). v6 decomposed 4 remaining large files into focused classes with proper SRP, added `ISettingViewModelEnricher`, `ISettingPreparationPipeline`, `IFilePickerService`, `ISelectedAppsProvider` interfaces, and 5 new sub-ViewModels for WimUtil.

**v4 fresh review (2026-02-24):** All 20 sampled v1–v3 changes were verified as correctly implemented in the actual source code (20/20). The v4 analysis found 26 new findings. **20/26 findings resolved** (6 quick wins + 6 medium effort + 3 significant + F-23 + F-1/F-6/F-14/F-21). All highest-impact testability blockers resolved:
- ~~`SettingViewModelFactory` registered as concrete type without interface (F-4)~~ ✅ Resolved
- ~~`ConfigExportService`, `ConfigAppSelectionService` depend on concrete `WindowsAppsViewModel`/`ExternalAppsViewModel` (F-9)~~ ✅ Resolved — decoupled via `IWindowsAppsItemsProvider`/`IExternalAppsItemsProvider`
- ~~`MoreMenuViewModel` embeds P/Invoke `DllImport` and COM Shell.Application interop (F-18)~~ ✅ Resolved — extracted to `IExplorerWindowManager` + `ExplorerWindowManager`
- ~~`MainWindow.xaml.cs` has grown to 1483 lines with 25+ service locator calls (F-25)~~ ✅ Reduced to 722 lines (v6); startup logic extracted to `IStartupOrchestrator`/`StartupOrchestrator` (v4), then further decomposed into `TaskProgressCoordinator`, `NavigationRouter`, `StartupUiCoordinator`, `TitleBarManager` helpers + XAML `x:Bind` bindings (v6)
- ~~`MainWindowViewModel` has 16 constructor parameters (F-7)~~ ✅ Reduced to 11 params; `IWinGetStartupService` + `IWindowsVersionFilterService` extracted

**v5 final review (2026-02-24):** Independent verification confirmed 18/18 sampled changes across v1-v4 as correctly implemented. Fresh code quality scan found only 8 minor findings (0 blocking). See `code-quality-v5.md` for the final analysis.

See `code-quality-v4.md`, `code-quality-v5.md`, and `code-quality-v6.md` for the full analyses.

---

## Architecture Summary (for test planning)

- **3-Layer Architecture:**
  - `Winhance.Core`: Interfaces, models, enums (abstraction layer)
  - `Winhance.Infrastructure`: Service implementations (business logic)
  - `Winhance.UI`: WinUI 3 views and ViewModels (presentation)

- **Project Dependencies:**
  ```
  Winhance.UI → Winhance.Infrastructure → Winhance.Core
  Winhance.UI → Winhance.Core (direct, for interfaces)
  ```

- **DI Registration:** All services registered in extension methods:
  - `InfrastructureServicesExtensions.cs` (~46 interface-to-implementation registrations, ~52 total `Add*` calls including forwarding)
  - `DomainServicesExtensions.cs` (~30 services, ~41 total `Add*` calls including forwarding)
  - `UIServicesExtensions.cs` (UI-specific — includes 5 config sub-services + facade, `ISelectedAppsProvider`, `IFilePickerService`, `SettingViewModelDependencies`, `ISettingViewModelEnricher`, `ISettingPreparationPipeline`, and WimUtil sub-VMs)
  - Services registered as Singletons or Scoped (scoped for services with per-operation state like `IWindowsAppsService`, `IExternalAppsService`, `IAppInstallationService`, `IAppUninstallationService`, `IAppLoadingService`, `IAppUninstallService`, `ILegacyCapabilityService`, `IOptionalFeatureService`)

---

## What IS Testable Today (No Refactoring Needed)

### ViewModels & MVVM Logic

All ViewModels use constructor injection and are fully mockable:

```csharp
// Example: All dependencies injectable, can be mocked (no concrete VM references)
public PowerOptimizationsViewModel(
    IDomainServiceRouter domainServiceRouter,
    ISettingsLoadingService settingsLoadingService,
    ILogService logService,
    ILocalizationService localizationService,
    IDispatcherService dispatcherService,
    IDialogService dialogService,
    IEventBus eventBus,
    IPowerPlanComboBoxService powerPlanComboBoxService)
```

**Testable ViewModel behaviors:**
- Observable property change notifications
- RelayCommand execution and CanExecute logic
- Tab switching logic (`SoftwareAppsViewModel`)
- Search/filter logic (`BaseSettingsFeatureViewModel._searchText` with debounce)
- Selection state management (`SettingItemViewModel` AC/DC values)
- Mutual exclusion patterns (`WizardActionCard` — `_isComplete`/`_hasFailed`/`_isProcessing`)
- Settings loading and display pipeline

### Service Logic (Behind Interfaces)

50+ services have proper `IFoo` / `Foo` pairs:

- `ILogService` / `LogService`
- `IWindowsRegistryService` / `WindowsRegistryService`
- `IEventBus` / `EventBus`
- `IDomainServiceRouter` / `DomainServiceRouter`
- `ISettingApplicationService` / `SettingApplicationService`
- `IWinGetBootstrapper` / `WinGetBootstrapper`, `IWinGetDetectionService` / `WinGetDetectionService`, `IWinGetPackageInstaller` / `WinGetPackageInstaller` (decomposed from former `WinGetService`)
- `IWindowsAppsService` / `WindowsAppsService`
- `ITaskProgressService` / `TaskProgressService`
- `IDialogService` / `DialogService`
- `ILocalizationService` / `LocalizationService`
- And 40+ more

**Testable service behaviors:**
- `DomainServiceRouter` — routing settings to correct domain services by feature ID
- `EventBus` — publish/subscribe message flow, subscription cleanup, async handler observation
- `SettingApplicationService` — setting routing/orchestration logic (9 params after OM2; mocked `ISettingOperationExecutor` for operation execution)
- `SettingOperationExecutor` — registry, scheduled task, PowerShell, reg content, PowerCfg, native power API operations (OM2: extracted from SettingApplicationService, 10 params — all mockable)
- `SettingDependencyResolver` — dependency resolution, auto-enable, preset sync (OM2: extracted)
- `RecommendedSettingsApplier` — recommended setting application (OM2: extracted)
- `PowerCfgApplier` — power config value application via P/Invoke (OM2: extracted; returns `OperationResult` per OM4)
- `IConfigurationApplicationBridgeService` / `ConfigurationApplicationBridgeService` — config import/export logic, value resolution (M1: interface extracted)
- `IConfigMigrationService` / `ConfigMigrationService` — backward compatibility transformations (M1: interface extracted)
- `IConfigLoadService` / `ConfigLoadService` — config loading, validation, compatibility filtering (S2+S7)
- `IConfigAppSelectionService` / `ConfigAppSelectionService` — app selection from config (S2+S7)
- `IConfigExportService` / `ConfigExportService` — config export and backup (S2+S7)
- `IConfigApplicationExecutionService` / `ConfigApplicationExecutionService` — import execution pipeline (S2+S7)
- `IConfigReviewOrchestrationService` / `ConfigReviewOrchestrationService` — review mode lifecycle (S2+S7)
- `TooltipDataService` — tooltip data assembly from setting definitions
- `ComboBoxSetupService` — dropdown option building from setting metadata
- `CompatibleSettingsRegistry` — settings discovery and compatibility filtering
- `SettingViewModelFactory` — ViewModel creation with combobox setup (S5). Now behind `ISettingViewModelFactory` interface (v4 F-4 ✅). v6: reduced from 13 to 7 constructor params via `SettingViewModelDependencies` record (6 pass-through deps) + `ISettingViewModelEnricher` service (hardware detection, cross-group info, review diff). `SettingsLoadingService` fully testable with mocked factory.
- `ISettingViewModelEnricher` / `SettingViewModelEnricher` — post-construction enrichment: battery detection, cross-group info messages, review diff application (v6: extracted from SettingViewModelFactory)
- `ISettingPreparationPipeline` / `SettingPreparationPipeline` — setting filtering + localization pipeline (v6: extracted from SettingsLoadingService)
- `ISelectedAppsProvider` / `SelectedAppsProvider` — bridges SoftwareApps feature to `AutounattendXmlGeneratorService`, wraps `WindowsAppsViewModel` selected items (v6)
- `IFilePickerService` / `FilePickerService` — abstracts Win32 file/folder picker dialogs via `IMainWindowProvider` (v6)
- `ISettingReviewDiffApplier` / `SettingReviewDiffApplier` — review diff computation and application to ViewModels (S5)
- `IReviewModeViewModelCoordinator` / `ReviewModeViewModelCoordinator` — review mode ViewModel coordination, wrapping 5 concrete ViewModels behind mockable interface (v3 ME-1+ME-2)
- `IActionCommandProvider` / implementations on `StartMenuService`, `TaskbarService` — typed command dispatch replacing reflection (v3 SR-2)
- `NumericConversionHelper` — shared numeric conversion utility (v3 QW-1). **Note:** `internal static` class in Infrastructure — requires `[InternalsVisibleTo]` on the Infrastructure project for direct testing.

### Data Models & Utilities

- `ConfigurationItem` serialization/deserialization
- `SettingDefinition` parsing and validation
- `PowerCfgSetting` value handling (now record with `{ get; init; }` per Q1)
- `TaskProgressDetail` state management
- `ApplySettingRequest` parameter object (M4: record with required + optional properties)
- `OperationResult` (non-generic) — factory methods `Succeeded()`, `Failed(msg)`, `Failed(msg, ex)` (OM4: standardized return type for fallible operations across 7 interfaces)
- `OperationResult<T>` (generic) — pre-existing, used in SoftwareApps services
- `ConfigReviewDiff`, `ScriptMigrationResult`, `ImageDetectionResult`, `PowerPlanComboBoxOption` — now immutable records (M3), equality semantics usable in assertions
- `ItemDefinition` — 3 dead mutable fields removed (S6: `Version`, `LastOperationError`, `IsSelected`); only `IsInstalled`/`DetectedVia` remain as documented runtime state

### Example Test (What's Possible Today)

```csharp
[Fact]
public async Task ApplySetting_WithMockedDependencies_ReturnsSuccess()
{
    // Arrange — SettingApplicationService has 9 params after OM2 extraction
    var mockRouter = new Mock<IDomainServiceRouter>();
    var mockLogService = new Mock<ILogService>();
    var mockRegistry = new Mock<IGlobalSettingsRegistry>();
    var mockEventBus = new Mock<IEventBus>();
    var mockRecommended = new Mock<IRecommendedSettingsApplier>();
    var mockRestartManager = new Mock<IProcessRestartManager>();
    var mockDepResolver = new Mock<ISettingDependencyResolver>();
    var mockCompatFilter = new Mock<IWindowsCompatibilityFilter>();
    var mockOperationExecutor = new Mock<ISettingOperationExecutor>();

    // Setup domain service to return a matching setting
    var mockDomainService = new Mock<IDomainService>();
    var testSetting = new SettingDefinition { Id = "test-setting-id" };
    mockDomainService.Setup(d => d.GetSettingsAsync())
        .ReturnsAsync(new[] { testSetting });
    mockRouter.Setup(r => r.GetDomainService("test-setting-id"))
        .Returns(mockDomainService.Object);
    mockOperationExecutor.Setup(e => e.ApplySettingOperationsAsync(
        It.IsAny<SettingDefinition>(), It.IsAny<bool>(), It.IsAny<object?>()))
        .ReturnsAsync(OperationResult.Succeeded());

    var service = new SettingApplicationService(
        mockRouter.Object, mockLogService.Object, mockRegistry.Object,
        mockEventBus.Object, mockRecommended.Object, mockRestartManager.Object,
        mockDepResolver.Object, mockCompatFilter.Object, mockOperationExecutor.Object);

    // Act — returns OperationResult (OM4)
    var result = await service.ApplySettingAsync(new ApplySettingRequest
    {
        SettingId = "test-setting-id",
        Enable = true
    });

    // Assert
    Assert.True(result.Success);
    mockOperationExecutor.Verify(e => e.ApplySettingOperationsAsync(
        testSetting, true, null), Times.Once);
}

[Fact]
public async Task PowerService_RunPowercfg_ReturnsCapturedOutput()
{
    // Arrange — IProcessExecutor is now mockable (Phase B, S1)
    // PowerService constructor: (ILogService, IPowerSettingsQueryService,
    //   ICompatibleSettingsRegistry, IEventBus, IPowerPlanComboBoxService,
    //   IProcessExecutor, IFileSystemService)
    // Note: no ISettingApplicationService in constructor — received via method param
    var mockProcessExecutor = new Mock<IProcessExecutor>();
    mockProcessExecutor
        .Setup(p => p.ExecuteAsync("powercfg", It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new ProcessExecutionResult { ExitCode = 0, StandardOutput = "GUID: abc-123" });

    var service = new PowerService(
        mockLogService.Object, mockPowerSettingsQuery.Object,
        mockCompatibleSettings.Object, mockEventBus.Object,
        mockPowerPlanComboBox.Object, mockProcessExecutor.Object,
        mockFileSystemService.Object);

    // Act
    var result = await service.GetAvailablePowerPlansAsync();

    // Assert — no real powercfg.exe needed
    Assert.NotEmpty(result);
}

[Fact]
public void DomainServiceRouter_GetDomainService_ReturnsCorrectServiceForFeatureId()
{
    // Arrange
    var mockService = new Mock<IDomainService>();
    mockService.Setup(s => s.DomainName).Returns("Windows.Theme");
    var router = new DomainServiceRouter(new[] { mockService.Object });

    // Act
    var result = router.GetDomainService("Windows.Theme");

    // Assert
    Assert.Same(mockService.Object, result);
}

[Fact]
public void EventBus_Publish_NotifiesAllSubscribers()
{
    // Arrange — EventBus requires ILogService (for error logging in handlers)
    var mockLogService = new Mock<ILogService>();
    var eventBus = new EventBus(mockLogService.Object);
    var received = false;
    eventBus.Subscribe<SettingAppliedEvent>(e => received = true);

    // Act — SettingAppliedEvent(settingId, isEnabled, value?)
    eventBus.Publish(new SettingAppliedEvent("test-id", true));

    // Assert
    Assert.True(received);
}
```

---

## What is NOT Testable Today (Requires Refactoring)

### 1. Direct `Process.Start()` — ~~15+ locations~~ MOSTLY RESOLVED ✅

**Status:** `IProcessExecutor` abstraction created and injected into ~17 services (2026-02-23).

**Interface:** `IProcessExecutor` in `Winhance.Core/Features/Common/Interfaces/` with 3 methods:
- `ExecuteAsync` — captured stdout/stderr, UTF-8 encoding
- `ExecuteWithStreamingAsync` — line-by-line callbacks for DISM/Chocolatey progress
- `ShellExecuteAsync` — UseShellExecute=true for URLs, Explorer, installers

**Still using direct Process:**
| Service | Reason |
|---------|--------|
| `WinGetCliRunner` (static) | Needs interface extraction |
| 3 SoftwareApps services | `WinGetBootstrapper`, `WinGetDetectionService`, `WinGetPackageInstaller` use direct Process via `WinGetCliRunner` |
| `SystemBackupService` | Direct `ProcessStartInfo` for `vssadmin` |

**Note:** `PowerShellRunner` was converted from static to injectable `IPowerShellRunner` in Phase C (S7).

### 2. P/Invoke Static Classes — Unmockable (Partially Centralized)

These are static classes with `[DllImport]` attributes — they cannot be mocked or replaced:

| Static Class | Location | P/Invoke Count | What it does |
|-------------|----------|---------------|-------------|
| `UserTokenApi` | `Core/Native/` | 16 methods | Process token manipulation, user elevation detection |
| `User32Api` | `Core/Native/` | 1 method + 5 constants | `SendMessageTimeout` for GUI refresh (OM5: centralized from local declaration in `WindowsUIManagementService`) |
| `ConPtyApi` | `Core/Native/` | 8 methods | Pseudo-console creation for CLI output |
| `PowerProf` | `Core/Native/` | 12 methods | Power profile queries and modifications |
| `DismApi` | `Core/Native/` | Multiple | Windows feature management |
| `MsiApi` | `Core/Native/` | 3 methods | MSI package property queries (F-21: centralized from `DirectDownloadService`) |
| `SrClientApi` | `Core/Native/` | 1 method | System Restore `SRSetRestorePointW` (V5-7: centralized from `SystemBackupService`) |

**Note:** OM5 centralized `SendMessageTimeout` from a local `[DllImport]` inside `WindowsUIManagementService.RefreshWindowsGUI` into `User32Api` in `Core/Native/`. F-21 centralized MSI `DllImport` from `DirectDownloadService` into `MsiApi.cs` in `Core/Native/`. V5-7 centralized `SRSetRestorePointW` from `SystemBackupService` into `SrClientApi.cs` in `Core/Native/`. V5-8 centralized `SystemParametersInfo` from `WallpaperService` into `User32Api` in `Core/Native/`. All P/Invoke declarations are now in the `Core/Features/Common/Native/` folder (except script-embedded strings in `AutounattendScriptBuilder`).

**Refactoring needed:** Wrap each behind an interface:
```csharp
// BEFORE (untestable):
public static class PowerProf
{
    [DllImport("powrprof.dll")]
    public static extern uint PowerEnumerate(...);
}

// AFTER (testable):
public interface IPowerProfileNative
{
    uint PowerEnumerate(...);
}

public class PowerProfileNative : IPowerProfileNative
{
    public uint PowerEnumerate(...) => PowerProf.PowerEnumerate(...);
}
```

### 3. COM/WinRT Direct Usage — WinGetComSession

`WinGetComSession` (extracted from former `WinGetService`) directly manages COM objects:
```csharp
private WindowsPackageManagerFactory? _winGetFactory;
private PackageManager? _packageManager;
// Direct COM object handling — not abstracted
```

**Status:** COM state is now isolated in `WinGetComSession` (singleton), injected into `WinGetBootstrapper`, `WinGetDetectionService`, and `WinGetPackageInstaller`. The decomposition makes COM-dependent logic independently testable — `WinGetPackageInstaller` (CLI-only install/uninstall) can be tested without COM by mocking `WinGetComSession`. `WinGetDetectionService` has COM+CLI fallback; the CLI path is testable without COM.

**Remaining refactoring:** Abstract `WinGetComSession` behind an interface (`IWinGetComSession`) for full mockability in tests:
```csharp
public interface IWinGetComSession
{
    bool EnsureComInitialized();
    void ResetFactory();
    PackageManager? PackageManager { get; }
    WindowsPackageManagerFactory? Factory { get; }
}
```

### 4. ~7-8 Utility Services/Classes Without Interfaces

These concrete-only services/classes can't be swapped for test doubles:

| Service/Class | Type | Location | Notes |
|--------------|------|----------|-------|
| ~~`SettingViewModelFactory`~~ | ~~DI singleton~~ | ~~UI~~ | ✅ **RESOLVED** — `ISettingViewModelFactory` extracted (v4 F-4) |
| `AutounattendScriptBuilder` | DI singleton | Infrastructure | PowerShell script generation (v6: 1859→265 line orchestrator + 7 extracted section/helper classes), no interface |
| `DismSessionManager` | static class | Infrastructure | Static DISM wrapper |
| `WinGetCliRunner` | static class | Infrastructure | Static WinGet wrapper, also uses direct `File.Exists` |
| `WinGetExitCodes` | static class | Infrastructure | Static exit code mapping |
| `WinGetProgressParser` | static class | Infrastructure | Static output parsing |
| `WinGetInstaller` | class with DI deps | Infrastructure | 552 lines, created via `new` inside `WinGetBootstrapper` (not DI-registered). Has 6 deps: `IPowerShellRunner`, `HttpClient`, `ILogService`, `ILocalizationService`, `ITaskProgressService`, `IFileSystemService`. Testable through `WinGetBootstrapper` with mocked deps, or could extract `IWinGetInstaller` for direct mockability. |
| `BloatRemovalScriptGenerator` | static class | Core | Generates bloat removal PowerShell scripts |
| `ConPtyProcess` | internal sealed | Infrastructure | Pseudo-console wrapper |
| `NumericConversionHelper` | internal static | Infrastructure | Shared utility (v3 QW-1), needs `[InternalsVisibleTo]` |
| `WinGetComSession` | DI singleton (concrete) | Infrastructure | COM state management, no interface — needs `IWinGetComSession` for full mockability |

**Previously resolved:**
- ~~`ConfigurationApplicationBridgeService`~~ → `IConfigurationApplicationBridgeService` (M1)
- ~~`ConfigMigrationService`~~ → `IConfigMigrationService` (M1)
- ~~`AutounattendXmlGeneratorService`~~ → `IAutounattendXmlGeneratorService` (M8)
- ~~`NavBadgeService`~~ → `INavBadgeService`
- ~~`DriverCategorizer`~~ → `IDriverCategorizer` (S7)
- ~~`PowerShellRunner`~~ → `IPowerShellRunner` (S7)
- ~~`RegeditLauncher`~~ → `IRegeditLauncher` (S7)

**Note:** `IFileSystemService` was created in Phase C (S8) to abstract `File.*`/`Directory.*` calls, and **all 24 eligible Infrastructure files AND 5 UI layer files (v3 ME-3) were migrated** in code-quality-v2 S1 + v3 ME-3 (~227+ calls replaced). ~~`MoreMenuViewModel` (v4 F-19)~~ ✅ now also uses `IFileSystemService`. Remaining direct `System.IO` usage: `LogService` (bootstrapping — acceptable), `WinGetCliRunner` (static utility).

**Refactoring needed:** Extract interfaces for remaining concrete services. ~~`SettingViewModelFactory` is the highest priority~~ ✅ Resolved (v4 F-4). ~~`StartupOrchestrator` extraction (F-25)~~ ✅ Resolved. ~~`MainWindowViewModel` decomposition (F-7)~~ ✅ Resolved. ~~`WinGetService` decomposition (F-26)~~ ✅ Resolved — decomposed into `IWinGetBootstrapper`/`IWinGetDetectionService`/`IWinGetPackageInstaller` + `WinGetComSession`. ~~Service locator elimination (SR-3)~~ ✅ Partially resolved — `SoftwareAppsPage.HelpButton_Click` moved to ViewModel command (~61 `GetRequiredService` calls remain: 27 in code-behind + 34 in DI registration files). v6 added `IFilePickerService`, `ISelectedAppsProvider`, `ISettingViewModelEnricher`, `ISettingPreparationPipeline` — further reducing concrete-only dependencies.

### 5. `IServiceProvider` Injected Directly — FULLY RESOLVED ✅

**Status:** All 6 locations resolved (2026-02-23):
- `AutounattendScriptBuilder` → explicit `IPowerSettingsQueryService` + `IHardwareDetectionService`
- `WimUtilViewModel` → explicit `IAutounattendXmlGeneratorService`
- `PowerService` → receives `ISettingApplicationService` via method parameter on `TryApplySpecialSettingAsync` (no constructor dependency)
- `UpdateService` → receives `ISettingApplicationService` via method parameter on `TryApplySpecialSettingAsync` (no constructor dependency)
- `AutounattendXmlGeneratorService` → IServiceProvider removed (M8); now uses `ISelectedAppsProvider` to gather selected apps internally (v6 refinement — service is fully self-contained for `GenerateFromCurrentSelectionsAsync`)
- `ConfigurationService` → IServiceProvider removed (S2+S7); decomposed into 5 focused services (`ConfigLoadService`, `ConfigAppSelectionService`, `ConfigExportService`, `ConfigApplicationExecutionService`, `ConfigReviewOrchestrationService`) + thin facade. All 17 `IServiceProvider.GetService<T>()` calls across 13 call sites eliminated via direct constructor injection of singleton ViewModels and `ISettingsLoadingService`.

**Circular dependency fully eliminated (Phase C, S3):** The original cycle (`DomainServiceRouter → IDomainService(PowerService/UpdateService) → ISettingApplicationService → DomainServiceRouter`) was permanently resolved using a method-parameter callback pattern. `IDomainService.TryApplySpecialSettingAsync` accepts an optional `ISettingApplicationService?` parameter, which the orchestrator passes as `this` at call time. Zero `Lazy<T>` remains in the codebase.

### 6. `ServiceController` Direct Usage — RESOLVED ✅

**Status:** Resolved in Phase C (S3/S9). `ServiceController` usage was extracted from `SettingApplicationService` into `ProcessRestartManager`, which is injected via the `IProcessRestartManager` interface. The Windows service dependency is now isolated behind a mockable abstraction.

---

## Testability Scoring by Area

| Area | Score | Notes |
|------|-------|-------|
| **DI Architecture** | 10/10 | Excellent — `IServiceProvider` fully eliminated from all services. Zero circular dependencies (method-parameter callback pattern). |
| **Interface Coverage** | 9.5/10 | Most services have interfaces. v4 resolved remaining high-impact gaps: `ISettingViewModelFactory` (F-4), `IWindowsAppsItemsProvider`/`IExternalAppsItemsProvider` (F-9), `IExplorerWindowManager` (F-18), `IMainWindowProvider` (F-22). v3 added `IReviewModeViewModelCoordinator`, `IActionCommandProvider`. HttpClient now DI-injected in 5 services. IFileSystemService migrated to 5 UI layer files. v6 added `IFilePickerService`, `ISelectedAppsProvider`, `ISettingViewModelEnricher`, `ISettingPreparationPipeline`. |
| **Service Coupling** | 9.5/10 | Good DI, most large services decomposed. Config sub-services decoupled from concrete VMs via `IWindowsAppsItemsProvider`/`IExternalAppsItemsProvider` (v4 F-9 ✅). `MainWindowViewModel` reduced from 16 to 11 params (v4 F-7 ✅). v6: `SettingViewModelFactory` 13→7 params, `SettingsLoadingService` 9→8 params, `AutounattendXmlGeneratorService` now self-contained (gathers selected apps via `ISelectedAppsProvider` internally). |
| **ViewModel Design** | 9.5/10 | Constructor injection throughout, most dependencies injectable. `MainWindowViewModel` constructor has zero side effects (OB3). Decomposed into 3 child VMs (S3). v4 F-18 resolved: `MoreMenuViewModel` P/Invoke + COM extracted to `IExplorerWindowManager`, now fully testable via DI. v6: `WimUtilViewModel` decomposed into 5 sub-ViewModels (425-line orchestrator); `SoftwareAppsViewModel.ShowHelpAsync` no longer creates ContentDialog directly. |
| **System Abstraction** | 8/10 | Process.Start abstracted via `IProcessExecutor`. ServiceController abstracted via `IProcessRestartManager`. File I/O abstracted via `IFileSystemService` (24 files migrated). P/Invoke centralized in `Core/Native/` (OM5: `User32Api` added). IFileSystemService extended to 5 UI layer files (v3 ME-3). HttpClient DI-injected in 5 services (v3 QW-7+ME-5). Remaining gaps: P/Invoke wrappers, COM |
| **Static Method Usage** | 7.5/10 | `PowerShellRunner`, `DriverCategorizer`, `RegeditLauncher` converted to injectable (S7). P/Invoke centralized in Native folder (OM5). `WinGetService` decomposed into 3 interface-backed services + COM session (F-26). Remaining static: `WinGetCliRunner`, `DismSessionManager`, `WinGetProgressParser` |
| **Event System** | 10/10 | `IEventBus` is well-abstracted with both sync `Subscribe` and async `SubscribeAsync` overloads (S8); 4 `async void` handlers converted to proper `async Task` with observed error handling |
| **Error Handling** | 10/10 | `OperationResult` standardized across 7 interfaces for fallible operations (OM4). Boolean queries (`IsTaskRegistered`, `HasBattery`) correctly remain `Task<bool>`. Async event handlers use `SubscribeAsync` with Task observation (S8). Empty catch blocks eliminated from 5 UI controls (OL2). |
| **Overall Unit Testing** | 9/10 | ~90% testable in isolation; OM2 decomposition makes `SettingApplicationService` and `SettingOperationExecutor` independently testable; OB3 makes `MainWindowViewModel` constructable without side effects; v3 ME-1+ME-2 makes `ConfigReviewOrchestrationService` testable with mocked VMs; F-26 decomposition makes WinGet install/detect/bootstrap independently testable |
| **Overall Integration Testing** | 7/10 | Process execution mockable, ServiceController abstracted, file I/O fully abstracted. P/Invoke and COM still tightly coupled |

---

## Implementation Plan

### Phase 1: Test Infrastructure + Low-Hanging Fruit

**Goal:** Set up test projects and write tests for the 60% that's already testable.

**Steps:**
1. Create test projects:
   - `Winhance.Core.Tests` (model/utility tests)
   - `Winhance.Infrastructure.Tests` (service logic tests)
   - `Winhance.UI.Tests` (ViewModel tests)
2. Add frameworks: **xUnit** + **Moq** (or NSubstitute) + **FluentAssertions**
3. Write tests for:
   - `EventBus` publish/subscribe (sync + async handlers)
   - `DomainServiceRouter` routing logic
   - `SettingApplicationService` routing/orchestration (9 params, OM2 — mock `ISettingOperationExecutor`)
   - `SettingOperationExecutor` operation dispatch (10 params, OM2 — mock registry/PowerCfg/scheduled tasks)
   - `SettingDependencyResolver` dependency resolution, auto-enable, preset sync
   - `RecommendedSettingsApplier` recommended setting application
   - `SettingItemViewModel` property state management
   - `ConfigurationItem` model behavior
   - `BaseSettingsFeatureViewModel` search/filter logic
   - `SoftwareAppsViewModel` tab switching
   - `OptimizeViewModel` / `CustomizeViewModel` section routing and search (accept `IEnumerable<T>` — easy to mock)
   - `WizardActionCard` mutual exclusion state
   - `MainWindowViewModel` — constructable without side effects (OB3); test `Initialize()` separately
   - `TaskProgressViewModel` progress state, cancel/close/show-details commands (S3)
   - `UpdateCheckViewModel` update check flow, InfoBar state management (S3)
   - `ReviewModeBarViewModel` review mode state, apply/cancel commands, status text (S3)
   - `WimStep1ViewModel`, `WimStep2XmlViewModel`, `WimStep3DriversViewModel`, `WimStep4IsoViewModel`, `WimImageFormatViewModel` — independently testable sub-VMs (v6; not DI-registered — instantiated via `new` in `WimUtilViewModel` with deps passed manually, but all constructor deps are interface-based)
   - `WimUtilViewModel` orchestrator — wizard navigation state, step availability logic, sub-VM observation (v6)
   - `SettingViewModelEnricher` — battery detection, cross-group info, review diff (v6: extracted from factory)
   - `SettingPreparationPipeline` — setting filtering + localization (v6: extracted from loading service)
   - `SelectedAppsProvider` — selected Windows apps mapping (v6)
   - `RegistryCommandEmitter` — registry PS command generation (v6: extracted from AutounattendScriptBuilder)
   - `PowerShellScriptUtilities` — pure utility functions (v6: extracted from AutounattendScriptBuilder)
   - `ComboBoxSetupService` option building
   - `TooltipDataService` data assembly
   - `CompatibleSettingsRegistry` settings discovery
   - `OperationResult` / `OperationResult<T>` factory methods and state

**Estimated scope:** 300-400 unit tests for thorough Phase 1 coverage (happy path + error path + edge cases per method). The expanded test file structure (see below) includes 9 settings feature ViewModels, 5 WimUtil sub-ViewModels, ~67 Infrastructure service tests (including 7 AutounattendScriptBuilder sections), 4 Core service tests, and ~25 UI service tests.

**No refactoring required.** All major testability blockers have been resolved through v1-v6 code quality rounds (`ISettingViewModelFactory`, `IAppItemsProvider`, `IExplorerWindowManager`, `IMainWindowProvider`, `IStartupOrchestrator`, `IFilePickerService`, `ISelectedAppsProvider`, `ISettingViewModelEnricher`, `ISettingPreparationPipeline`, etc.).

### Phase 2: `IProcessExecutor` Abstraction ✅ COMPLETED

**Status:** Completed 2026-02-23. `IProcessExecutor` interface created in Core with 3 methods (`ExecuteAsync`, `ExecuteWithStreamingAsync`, `ShellExecuteAsync`). `ProcessExecutor` implementation in Infrastructure. Injected into ~17 services across Infrastructure and UI layers. Registered as singleton in DI.

**Unlocked by IProcessExecutor (now testable with mocked process execution):** `PowerService`, `UpdateService`, `WimUtilService`, `ChocolateyService`, `DirectDownloadService`, `AppUninstallService`, `StartMenuService`, `WindowsUIManagementService`, `VersionService`, `InteractiveUserService`, `ApplicationCloseService`, `SettingApplicationService` (9 params, routes to `IProcessExecutor` via sub-services), `SettingOperationExecutor` (10 params), `PowerCfgApplier`.

**Note:** The following services were already testable before Phase 2 (no `IProcessExecutor` dependency): `SettingDependencyResolver`, `RecommendedSettingsApplier`, `ConfigurationService` (facade), `ConfigLoadService`, `ConfigAppSelectionService`, `ConfigExportService`, `ConfigApplicationExecutionService`, `ConfigReviewOrchestrationService`, `SettingViewModelFactory`, `SettingReviewDiffApplier`, `SettingsLoadingService`.

**Remaining:** `WinGetCliRunner` (static) and 3 decomposed WinGet services (`WinGetBootstrapper`, `WinGetDetectionService`, `WinGetPackageInstaller`) still use direct Process via `WinGetCliRunner`. `PowerShellRunner` was converted to injectable `IPowerShellRunner` in Phase C (S7). ~~`SystemBackupService` direct `ProcessStartInfo` for `vssadmin`~~ ✅ **RESOLVED** (v7 V7-5) — now uses `IProcessExecutor`.

### Phase 3: Interface Extraction for Concrete Services — PARTIALLY COMPLETED ✅

**Status:** Phase C (S7) converted 3 static utility classes to injectable services with interfaces:
- `PowerShellRunner` → `IPowerShellRunner` (7 callers updated)
- `DriverCategorizer` → `IDriverCategorizer` (1 caller updated)
- `RegeditLauncher` → `IRegeditLauncher` (2 callers updated)

Additionally, `IFileSystemService` was created (S8) to abstract `File.*`/`Directory.*` calls. **Caller migration is complete** — 24 files migrated in code-quality-v2 S1 (~227 calls replaced).

**Goal:** Make the remaining ~8-9 interface-less services/classes mockable.

**Remaining steps (by priority):**
1. ~~Extract `ISettingViewModelFactory` (v4 F-4)~~ ✅ **DONE** — `SettingsLoadingService` now testable
2. ~~Extract `IAppItemProvider` for `WindowsAppsViewModel`/`ExternalAppsViewModel` (v4 F-9)~~ ✅ **DONE** — `IWindowsAppsItemsProvider`/`IExternalAppsItemsProvider` extracted, 2 config sub-services decoupled
3. Extract interfaces for remaining concrete utility services (`WinGetCliRunner`, `DismSessionManager`, `AutounattendScriptBuilder`, etc.)
4. Update DI registrations and consumers
5. Add `[InternalsVisibleTo]` on Infrastructure project for `NumericConversionHelper` testing

**Unlocks:** Testing for service consumers that depend on these utilities.

### Phase 4: P/Invoke & COM Abstraction — PARTIALLY COMPLETED ✅

**Status:** `ServiceController` usage abstracted behind `IProcessRestartManager` in Phase C (S3/S9). `SendMessageTimeout` centralized into `User32Api` in `Core/Native/` (OM5).

**Goal:** Make remaining Windows system calls testable.

**Remaining steps:**
1. Wrap `UserTokenApi`, `User32Api`, `ConPtyApi`, `PowerProf`, `DismApi` behind interfaces
2. ~~Abstract `WinGetService` COM initialization behind a factory interface~~ ✅ **RESOLVED** — COM state extracted to `WinGetComSession` singleton, shared by `WinGetBootstrapper`, `WinGetDetectionService`, `WinGetPackageInstaller` (F-26)
3. Write tests for elevation detection, power management, feature management

**Unlocks:** Testing for the deepest system integration layer.

### Phase 5: Remove `IServiceProvider` Direct Injection — FULLY COMPLETED ✅

**Status:** All 6 locations resolved (2026-02-23). `AutounattendScriptBuilder` and `WimUtilViewModel` use explicit constructor parameters. `PowerService` and `UpdateService` receive `ISettingApplicationService` via method parameter on `TryApplySpecialSettingAsync` (no constructor dependency — circular dependency fully eliminated). `AutounattendXmlGeneratorService` resolved in M8 (callers pass selected apps as parameter). `ConfigurationService` resolved in S2+S7 (decomposed into 5 focused services + facade; all 17 `IServiceProvider` resolutions eliminated via direct constructor injection).

Zero `IServiceProvider` usage remains outside of DI registration infrastructure.

---

## Testing Strategy by Layer

### Unit Tests (xUnit + Moq)

**Target:** All service logic, ViewModel logic, model behavior.

**Note:** `[InternalsVisibleTo]` attributes are needed on `Winhance.Infrastructure` (for `NumericConversionHelper`, `RegistryCommandEmitter`, `FeatureRegistryScriptSection`, `PowerSettingsScriptSection`, `ScriptPreambleSection`, `AppRemovalScriptSection`, `SpecialFeatureScriptSection`, `PowerShellScriptUtilities`) and `Winhance.UI` (for `SettingStatusBannerManager`, `TechnicalDetailsManager`, `UnitConversionHelper`, `ConfigRegistryInitializer`) to test `internal` classes directly.

**NOT unit testable (require WinUI 3 runtime — defer to integration tests):** `TaskProgressCoordinator` (takes `TaskProgressControl`, `DispatcherQueue`), `NavigationRouter` (uses `Frame`, page types), `StartupUiCoordinator` (references `Microsoft.UI.Xaml.Window`), `TitleBarManager` (references `Window`), `ConfigImportDialogBuilder` (uses WinUI `ContentDialog`, `StackPanel`), `TaskOutputDialogBuilder` (uses WinUI controls), `DialogAccessibilityHelper` (uses `UIElement`, `AutomationPeer`). These 7 classes depend directly on WinUI 3 types and cannot be tested with a standard xUnit runner.

```
Winhance.Core.Tests/
  ├── Models/
  │   ├── ConfigurationItemTests.cs
  │   ├── SettingDefinitionTests.cs
  │   └── OperationResultTests.cs
  ├── Services/
  │   ├── GlobalSettingsRegistryTests.cs          # impl lives in Core
  │   ├── DependencyManagerTests.cs               # impl lives in Core
  │   ├── InitializationServiceTests.cs           # impl lives in Core
  │   └── LogServiceTests.cs                      # impl lives in Core (bootstrapping constraints noted)
  ├── Utils/
  │   └── SearchHelperTests.cs                     # public static, pure function MatchesSearchTerm()
  └── Events/
      └── EventBusTests.cs

Winhance.Infrastructure.Tests/
  ├── Services/
  │   │── DomainServiceRouterTests.cs
  │   │── SettingApplicationServiceTests.cs
  │   │── SettingOperationExecutorTests.cs
  │   │── SettingDependencyResolverTests.cs
  │   │── RecommendedSettingsApplierTests.cs
  │   │── PowerCfgApplierTests.cs
  │   │── ProcessRestartManagerTests.cs
  │   │── ComboBoxSetupServiceTests.cs
  │   │── ComboBoxResolverTests.cs
  │   │── TooltipDataServiceTests.cs
  │   │── CompatibleSettingsRegistryTests.cs
  │   │── GlobalSettingsPreloaderTests.cs         # impl lives in Infrastructure (moved from UI.Tests)
  │   │── RecommendedSettingsServiceTests.cs      # impl lives in Infrastructure (moved from UI.Tests)
  │   │── SystemSettingsDiscoveryServiceTests.cs
  │   │── ConfigMigrationServiceTests.cs
  │   │── ConfigurationApplicationBridgeServiceTests.cs
  │   │── ScheduledTaskServiceTests.cs
  │   │── VersionServiceTests.cs
  │   │── InternetConnectivityServiceTests.cs
  │   │── UserPreferencesServiceTests.cs
  │   │── ScriptMigrationServiceTests.cs
  │   │── RemovalScriptUpdateServiceTests.cs
  │   │── ExplorerWindowManagerTests.cs
  │   │── NumericConversionHelperTests.cs         # internal static — needs [InternalsVisibleTo]
  │   │── TooltipRefreshEventHandlerTests.cs     # public, 4 interface deps — event subscription/refresh logic
  │   │── WindowsAppsServiceTests.cs
  │   │── ExternalAppsServiceTests.cs
  │   │── AppInstallationServiceTests.cs
  │   │── AppUninstallationServiceTests.cs
  │   │── AppStatusDiscoveryServiceTests.cs
  │   │── AppLoadingServiceTests.cs
  │   │── AppUninstallServiceTests.cs             # scoped, IAppUninstallService
  │   │── WinGetBootstrapperTests.cs
  │   │── WinGetDetectionServiceTests.cs
  │   │── WinGetPackageInstallerTests.cs
  │   │── WinGetStartupServiceTests.cs            # IWinGetStartupService
  │   │── BloatRemovalServiceTests.cs
  │   │── PowerServiceTests.cs
  │   │── PowerSettingsQueryServiceTests.cs
  │   │── PowerSettingsValidationServiceTests.cs
  │   │── PowerPlanComboBoxServiceTests.cs        # IPowerPlanComboBoxService
  │   │── UpdateServiceTests.cs
  │   │── StartMenuServiceTests.cs
  │   │── TaskbarServiceTests.cs
  │   │── WallpaperServiceTests.cs
  │   │── WindowsThemeServiceTests.cs
  │   │── ExplorerCustomizationServiceTests.cs
  │   │── HardwareDetectionServiceTests.cs
  │   │── HardwareCompatibilityFilterTests.cs
  │   │── WindowsCompatibilityFilterTests.cs
  │   │── TaskProgressServiceTests.cs             # ITaskProgressService + IMultiScriptProgressService
  │   │── SystemBackupServiceTests.cs             # ISystemBackupService (v7: now takes IProcessExecutor)
  │   │── WindowsVersionServiceTests.cs           # IWindowsVersionService
  │   │── WindowsUIManagementServiceTests.cs      # IWindowsUIManagementService
  │   │── LocalizationServiceTests.cs             # ILocalizationService
  │   │── InteractiveUserServiceTests.cs          # IInteractiveUserService
  │   │── ChocolateyServiceTests.cs               # IChocolateyService
  │   │── StoreDownloadServiceTests.cs            # IStoreDownloadService
  │   │── DirectDownloadServiceTests.cs           # IDirectDownloadService
  │   │── LegacyCapabilityServiceTests.cs         # ILegacyCapabilityService (scoped)
  │   │── OptionalFeatureServiceTests.cs          # IOptionalFeatureService (scoped)
  │   │── PrivacyAndSecurityServiceTests.cs       # IDomainService impl
  │   │── GamingPerformanceServiceTests.cs        # IDomainService impl
  │   │── NotificationServiceTests.cs             # IDomainService impl
  │   └── SoundServiceTests.cs                    # IDomainService impl
  ├── Utilities/
  │   ├── WinGetExitCodesTests.cs                 # static class — pure function testing, no mocking
  │   └── WinGetProgressParserTests.cs            # static class — pure function testing, no mocking
  └── AdvancedTools/
      ├── AutounattendScriptBuilderTests.cs       # thin orchestrator (v6: 265 lines)
      ├── RegistryCommandEmitterTests.cs          # registry PS command emission (v6: extracted)
      ├── PowerShellScriptUtilitiesTests.cs       # static utilities — pure function testing (v6: extracted)
      ├── FeatureRegistryScriptSectionTests.cs    # feature registry script section (v6: extracted)
      ├── PowerSettingsScriptSectionTests.cs      # power settings script section (v6: extracted)
      ├── AppRemovalScriptSectionTests.cs         # app removal scripts section (v6: extracted)
      ├── SpecialFeatureScriptSectionTests.cs     # special feature handlers (v6: extracted)
      ├── ScriptPreambleSectionTests.cs           # script preamble section (v6: extracted from AutounattendScriptBuilder)
      └── WimUtilServiceTests.cs                  # WimUtilService (1577 lines — largest Infrastructure service)

Winhance.UI.Tests/
  ├── ViewModels/
  │   ├── SettingItemViewModelTests.cs
  │   ├── SettingStatusBannerManagerTests.cs      # internal sealed — needs [InternalsVisibleTo]
  │   ├── TechnicalDetailsManagerTests.cs         # internal sealed — needs [InternalsVisibleTo]
  │   ├── BaseSettingsFeatureViewModelTests.cs
  │   ├── SoftwareAppsViewModelTests.cs           # Note: depends on concrete WindowsAppsVM/ExternalAppsVM
  │   ├── OptimizeViewModelTests.cs               # 57 lines, extends SectionPageViewModel<T>
  │   ├── CustomizeViewModelTests.cs              # 51 lines, extends SectionPageViewModel<T>
  │   ├── SectionPageViewModelTests.cs            # base class shared logic
  │   ├── WizardActionCardTests.cs                # model (ObservableObject), not DI-registered
  │   ├── WizardStepStateTests.cs                 # public INotifyPropertyChanged model — computed properties (IsLocked, ShowChevron, ChevronRotation)
  │   ├── MainWindowViewModelTests.cs
  │   ├── TaskProgressViewModelTests.cs
  │   ├── UpdateCheckViewModelTests.cs
  │   ├── ReviewModeBarViewModelTests.cs
  │   ├── WimUtilViewModelTests.cs                # thin orchestrator (v6: 425 lines)
  │   ├── WimStep1ViewModelTests.cs               # Step 1: ISO selection/extraction (v6: extracted)
  │   ├── WimImageFormatViewModelTests.cs          # Image format detection/conversion (v6: extracted)
  │   ├── WimStep2XmlViewModelTests.cs             # Step 2: XML management (v6: extracted)
  │   ├── WimStep3DriversViewModelTests.cs         # Step 3: driver injection (v6: extracted)
  │   ├── WimStep4IsoViewModelTests.cs             # Step 4: ISO creation (v6: extracted)
  │   ├── RemovalStatusViewModelTests.cs
  │   ├── RemovalStatusContainerViewModelTests.cs
  │   ├── AppItemViewModelTests.cs
  │   ├── MoreMenuViewModelTests.cs
  │   ├── SettingsViewModelTests.cs
  │   ├── AutounattendGeneratorViewModelTests.cs
  │   ├── AdvancedToolsViewModelTests.cs
  │   ├── PowerOptimizationsViewModelTests.cs
  │   ├── GamingOptimizationsViewModelTests.cs
  │   ├── NotificationOptimizationsViewModelTests.cs
  │   ├── PrivacyOptimizationsViewModelTests.cs
  │   ├── SoundOptimizationsViewModelTests.cs
  │   ├── UpdateOptimizationsViewModelTests.cs
  │   ├── ExplorerCustomizationsViewModelTests.cs
  │   ├── StartMenuCustomizationsViewModelTests.cs
  │   ├── TaskbarCustomizationsViewModelTests.cs
  │   ├── WindowsThemeCustomizationsViewModelTests.cs
  │   ├── ExternalAppsViewModelTests.cs
  │   └── WindowsAppsViewModelTests.cs
  └── Services/
      ├── ConfigLoadServiceTests.cs
      ├── ConfigAppSelectionServiceTests.cs
      ├── ConfigExportServiceTests.cs
      ├── ConfigApplicationExecutionServiceTests.cs
      ├── ConfigReviewOrchestrationServiceTests.cs
      ├── ConfigurationServiceTests.cs
      ├── SettingViewModelFactoryTests.cs          # v6: 7 params (was 13)
      ├── SettingViewModelEnricherTests.cs          # ISettingViewModelEnricher (v6: extracted from factory)
      ├── SettingPreparationPipelineTests.cs        # ISettingPreparationPipeline (v6: extracted from loading service)
      ├── FilePickerServiceTests.cs                 # IFilePickerService (v6)
      ├── SelectedAppsProviderTests.cs              # ISelectedAppsProvider (v6)
      ├── SettingReviewDiffApplierTests.cs
      ├── ConfigReviewServiceTests.cs
      ├── ReviewModeViewModelCoordinatorTests.cs
      ├── NavBadgeServiceTests.cs
      ├── ApplicationCloseServiceTests.cs
      ├── SettingsLoadingServiceTests.cs              # v6: 8 params (was 9)
      ├── ConfigImportOverlayServiceTests.cs
      ├── ChocolateyConsentServiceTests.cs
      ├── SettingLocalizationServiceTests.cs
      ├── ThemeServiceTests.cs
      ├── StartupOrchestratorTests.cs             # IStartupOrchestrator
      ├── WindowsVersionFilterServiceTests.cs     # IWindowsVersionFilterService
      ├── StartupNotificationServiceTests.cs      # IStartupNotificationService
      ├── UnitConversionHelperTests.cs            # internal static — needs [InternalsVisibleTo], pure functions
      └── ConfigRegistryInitializerTests.cs       # internal static — needs [InternalsVisibleTo], has interface deps
```

### Integration Tests (Future, Post-Refactoring)

**Target:** Full setting application workflows, configuration import/export pipelines.

These use real service implementations but mock the OS boundary:
- Mocked `IProcessExecutor` returns canned CLI output
- Mocked `IWindowsRegistryService` uses in-memory dictionary
- Mocked `IPowerProfileNative` returns test power data

### Real-World Bug Scenarios Tests Should Catch

#### Issue #482 — Power page blank after config import (race condition)

**Bug:** When a user imports a config and then navigates to the Power page under Optimizations, the page shows completely blank — no settings displayed.

**Root cause:** A race condition in `BaseSettingsFeatureViewModel.LoadSettingsAsync()`. The `_settingsLoaded` boolean flag is set to `true` inside a `lock` block BEFORE the async loading actually completes. When config import triggers review mode (which eagerly pre-computes diffs for all settings including Power via `ConfigReviewService`), and the user then navigates to the Power page, `LoadSettingsAsync()` is called again — but the flag is already `true`, so it returns immediately with an empty `Settings` collection. The UI binds to `GroupedSettings` which never gets rebuilt.

**Key code locations:**
- `BaseSettingsFeatureViewModel.cs` ~line 285-287: `_settingsLoaded = true` set before await
- `BaseSettingsFeatureViewModel.cs` ~line 279-314: `Settings.Clear()` runs, but `RebuildGroupedSettings()` may never complete
- `BaseSettingsFeatureViewModel.cs` ~line 416-448: `RebuildGroupedSettings` returns early if Settings is empty
- `PowerOptimizationsViewModel.cs` ~line 41-47: `LoadSettingsAsync` override doesn't handle race conditions
- `ConfigReviewOrchestrationService.cs`: `EnterReviewModeAsync` triggers eager diff computation
- `PowerOptimizePage.xaml` ~line 15: `CollectionViewSource` binds to potentially empty `GroupedSettings`

**Tests that would catch this:**
1. **Concurrent loading test** — Call `LoadSettingsAsync()` twice in rapid succession; assert `GroupedSettings` is populated after both calls complete.
2. **Load-after-clear test** — Call `LoadSettingsAsync()`, then clear settings and call it again; assert settings are reloaded (flag should reset or loading should re-trigger).
3. **Config-import-then-navigate integration test** — Mock the full flow: enter review mode (which pre-computes diffs), then call `LoadSettingsAsync()` on the Power VM; assert `GroupedSettings` is not empty.
4. **Flag state test** — Assert `_settingsLoaded` is only set to `true` AFTER the async loading pipeline completes successfully, not before.

**Recommended fix direction:**
- Move `_settingsLoaded = true` to after loading completes successfully
- Consider using `SemaphoreSlim` instead of `lock` + boolean for async-safe guarding
- Add `CancellationToken` support to cancel in-flight loads when a new load is requested

**Lesson:** Simple boolean guards around async operations are a common source of race conditions. Tests that exercise concurrent/sequential calling patterns catch these bugs before users do.

---

### What NOT to Test (Extensively)

- XAML bindings (test the ViewModel, trust WinUI data binding)
- P/Invoke signatures (these are declarations, not logic)
- Generated MVVM Toolkit code (`OnPropertyChanged`, etc.)
- DI container wiring in general (trust Microsoft.Extensions.DependencyInjection) — **exception:** add a DI smoke test that verifies all 3 `IConfigReviewService` sub-interface forwarding casts resolve correctly (OB6: compile-time safe via interface inheritance, but a smoke test provides runtime confidence)

---

## Strengths of the Current Architecture (for testing)

1. **Excellent DI setup** — all services properly registered and injectable; zero `IServiceProvider` usage
2. **Interface-first design** — Core defines interfaces, Infrastructure implements; ISP applied (`ISpecialSettingHandler` opt-in per OM1, `IConfigReviewService` split into 3 per S2)
3. **Clean ViewModel injection** — all VM dependencies constructor-injected; `MainWindowViewModel` has zero constructor side effects (OB3: `Initialize()` deferred)
4. **Well-abstracted event system** — `IEventBus` provides clean sync + async pub/sub (S8)
5. **Zero circular dependencies** — method-parameter callback pattern (zero `Lazy<T>` in codebase)
6. **No service-to-service `new` instantiation** — all cross-service dependencies go through DI
7. **Well-decomposed services** — `SettingApplicationService` split: routing (9 params) + execution via `ISettingOperationExecutor` (10 params) per OM2. `ConfigurationService` decomposed into 5 focused services + facade (S2+S7). `MainWindowViewModel` decomposed into 3 child VMs (S3). `SettingsLoadingService` decomposed into 3 classes (S5). `SettingItemViewModel` decomposed with 2 manager classes (S4). Window/ExternalAppsVM deps pushed into service layer (OM3). v6: `WimUtilViewModel` decomposed into 5 sub-ViewModels + orchestrator. `AutounattendScriptBuilder` decomposed into 7 section/helper classes + orchestrator. `DialogService` decomposed into 2 builder classes + accessibility helper. `MainWindow.xaml.cs` decomposed into 4 helper classes. `SettingViewModelFactory` reduced from 13→7 params via `SettingViewModelDependencies` + `ISettingViewModelEnricher`. **1 service file >1000 lines remains** (`WimUtilService.cs` at 1577 lines — deferred to V8-11; 4 data-only model files in Core are also >1000 lines but contain pure setting definitions, not service logic).
8. **File I/O fully abstracted** — all 24 eligible Infrastructure services AND 5 UI layer files (v3 ME-3) use `IFileSystemService`, enabling mock-based testing without touching the real file system
9. **Standardized error returns** — `OperationResult` (non-generic) used across 7 interfaces for fallible operations (OM4); `.Success` property enables clean test assertions without try/catch

## Critical Weaknesses (for testing)

**Active:**
1. **P/Invoke not abstracted** — `UserTokenApi`, `User32Api`, `ConPtyApi`, `PowerProf`, `DismApi`, `MsiApi` cannot be mocked (centralized in `Core/Native/` per OM5, but still static). ~~`MoreMenuViewModel` P/Invoke (v4 F-18)~~ ✅ moved to `User32Api` + `ExplorerWindowManager`. ~~`DirectDownloadService` (v4 F-21) inline `DllImport`~~ ✅ moved to `MsiApi.cs`.
2. **COM/WinRT not fully abstracted** — `WinGetComSession` manages `PackageManager` directly (extracted from former `WinGetService` in F-26; now isolated in one class instead of spread across a 1168-line monolith). ~~`MoreMenuViewModel` COM Shell.Application interop (v4 F-18)~~ ✅ moved to `ExplorerWindowManager` behind `IExplorerWindowManager`.
3. **~7-8 services/classes without interfaces** — remaining utility services can't be swapped for test doubles. `SettingViewModelFactory` resolved (v4 F-4 ✅).
4. ~~**Service→ViewModel layer violation**~~ ✅ Resolved — `ConfigExportService`, `ConfigAppSelectionService` now depend on `IWindowsAppsItemsProvider`/`IExternalAppsItemsProvider` interfaces (v4 F-9).
5. ~~**`MainWindow.xaml.cs` — 1483 lines, 25+ service locator calls**~~ ✅ Reduced to 722 lines (v6 V6-1). Startup orchestration extracted to `IStartupOrchestrator`/`StartupOrchestrator` (v4). Further decomposed into `TaskProgressCoordinator`, `NavigationRouter`, `StartupUiCoordinator`, `TitleBarManager` helpers + ~244 lines of PropertyChanged handlers eliminated via XAML `x:Bind` bindings (v6).
6. ~~**Static `App.MainWindow` access in services**~~ ✅ **RESOLVED** — `ConfigExportService` and `ConfigLoadService` no longer access `App.MainWindow` directly; replaced with `IMainWindowProvider` (v4 F-22).
7. **`SoftwareAppsViewModel` and `AutounattendGeneratorViewModel` concrete ViewModel dependencies** — `SoftwareAppsViewModel` constructor takes concrete `WindowsAppsViewModel` and `ExternalAppsViewModel` (not interfaces). `AutounattendGeneratorViewModel` also depends on concrete `WindowsAppsViewModel`. These have complex constructors, making full mockability difficult without creating real child VM instances. Workaround: use Moq with virtual members or create lightweight test doubles. (v6 added `IDialogService` dependency; `ShowHelpAsync` no longer creates ContentDialog directly.)

**Resolved:**
- ~~**`Process.Start()` not abstracted**~~ ✅ **RESOLVED** — `IProcessExecutor` injected into ~17 services
- ~~**`IServiceProvider` injection**~~ ✅ **FULLY RESOLVED** — all locations use explicit deps
- ~~**`ServiceController` direct usage**~~ ✅ **RESOLVED** — abstracted behind `IProcessRestartManager`
- ~~**Inconsistent error return patterns**~~ ✅ **RESOLVED** (OM4) — `OperationResult` standardized across 7 interfaces
- ~~**MainWindowViewModel untestable constructor**~~ ✅ **RESOLVED** (OB3) — side effects deferred to `Initialize()`

---

## Summary

### Phase Status Overview

| Phase | Status | Description |
|-------|--------|-------------|
| **Phase 1** | **READY** | Test infrastructure + unit tests for ~90% already-testable code |
| **Phase 2** | ✅ COMPLETE | `IProcessExecutor` abstraction — ~17 services now testable |
| **Phase 3** | PARTIAL | Interface extraction — 3 static utilities converted + `WinGetService` decomposed (F-26) + v6 added `IFilePickerService`, `ISelectedAppsProvider`, `ISettingViewModelEnricher`, `ISettingPreparationPipeline`; ~5-6 concrete-only utilities remaining |
| **Phase 4** | PARTIAL | P/Invoke & COM abstraction — `ServiceController` done, WinGet COM isolated to `WinGetComSession` (F-26), P/Invoke wrappers pending |
| **Phase 5** | ✅ COMPLETE | `IServiceProvider` removal — all 6 locations resolved |

### Current State (post v1-v8 refactoring, verified 2026-02-25)

**~90% of application logic is testable today.** v5 independent verification confirmed 18/18 sampled v1-v4 code quality changes as correctly implemented. The v5 fresh review found only 8 minor findings (0 blocking, all resolved). v6 decomposed the 4 remaining large files (>1000 lines) into focused single-responsibility classes, added 4 new interfaces (`ISettingViewModelEnricher`, `ISettingPreparationPipeline`, `IFilePickerService`, `ISelectedAppsProvider`), and created 5 independently-testable sub-ViewModels for the WIM utility wizard. v7 fixed 8 correctness/resource-leak issues including `SystemBackupService` now using `IProcessExecutor` (eliminating the last direct `Process` usage outside static utilities). v8 fixed 10 code quality issues: cancellation propagation, resource disposal, volatile fields, null safety, unnecessary async removal, and reflection optimization. The codebase is ready for test authoring.

**What's ready now (Phase 1 — no refactoring needed):**
- All ViewModels with constructor injection (~33 concrete ViewModels, including 5 new WimUtil sub-VMs from v6)
- All services behind interfaces (91+ DI-registered interface-to-implementation pairs, +4 from v6)
- EventBus pub/sub, DomainServiceRouter routing, SettingApplicationService orchestration
- Config import/export pipeline (5 sub-services + facade)
- OperationResult-based assertions across 7 interfaces
- v6 extracted classes: `RegistryCommandEmitter`, `PowerShellScriptUtilities`, 5 script sections (all independently testable)
- Estimated scope: 300-400 unit tests for thorough coverage (expanded test file structure with ~40 additional test files from v6)

**What blocks the remaining ~10%:**
1. P/Invoke static classes in `Core/Native/` (7 classes, unmockable: `UserTokenApi`, `User32Api`, `ConPtyApi`, `PowerProf`, `DismApi`, `MsiApi`, `SrClientApi`)
2. COM/WinRT in `WinGetComSession` (direct `PackageManager` — isolated from former `WinGetService`)
3. ~6-7 concrete-only utilities without interfaces (`AutounattendScriptBuilder` (v6: 265-line orchestrator, testable with mocked section builders), `DismSessionManager`, `WinGetCliRunner`, `WinGetExitCodes`, `WinGetProgressParser`, `BloatRemovalScriptGenerator`, `ConPtyProcess`, `WinGetComSession`)
4. `SoftwareAppsViewModel` depends on concrete `WindowsAppsViewModel`/`ExternalAppsViewModel` (not interfaces)

### Recommended Pre-Test Code Quality Items (from v4)

**v4 Quick Wins — ALL RESOLVED (2026-02-24):**
1. ~~**Extract `ISettingViewModelFactory`**~~ ✅ (v4 F-4) — interface extracted, DI updated, `SettingsLoadingService` now uses `ISettingViewModelFactory`
2. ~~**Make `FireAndForget()` require `logService`**~~ ✅ (v4 F-13) — `logService` now required (non-nullable), logs at Warning. `RegeditLauncher` and `SettingsViewModel` updated with `ILogService` constructor params
3. ~~**Replace `_ = HandleToggleAsync()` with `FireAndForget()`**~~ ✅ (v4 F-15) — all 9 fire-and-forget patterns in `SettingItemViewModel` now use `FireAndForget(_logService)`
4. ~~**Extract shared `ConvertFromSystemUnits` helper**~~ ✅ (v4 F-16) — `UnitConversionHelper` shared between `SettingViewModelFactory` and `SettingItemViewModel`
5. ~~**Extract shared `EnsureRegistryInitializedAsync` helper**~~ ✅ (v4 F-17) — `ConfigRegistryInitializer` shared between `ConfigurationService` and `ConfigExportService`
6. ~~**Add logging to bare `catch` blocks in version info**~~ ✅ (v4 F-11) — `MoreMenuViewModel` and `MainWindowViewModel` now log at Debug level

**v4 Medium Effort — ✅ ALL RESOLVED (2026-02-24):**
7. ~~**Inject `IFileSystemService` into `MoreMenuViewModel`**~~ ✅ (v4 F-19) — 4 direct `Directory.*` calls replaced with `_fileSystemService`
8. ~~**Extract `IExplorerWindowManager` from `MoreMenuViewModel`**~~ ✅ (v4 F-18) — P/Invoke moved to `User32Api.cs`, COM Shell interop moved to `ExplorerWindowManager` in Infrastructure, `MoreMenuViewModel` now fully testable
9. ~~**Extract `IMainWindowProvider`**~~ ✅ (v4 F-22) — replaces static `App.MainWindow` access in `ConfigExportService`, `ConfigLoadService`, `ThemeService`
10. ~~**Extract `IAppItemsProvider` interfaces**~~ ✅ (v4 F-9) — `IWindowsAppsItemsProvider`/`IExternalAppsItemsProvider` extracted, `ConfigExportService` and `ConfigAppSelectionService` decoupled from concrete ViewModels, DI factory registrations added
11. ~~**Create `SettingItemViewModelConfig` record**~~ ✅ (v4 F-2) — config record captures 13 init properties, `SettingItemViewModel` constructor takes config instead of relying on object initializer, factory creates config record
12. ~~**Extract `SectionPageViewModel<T>` base class**~~ ✅ (v4 F-24) — generic base class with `ISectionInfo` constraint consolidates ~250 lines of duplicated init/search/nav logic; `OptimizeViewModel` (321→57 lines) and `CustomizeViewModel` (315→51 lines) now only define section list + named properties; duplicate `SearchSuggestionItem` consolidated to `Common/Models/`

**Remaining v4 significant items (not yet implemented):**
- ~~**Extract `StartupOrchestrator`** (v4 F-25)~~ ✅ Done — `MainWindow.xaml.cs` reduced from 1483 to 1362 lines (v4), then to 722 lines (v6 V6-1: 4 helper classes + XAML x:Bind bindings)
- ~~**Decompose `MainWindowViewModel`** (v4 F-7)~~ ✅ Done — 16 params reduced to 11; `IWinGetStartupService` + `IWindowsVersionFilterService` extracted
- ~~**Decompose `WinGetService`** (v4 F-26)~~ ✅ Done — 1168-line monolith decomposed into `WinGetComSession` + `WinGetBootstrapper`/`WinGetDetectionService`/`WinGetPackageInstaller` (3 interfaces, 4 implementations); 6 callers migrated, old `IWinGetService`/`WinGetService` deleted
- ~~**Service locator elimination** (SR-3)~~ ✅ Partially done — `SoftwareAppsPage.HelpButton_Click` moved to `SoftwareAppsViewModel.ShowHelpCommand` (~61 `GetRequiredService` calls remain: 27 in code-behind + 34 in DI registration files)

**v4 Correctness & Consistency Fixes (2026-02-24):**
- ~~**Make `ComboBoxOption` immutable** (v4 F-1)~~ ✅ Done — constructor + get-only `Value`/`Description`/`Tag` properties; `init` not viable due to WinUI XAML codegen; 6 call sites updated
- ~~**Type `ObservableCollection<object>`** (v4 F-6)~~ ✅ Done — `ISettingsLoadingService` return type changed to `ObservableCollection<SettingItemViewModel>`; removed `.Cast<>().ToList()` in `BaseSettingsFeatureViewModel`
- ~~**Add missing `ConfigureAwait(false)`** (v4 F-14)~~ ✅ Done — 4 awaits in `WinGetStartupService` + 12 awaits in `DependencyManager`
- ~~**Move MSI `DllImport` to `Native/`** (v4 F-21)~~ ✅ Done — `MsiApi.cs` created in `Core/Features/Common/Native/`; `DirectDownloadService` updated to use `MsiApi.*`

See `code-quality-v4.md` for the full 26-finding analysis with prioritized remediation plan.

### v5 Final Review (2026-02-24) + Remediation (2026-02-25)

**v5 code quality review** (`code-quality-v5.md`) performed a fresh-eyes final pass. Found 8 minor items, 0 blocking. **All 8 resolved on 2026-02-25** (commit `98bf09b`):

| Finding | Severity | Resolution |
|---------|----------|------------|
| V5-1: Thread safety in `GlobalSettingsRegistry` | MEDIUM | **RESOLVED** — Added `_listLock` for synchronized list access in `RegisterSetting`, `GetSetting`, `GetAllSettings` |
| V5-2: `LogService` missing `IDisposable` | LOW-MED | **RESOLVED** — Added `: IDisposable` to class declaration |
| V5-3: `_searchDebounceTokenSource` not disposed | LOW | **RESOLVED** — Dispose before creating new CTS + in `Dispose(bool)` |
| V5-4: `CompatibleSettingsRegistry` bare catches | LOW-MED | **RESOLVED** — All 5 catch blocks now log warnings with context |
| V5-5: Hardcoded hibernation ID in executor | LOW | **RESOLVED** — New `NativePowerApiSetting` model (data-driven, same pattern as `RegistrySetting`/`PowerCfgSetting`). Hibernation definition declares P/Invoke params; executor handles generically. No hardcoded IDs. |
| V5-6: `DomainServiceRouter._settingToFeatureMap` | LOW | **RESOLVED** — Replaced with `ConcurrentDictionary<string, string>` |
| V5-7: `SystemBackupService` P/Invoke location | LOW | **RESOLVED** — Created `SrClientApi.cs` in `Native/` folder |
| V5-8: `WallpaperService` duplicate P/Invoke | LOW | **RESOLVED** — Uses `User32Api.SystemParametersInfo()` now |

**New files from v5 remediation:**
- `Winhance.Core/Features/Common/Models/NativePowerApiSetting.cs` — data-driven model for `CallNtPowerInformation` P/Invoke
- `Winhance.Core/Features/Common/Native/SrClientApi.cs` — centralized P/Invoke for `SrClient.dll`

**v5 deferred items verification:**
- 6.4 (fire-and-forget without error handling) — **RESOLVED** (all call sites now use safe `FireAndForget(ILogService)`)
- M9/5.3 (IDomainService async for sync) — **NON-ISSUE** (interface is clean, async contract correct for general case)
- ~~ME-4/8.4 (large files)~~ — ✅ **RESOLVED** (v6 V6-1/V6-2/V6-3/V6-4: all 4 files decomposed — MainWindow.xaml.cs 1362→722, WimUtilViewModel 1363→425, AutounattendScriptBuilder 1859→265, DialogService 1181→480; 0 files >1000 lines remain)
- ~~8.5 (ViewModel showing dialogs)~~ — ✅ **RESOLVED** (v6 V6-5: `SoftwareAppsViewModel.ShowHelpAsync` now uses `_dialogService.ShowCustomContentDialogAsync()` instead of creating ContentDialog directly)
- All other deferred items confirmed as legitimate deferrals

### v6 Large File Decomposition (2026-02-25)

**v6 code quality review** (`code-quality-v6.md`) addressed 4 large files (>1000 lines), ContentDialog outside DialogService, constructor parameter bloat, and mutable collection defaults. **All 8 action items resolved** (commit `e2073fd`):

| Finding | Severity | Resolution |
|---------|----------|------------|
| V6-1: MainWindow.xaml.cs (1362 lines) | MEDIUM | **RESOLVED** — 1362→722 lines. Extracted `TaskProgressCoordinator`, `NavigationRouter`, `StartupUiCoordinator`, `TitleBarManager`. Replaced ~244 lines of PropertyChanged handlers with XAML `x:Bind` bindings. |
| V6-2: WimUtilViewModel (1363 lines) | MEDIUM | **RESOLVED** — 1363→425 lines. Extracted 5 sub-ViewModels: `WimStep1ViewModel` (294), `WimImageFormatViewModel` (320), `WimStep2XmlViewModel` (265), `WimStep3DriversViewModel` (209), `WimStep4IsoViewModel` (275). Created `ISelectedAppsProvider` (consumed by `AutounattendXmlGeneratorService`) and `IFilePickerService`. |
| V6-3: AutounattendScriptBuilder (1859 lines) | MEDIUM | **RESOLVED** — 1859→265 lines. Extracted `PowerShellScriptUtilities` (60), `RegistryCommandEmitter` (318), `FeatureRegistryScriptSection` (341), `PowerSettingsScriptSection` (262), `ScriptPreambleSection` (386), `AppRemovalScriptSection` (277), `SpecialFeatureScriptSection` (86). Fixed DRY violation in binary registry emit logic. |
| V6-4: DialogService (1181 lines) | MEDIUM | **RESOLVED** — 1181→480 lines. Extracted `ConfigImportDialogBuilder` (464), `TaskOutputDialogBuilder` (220), `DialogAccessibilityHelper` (29). `ExecuteDialogAsync` guard helper eliminated 9x boilerplate. |
| V6-5: ContentDialog outside DialogService | LOW-MED | **RESOLVED** — Added `ShowCustomContentDialogAsync` to `IDialogService`. `SoftwareAppsViewModel.ShowHelpAsync` refactored. |
| V6-6: SettingViewModelFactory 13 params | LOW | **RESOLVED** — 13→7 params via `SettingViewModelDependencies` record + `ISettingViewModelEnricher`. |
| V6-7: SettingsLoadingService 9 params | LOW | **RESOLVED** — 9→8 params via `ISettingPreparationPipeline`. Duplicated ComboBox loop extracted. |
| V6-8: Mutable collection defaults | LOW | **RESOLVED** — `Array.Empty<RegistrySetting>()` + static `ReadOnlyDictionary` singleton. |

**New testable classes from v6:** ~27 new files including 5 sub-ViewModels, 7 AutounattendScriptBuilder sections/helpers, 2 DialogService builders, 4 UI helpers, 4 new interfaces with implementations.

### v7 Correctness Fixes (2026-02-25)

**v7 code quality review** (`code-quality-v7.md`) focused on correctness and resource leaks. Found 8 issues. **All 8 resolved** (commit `678564f`). See `code-quality-v7.md` for details.

### v8 Code Quality Fixes (2026-02-25)

**v8 code quality review** (`code-quality-v8.md`) performed a fresh independent review. Found 11 issues. **10 resolved** (V8-11 deferred — WimUtilService decomposition):

| Finding | Severity | Resolution |
|---------|----------|------------|
| V8-1: `OperationCanceledException` swallowed in `StoreDownloadService.DownloadPackageAsync` | MEDIUM | **RESOLVED** — Added `catch (OperationCanceledException) { throw; }` before general catch |
| V8-2: `OperationCanceledException` swallowed in `StoreDownloadService.DownloadFileAsync` | MEDIUM | **RESOLVED** — Same pattern applied |
| V8-3: `null!` return in `DirectDownloadService.DownloadFileAsync` | LOW-MED | **RESOLVED** — Return type changed to `Task<string?>`, `null!` → `null` |
| V8-4: Handle leak in `ConPtyProcess.RunAsync` setup | MEDIUM | **RESOLVED** — Pipe handles wrapped in try/catch with cleanup on failure |
| V8-5: Missing `volatile` on `WinGetComSession` flags | LOW-MED | **RESOLVED** — `_isInitialized` and `_comInitTimedOut` marked `volatile` |
| V8-6: `ManagementObject` not disposed in foreach loops | LOW-MED | **RESOLVED** — Added `using (obj)` in `HardwareDetectionService` (2), `SystemBackupService` (1), `InteractiveUserService` (1) |
| V8-7: Null-forgiving `!` on nullable params | MEDIUM | **RESOLVED** — Null guards with `InvalidOperationException` in `UpdateService` (2) and `PowerService` (1) |
| V8-8: Unnecessary `async` on 7 domain services | LOW | **RESOLVED** — Removed `async`, return `Task.FromResult()` in `UpdateService`, `SoundService`, `PrivacyAndSecurityService`, `GamingPerformanceService`, `NotificationService`, `WindowsThemeService`, `ExplorerCustomizationService` |
| V8-9: Unused `CancellationToken` in `AppInstallationService` | LOW-MED | **RESOLVED** — Added `ThrowIfCancellationRequested()` checks between sequential service calls |
| V8-10: Double method invocation in `CompatibleSettingsRegistry` | LOW | **RESOLVED** — Single invocation during discovery; extracted both featureId and settings from one call; removed unused `GetFeatureIdFromMethod` |
| V8-11: `WimUtilService` at 1577 lines | LOW | **DEFERRED** — Requires significant decomposition into focused helpers |

**Status:** All code quality items across 8 rounds of review are resolved (1 deferred: V8-11 WimUtilService decomposition). 1 service file >1000 lines (WimUtilService.cs). Ready for Phase 1 test implementation.

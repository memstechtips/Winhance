# Winhance Improvement & Fix Plans 🛠️

This document outlines identified issues and proposed implementation plans for the Winhance utility.

## 1. Registry Reliability: Smart Locking & Unlocking
**Issue:**
Registry keys locked by Winhance (using `LockRegistryKey`) remain locked even when the user attempts to change the setting back. This causes "Access Denied" errors and prevents settings from being toggled.

**Implementation Plan:**
- **Refactor `RegistrySetting`:** Add a `LockCondition` enum (e.g., `Always`, `OnDisabled`, `Never`) to replace hardcoded `4` checks.
- **Update `WindowsRegistryService`:**
    - Implement a `CheckAccessControl` method to detect if a key is currently read-only.
    - Implement an "Unlock-Write-Relock" pattern in `ApplySetting`.
- **Validation:** Add unit tests simulating "Access Denied" scenarios using mocked registry wrappers.

## 2. Setting Application: Granular Status Reporting
**Issue:**
`SettingApplicationService` publishes success events even if only a portion of the operations (e.g., one of two registry keys) succeeded. This gives the user false confidence that a setting is fully applied.

**Implementation Plan:**
- **Refactor `OperationResult`:** Enhance it to support partial success states and detailed error collections per operation type.
- **Update `SettingApplicationService`:** Delay event publishing until all operations are attempted and include the full result in the event payload.
- **UI Update:** Modify `SettingStatusBannerManager` to show a "Warning" state for partial successes, allowing users to see exactly which part failed.

## 3. UI Architecture: Event Cleanup & DI Standard
**Issue:**
Manual event subscription management in code-behind (`xaml.cs`) is prone to memory leaks. The frequent use of `App.Services` (Service Locator) makes the UI layer difficult to unit test.

**Implementation Plan:**
- **Introduce WeakReferenceMessenger:** Replace `IEventBus` subscriptions in Views with `WeakReferenceMessenger` from the MVVM Toolkit to automate cleanup.
- **Standardize DI:** Move service resolutions from `OnNavigatedTo` into ViewModel constructors where possible, or use a pattern that allows mocking for UI tests.

## 4. Static Analysis: Comprehensive Catalog Validator
**Issue:**
Errors in setting definitions (invalid paths, missing localization, orphaned IDs) are only caught at runtime or by manual inspection.

**Implementation Plan:**
- **Expand `SettingCatalogValidator`:**
    - Add regex-based registry path validation.
    - Add a "Localization Integrity" check to ensure every setting has an `en.json` entry.
    - Add a "Feature Mapping" check to find settings not assigned to any category.
- **CI Integration:** Ensure the validator runs as part of the standard test suite.

## 5. UX: Technical Details Clarity (Task A9)
**Issue:**
The Technical Details panel uses generic "ValueNotExist" labels for multiple failure states, reducing its utility for troubleshooting.

**Implementation Plan:**
- **Complete Labels:** Add `KeyNotFound`, `ValueNotFound`, and `AccessDenied` strings to `TechnicalDetailLabels`.
- **Update Logic:** Refactor `TechnicalDetailsManager` to differentiate between a missing key and a missing value.
- **Feature Addition:** Add a "Copy Path" context menu or button to registry rows.

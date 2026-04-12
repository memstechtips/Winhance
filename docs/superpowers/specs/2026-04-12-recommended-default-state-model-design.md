# Design: Recommended & Default State Model

**Date:** 2026-04-12
**Author:** Marco + Claude
**Status:** Approved design, pending implementation plan

## Motivation

Winhance's InfoBadge system communicates three states on every setting card: **Recommended** (green), **Default** (grey), **Custom** (amber). Power users also get raw values in the Technical Details expander. Both depend on the data model knowing, for every setting, what the recommended value *is* and what Windows' default *is*.

Today that data is scattered and optional. Several problems surfaced:

1. **Per-registry-entry values don't fit ComboBox reality.** A ComboBox setting like "Windows Update Policy" has 23 registry entries but a single user-facing recommendation ("Security Only"). The current model forces authors to annotate every registry entry, producing either empty fields (silent gaps in the badge logic) or misleading per-key values that can drift from `ValueMappings`.
2. **`required` as a blunt instrument.** Marking `RegistrySetting.RecommendedValue` as `required` surfaced 46 real gaps (good) but also implied every registry entry in a ComboBox definition must carry a per-key recommended value (wrong — the concept lives at the option level).
3. **Parallel dictionaries in `ComboBoxMetadata`.** `DisplayNames[]`, `ValueMappings`, `SimpleValueMappings`, `CommandValueMappings`, `ScriptMappings`, `OptionTooltips`, `OptionWarnings`, `OptionConfirmations`, `ScriptVariables` are nine collections all keyed by the same option index. Authors must keep them in sync by hand.
4. **`RecommendedOption` / `DefaultOption` strings on `RegistrySetting`** are a workaround for (1) — they're meant to say "for this ComboBox option index, this entry's recommended lives there instead" — but the string-or-int ambiguity (`int.TryParse`) and the need to set them only on a "primary" entry makes the pattern fragile.
5. **NumericRange settings have no canonical place** to declare recommended/default beyond `RegistrySetting.RecommendedValue`, which is fine in isolation but inconsistent with how we think about Selection.

This design consolidates the model around one rule: **recommended/default state belongs at the level where the *user* decides it** — on the ComboBox option for Selection settings, on the registry entry for Toggle and NumericRange settings, on the PowerCfg / Task record for power and task settings.

## Goals

1. Every Toggle, Selection, and NumericRange setting yields a computable Recommended state and Default state at compile time or runtime — no silent gaps.
2. Authors add a new setting by filling in obvious, type-matching fields. No null placeholder noise for concepts that don't apply.
3. Non-technical users see the current state at a glance (InfoBadge, no model changes) and can reach Recommended or Default without knowing the underlying value.
4. Technical Details remains fully accurate for ComboBox settings: each registry entry shows what value it would take under Recommended or Default, resolved from one source of truth.
5. The `BulkSettingsActionService`, the badge computation, and config import/export all read from the new unified model.

## Non-goals

- CheckBox and Action input types are out of scope. They don't use badges today and won't after this change.
- Config Creation Mode (#422) is not part of this work.
- Changing which settings are currently recommended. The values already live in `Winhance_Recommended_Config.winhance`; this design moves where they live in code but doesn't alter what they are.

## Scope

Touches ~70 Selection settings, ~15 NumericRange settings, and the three model files: `RegistrySetting`, `ComboBoxMetadata`, `NumericRangeMetadata`. Leaves `PowerCfgSetting` and `ScheduledTaskSetting` as they are (already correct after the required-property pass earlier today).

---

## Data model changes

### `ComboBoxOption` (new)

One record per ComboBox option. Replaces the nine parallel dictionaries.

```csharp
public record ComboBoxOption
{
    public required string DisplayName { get; init; }
    public Dictionary<string, object?>? ValueMappings { get; init; }
    public int? SimpleValue { get; init; }
    public bool? CommandValue { get; init; }
    public ScriptOption? Script { get; init; }
    public string? Tooltip { get; init; }
    public string? Warning { get; init; }
    public (string Title, string Message)? Confirmation { get; init; }
    public Dictionary<string, string>? ScriptVariables { get; init; }
    public bool IsDefault { get; init; }
    public bool IsRecommended { get; init; }
}
```

### `ComboBoxMetadata` (changed)

```csharp
public record ComboBoxMetadata
{
    public required IReadOnlyList<ComboBoxOption> Options { get; init; }
    public bool SupportsCustomState { get; init; }
    public string? CustomStateDisplayName { get; init; }
}
```

Removed: `DisplayNames[]`, `ValueMappings`, `SimpleValueMappings`, `CommandValueMappings`, `ScriptMappings`, `OptionTooltips`, `OptionWarnings`, `OptionConfirmations`, `ScriptVariables`. All nine become properties of `ComboBoxOption`.

**Validation (unit test only, not startup):** A `SettingCatalogValidatorTests` test iterates every registered `SettingDefinition` and asserts:
- Every `ComboBoxMetadata` has **exactly one** option with `IsDefault = true`.
- Every `ComboBoxMetadata` has **at most one** option with `IsRecommended = true`. (Some purely informational ComboBoxes — e.g. "pick a DNS provider" — have no clear "recommended" option.)
- No duplicate `DisplayName` within a single `Options` list.
- If `SupportsCustomState = true`, the synthetic Custom entry (surfaced only at runtime via `CustomStateDisplayName`) is not counted toward the `IsDefault` / `IsRecommended` rules — `Options` lists the real options only.
- For ComboBox settings that write registry values: an audit step (see Migration) confirms each option uses *one* of `ValueMappings` / `SimpleValue` / `CommandValue` / `Script`. If the existing codebase has settings that combine them (e.g. registry write + follow-up script), the validator rule is softened to allow that specific combination and the spec is amended during implementation.

Validation runs in CI via the unit test. No startup-crash behaviour — a bad catalog fails the test build, never the running app.

**`DisplayName` localization:** `DisplayName` is the literal string that gets handed to the localizer at render time — identical semantics to today's `DisplayNames[]`. Resource-key indirection (if any) stays in the consumer layer, unchanged by this refactor.

### `RegistrySetting` (changed)

Remove three properties:
- `RecommendedOption` (string) — function moves to `ComboBoxOption.IsRecommended`.
- `DefaultOption` (string) — function moves to `ComboBoxOption.IsDefault`.
- `ComboBoxOptions` (`Dictionary<string, int>?`) — dead code. The field is declared on the record but never set in any model file. Consumers in `BulkSettingsActionService.cs` lines 363–380 reference it, but those branches have been unreachable in practice. Delete the property and the dead consumer branches as part of this work.

`RecommendedValue` and `DefaultValue` stay as `required object?`. Their semantics per InputType:

| InputType | `RegistrySetting.RecommendedValue` | `RegistrySetting.DefaultValue` |
|---|---|---|
| Toggle, CheckBox | Non-null. Must match `EnabledValue[0]` or `DisabledValue[0]`. | Non-null. Must match `EnabledValue[0]` or `DisabledValue[0]`. |
| NumericRange | Non-null integer. | Non-null integer (or `null` if Windows' default is "key absent"). |
| Selection | `null` — resolved via `ComboBoxMetadata.Options[i].ValueMappings`. | `null` — same. |
| Action (no badge) | `null`. | `null`. |

The `null` cases for Selection are explicit because `required` forces authors to write them — a deliberate acknowledgment that "this registry entry's recommendation is decided elsewhere", not a silent omission. Because `required object?` forces initialization but allows `null`, the catalog validator also asserts: for every `SettingDefinition` with `InputType == Selection`, every `RegistrySetting.RecommendedValue` and `DefaultValue` **must be `null`**. This catches the mirror mistake (accidental non-null value on a Selection-member entry) that the type system alone cannot express.

### `NumericRangeMetadata` (unchanged)

No new fields. The recommended/default numeric value continues to live on `RegistrySetting.RecommendedValue`/`DefaultValue`. NumericRange settings typically have one `RegistrySetting` per `SettingDefinition`, so this is natural.

### `PowerCfgSetting` (unchanged)

`RecommendedValueAC/DC` and `DefaultValueAC/DC` are already `required int?` after today's earlier work. Stays as the authoritative source for AC/DC power-config values.

### `ScheduledTaskSetting` (unchanged)

`RecommendedState` and `DefaultState` are already `required bool?`. Stays as the authoritative source for task state.

---

## Consumer changes

### `BulkSettingsActionService`

Simplified for Selection. Today it scans `RegistrySettings` for a `RecommendedOption` string and tries `int.TryParse`. After:

```csharp
// Apply Recommended for a Selection setting:
var recommendedIndex = setting.ComboBox.Options
    .Select((o, i) => (o, i))
    .FirstOrDefault(t => t.o.IsRecommended).i;
await ApplySettingAsync(setting.Id, Value: recommendedIndex, ...);
```

Same pattern for `IsDefault` in `ResetToDefaultsAsync`. Toggle/NumericRange paths are unchanged — they still read `RegistrySetting.RecommendedValue`/`DefaultValue`.

### `ComputeBadgeState` (`SettingItemViewModel`)

For Selection, replace the current "parse RecommendedOption as int" logic with:

```csharp
if (InputType == InputType.Selection)
{
    var metadata = SettingDefinition.ComboBox;
    if (metadata != null && SelectedValue is int currentIndex)
    {
        var recommendedIndex = /* index of Options[i] where IsRecommended */;
        var defaultIndex = /* index of Options[i] where IsDefault */;
        matchesRecommended = recommendedIndex.HasValue && currentIndex == recommendedIndex.Value;
        matchesDefault = defaultIndex.HasValue && currentIndex == defaultIndex.Value;
    }
}
```

Toggle and NumericRange branches unchanged.

**Tiebreak for options that are both `IsRecommended` and `IsDefault`:** when the current selection matches both, the badge renders as **Recommended** (green). This matches the visual tiebreak in the open-dropdown highlight. Covered by a dedicated unit test.

### Technical Details

For Selection settings, when Technical Details lists each registry entry with its "Recommended value" and "Default value" columns, the values come from:

- Recommended column: `ComboBoxMetadata.Options[recommendedIndex].ValueMappings[entry.ValueName]`
- Default column: `ComboBoxMetadata.Options[defaultIndex].ValueMappings[entry.ValueName]`

A `null` entry in `ValueMappings` displays as "— (key deleted)" or similar — matches current behaviour for group policy keys.

### Config import/export

The existing `.winhance` config files serialize `SelectedIndex` for Selection settings. No change — the index is still the index, regardless of where the Recommended flag now lives.

---

## Visual layer (scoped to this work)

### Toggle

No per-card visual change, and no per-card quick-set button. The InfoBadge on the card communicates state. Toggling the control flips to the other state — which is always either Recommended or Default, since Toggle has only two meaningful positions. "Custom" only surfaces when the registry has a stray third value, and toggling once writes back to `EnabledValue[0]` or `DisabledValue[0]`, resolving it.

For bulk "everything to Recommended" or "everything to Default", users still use the existing Quick Actions dropdown — that path is unchanged.

### Selection (ComboBox)

When the dropdown is open **and** `UserPreferences.ShowInfoBadges == true`:
- Option marked `IsRecommended`: green pill background, same palette as the Recommended badge.
- Option marked `IsDefault`: grey pill background, same palette as the Default badge.
- If one option is both (rare but allowed in the data): Recommended wins visually; tooltip reads "Recommended (also Windows default)".
- Hover tooltip on a highlighted option: "Recommended" / "Windows Default" (localized).

**Implementation constraint — closed-dropdown rendering:** the highlight pill must apply only to items in the popup list, never to the closed selection box. In WinUI 3, this means `ComboBox.ItemTemplate` carries the highlight visuals while `ComboBox.SelectionBoxItemTemplate` stays plain. A UI test asserts the closed dropdown shows no pill background for a Recommended-selected option.

### NumericRange (up/down spinner)

Two small quick-set buttons placed next to the spinner's up/down arrows:
- A green star icon — "Set to Recommended" — jumps the value to `RegistrySetting.RecommendedValue`.
- A grey reset icon — "Set to Default" — jumps the value to `RegistrySetting.DefaultValue`.
- Hover tooltip shows the target value, e.g. "Set to Recommended (100)".
- Icons only visible when `UserPreferences.ShowInfoBadges == true` (same global toggle as the card badges).
- The icons match the glyphs already used in the Quick Actions dropdown and the card badges, for consistency across the three surfaces.

For PowerCfg NumericRange settings with `PowerModeSupport.Separate`, each of the AC / DC spinners gets its own pair of quick-set buttons, backed by `PowerCfgSetting.RecommendedValueAC/DC` and `DefaultValueAC/DC`. Clicking the button on the AC spinner sets AC only; clicking on the DC spinner sets DC only — neither button touches the other power mode.

**Layout constraint:** the screenshot shows an already-tight row (title + badge + spinner). Two extra icons per spinner, doubled for AC/DC, risks overflow at narrow widths. The implementation plan must specify minimum card width / truncation behaviour and verify at 1024px, the current minimum supported window width.

---

## Migration strategy

Single PR, two clearly-separated phases inside the PR: **Phase A** (data model + consumer rewiring) can be fully reviewed and tested before **Phase B** (new visual layer) lands. The implementation plan splits accordingly.

### Phase A — Data model and consumers

1. Snapshot the current resolved Recommended/Default per setting ID (see "Golden snapshot" under Testing). This is the baseline for regression checks in step 10.
2. Add `ComboBoxOption` record. Add the new `Options` property to `ComboBoxMetadata` alongside the old fields. Mark the old fields `[Obsolete]` so the compiler surfaces every callsite but the build still succeeds.
3. Migrate every `SettingDefinition` using `ComboBox = new ComboBoxMetadata { ... }` to the new shape. Source of truth for `IsRecommended` / `IsDefault`, in order of precedence:
   - If the setting's current primary `RegistrySetting` has `RecommendedOption` / `DefaultOption` set → use that.
   - Otherwise, use the option index from `Winhance_Recommended_Config.winhance` (`IsRecommended`) and `Winhance_Default_Config_Windows11_25H2.winhance` (`IsDefault`).
   - Otherwise, infer `IsDefault` from the DisplayName containing "(Default)".
   - **Where any two sources disagree for the same setting ID, log a `// MIGRATION-CHECK:` comment inline and list the setting in the PR description for human review.** Do not silently pick one.
4. Update `BulkSettingsActionService` (`GetRecommendedOptionFromSetting` / `GetDefaultOptionFromSetting` plus the dead `ComboBoxOptions` branches at lines 363–380) to read from `ComboBoxMetadata.Options` exclusively.
5. Update `SettingItemViewModel.EvaluateRegistrySetting` for the Selection branch; apply the tiebreak rule for `IsRecommended + IsDefault` intersection.
6. Update Technical Details bindings to resolve Selection recommended/default via `ComboBoxMetadata.Options[i].ValueMappings[entry.ValueName]`. Preserve the null-vs-absent distinction: a `null` *entry* in `ValueMappings` renders as "— (key deleted)"; an *absent* key renders as "unchanged".
7. Delete the `[Obsolete]` fields (`DisplayNames`, `ValueMappings`, `SimpleValueMappings`, `CommandValueMappings`, `ScriptMappings`, `OptionTooltips`, `OptionWarnings`, `OptionConfirmations`, `ScriptVariables`). Delete `RegistrySetting.RecommendedOption`, `DefaultOption`, `ComboBoxOptions`. Delete the single live use of `RecommendedOption` in `StartMenuCustomizations.cs`.
8. Add the `SettingCatalogValidatorTests` unit test covering every `SettingDefinition` in the catalog.
9. Run the full test suite. Regenerate the golden snapshot; diff against step 1's baseline and attach to the PR — zero Recommended/Default changes expected for any setting ID.

### Phase B — Visual layer

10. Add the NumericRange quick-set button controls in the relevant XAML SettingsCard templates. Wire to `RegistrySetting.RecommendedValue` / `DefaultValue` (and `PowerCfgSetting.RecommendedValueAC/DC` / `DefaultValueAC/DC` for Separate). Verify layout at 1024px window width.
11. Add the open-ComboBox-dropdown option highlighting via `ComboBox.ItemTemplate` (ensuring `SelectionBoxItemTemplate` stays plain). Bind visibility to `UserPreferences.ShowInfoBadges`.
12. Add localized strings for the three new tooltips: "Set to Recommended (…)", "Set to Default (…)", "Recommended (also Windows default)". Follow the pattern from `docs/superpowers/plans/2026-04-12-modern-infobadge-pills.md` — add to all 27 language files via parallel subagents.
13. Run UI smoke tests from the Testing section.

### ScriptVariables stale-entry policy

If the existing `ComboBoxMetadata.ScriptVariables` dictionary contains keys for option indices that don't exist in `DisplayNames` (stale entries left behind from option removals), migration **fails loudly** — a `MIGRATION-FAIL:` comment is added inline and the setting is listed in the PR description for human review. Silent drop is not acceptable.

### Affected files (estimate)

- Model: 3 files (`RegistrySetting.cs`, `ComboBoxMetadata.cs`, new `ComboBoxOption.cs`).
- SettingDefinition sources using ComboBox: ~10 files across `Winhance.Core/Features/{Optimize,Customize}/Models/*`.
- Service consumers: `BulkSettingsActionService.cs`, `ComboBoxResolver.cs`, config import/export code. Exact call sites identified during the implementation plan via `[Obsolete]` compile errors.
- ViewModels: `SettingItemViewModel.cs` (badge computation + Technical Details binding).
- Views / XAML: `SettingTemplates.xaml` (ComboBox dropdown template), NumericRange spinner template, any `BadgeStyles.xaml` additions for the highlight styles.
- Tests: existing unit tests using `ComboBoxMetadata` or `RegistrySetting.RecommendedOption` need mechanical updates; add the new catalog-validator test.

## Testing

- **Golden snapshot (regression baseline):** before Phase A starts, dump `{settingId → (resolvedRecommendedIndex or value, resolvedDefaultIndex or value)}` for every `SettingDefinition` in the catalog, using the *current* resolution logic. Save as `tests/Winhance.UI.Tests/Fixtures/setting-state-baseline.json`. After Phase A, regenerate and diff — zero changes expected. The diff goes in the PR description. This is the highest-value test for a big-bang rename.
- **Catalog validator test (`SettingCatalogValidatorTests`):** iterate every registered `SettingDefinition`; assert the invariants listed under `ComboBoxMetadata` validation plus the Selection-entries-must-be-null rule. Fails CI if any setting has gaps.
- **`BulkSettingsActionServiceTests` additions:**
  - Apply Recommended for a Selection setting → verify the `IsRecommended` option's `ValueMappings` are written and nothing else.
  - Reset to Defaults for a Selection setting → verify `IsDefault` option's `ValueMappings` are written.
  - **Round-trip test:** apply Recommended, read back the effective `SelectedIndex` via `ComputeBadgeState`, assert `BadgeState == Recommended`. Same for Default.
  - Test a ComboBox setting with `SupportsCustomState = true` — validator does not fail, Apply Recommended still works.
- **`SettingItemViewModelTests` additions:**
  - Badge state for Selection maps `SelectedIndex == recommendedIndex` to `Recommended`, same for `Default`, everything else to `Custom`.
  - Tiebreak test: option that is both `IsRecommended` and `IsDefault`, when selected, yields `BadgeState == Recommended`.
- **Config import compatibility test:** round-trip a pre-migration `.winhance` file (a pinned copy in `tests/Winhance.UI.Tests/Fixtures/`) through the post-migration importer; assert every Selection setting resolves to the correct option index.
- **UI tests (Phase B):**
  - Closed ComboBox dropdown for a Recommended-selected option renders no pill background (guard against the `SelectionBoxItemTemplate` hazard).
  - Open ComboBox dropdown renders green pill on Recommended option and grey on Default when `ShowInfoBadges = true`; plain when `false`.
  - NumericRange green star button click → value becomes `RecommendedValue`; grey reset button click → value becomes `DefaultValue`.
  - AC quick-set on a Separate PowerCfg setting sets only AC; DC value unchanged.
- **Manual smoke test:**
  - On Optimize → Update Policy, confirm ComboBox dropdown highlights "Security Updates Only (Recommended)" in green and "Normal (Windows Default)" in grey.
  - Click Apply Recommended in Quick Actions; verify every registry entry in Technical Details shows the right post-apply value and the `null`-vs-absent distinction displays correctly.
  - Click Reset to Windows Defaults; verify badges return to Default state.
  - On a NumericRange setting (e.g. Maximum processor state), click the green star quick-set button; verify value jumps to 100 and badge flips to Recommended.
  - Verify card layout at 1024px window width for the busiest NumericRange settings (PowerCfg Separate sliders).

## Out-of-scope follow-ups

- Applying the same option-record pattern to other places that use parallel dictionaries (e.g. any other metadata type) — addressed only if found during this work.
- Localizing the option tooltips added via `ComboBoxOption.Tooltip` (current codebase already stores display strings literally in these files; no regression).

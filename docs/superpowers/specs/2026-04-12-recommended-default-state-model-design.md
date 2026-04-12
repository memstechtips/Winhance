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

**Validation (runtime, at app startup + unit test):**
- Every `ComboBoxMetadata` has **exactly one** option with `IsDefault = true`.
- Every `ComboBoxMetadata` has **at most one** option with `IsRecommended = true`. (At most, because some purely informational ComboBoxes — e.g. "pick a DNS provider" — have no clear "recommended" option.)
- No duplicate `DisplayName` within a single `Options` list.
- Each option sets at most one of `ValueMappings` / `SimpleValue` / `CommandValue` / `Script` (mutually exclusive by current usage).

Validation failures throw at app startup (fail fast) and are caught by a unit test iterating every SettingDefinition in the catalog.

### `RegistrySetting` (changed)

Remove `RecommendedOption` and `DefaultOption` string properties entirely. Their function moves to `ComboBoxOption.IsRecommended`/`IsDefault`.

`RecommendedValue` and `DefaultValue` stay as `required object?`. Their semantics per InputType:

| InputType | `RegistrySetting.RecommendedValue` | `RegistrySetting.DefaultValue` |
|---|---|---|
| Toggle, CheckBox | Non-null. Must match `EnabledValue[0]` or `DisabledValue[0]`. | Non-null. Must match `EnabledValue[0]` or `DisabledValue[0]`. |
| NumericRange | Non-null integer. | Non-null integer (or `null` if Windows' default is "key absent"). |
| Selection | `null` — resolved via `ComboBoxMetadata.Options[i].ValueMappings`. | `null` — same. |
| Action (no badge) | `null`. | `null`. |

The `null` cases for Selection are explicit because `required` forces authors to write them — a deliberate acknowledgment that "this registry entry's recommendation is decided elsewhere", not a silent omission.

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

No visual change. The InfoBadge on the card communicates state. Toggling flips to the other state. Only two positions exist; "Custom" only appears when the registry has a stray third value, and toggling once writes back to `EnabledValue[0]` or `DisabledValue[0]`, resolving it.

### Selection (ComboBox)

When the dropdown is open **and** `UserPreferences.ShowInfoBadges == true`:
- Option marked `IsRecommended`: green pill background, same palette as the Recommended badge.
- Option marked `IsDefault`: grey pill background, same palette as the Default badge.
- If one option is both (rare but allowed in the data): Recommended wins visually; tooltip reads "Recommended (also Windows default)".
- Hover tooltip on a highlighted option: "Recommended" / "Windows Default" (localized).

When the dropdown is closed: no change — the card's InfoBadge alone communicates state.

### NumericRange (up/down spinner)

Two small quick-set buttons placed next to the spinner's up/down arrows:
- A green star icon — "Set to Recommended" — jumps the value to `RegistrySetting.RecommendedValue`.
- A grey reset icon — "Set to Default" — jumps the value to `RegistrySetting.DefaultValue`.
- Hover tooltip shows the target value, e.g. "Set to Recommended (100)".
- Icons only visible when `UserPreferences.ShowInfoBadges == true` (same global toggle as the card badges).
- The icons match the glyphs already used in the Quick Actions dropdown and the card badges, for consistency across the three surfaces.

For PowerCfg NumericRange settings with `PowerModeSupport.Separate`, each of the AC / DC spinners gets its own pair of quick-set buttons, backed by `PowerCfgSetting.RecommendedValueAC/DC` and `DefaultValueAC/DC`.

---

## Migration strategy

Single PR, big-bang migration. Mechanical per-file passes with parallel subagents.

### Order of operations

1. Add `ComboBoxOption` record. Change `ComboBoxMetadata` to the new shape. Keep old fields temporarily marked `[Obsolete]` so the compiler surfaces every callsite.
2. Migrate every `SettingDefinition` using `ComboBox = new ComboBoxMetadata { ... }` to the new shape. Set `IsRecommended = true` on the option that matches `Winhance_Recommended_Config.winhance`'s `SelectedIndex` for that setting ID. Set `IsDefault = true` on the option whose DisplayName contains "(Default)" (or, where absent, the one matching `Winhance_Default_Config_Windows11_25H2.winhance`'s `SelectedIndex`).
3. Remove the `[Obsolete]` fields. Delete `RegistrySetting.RecommendedOption` and `DefaultOption` properties. Delete their single live use in `StartMenuCustomizations.cs`.
4. Update `BulkSettingsActionService.GetRecommendedOptionFromSetting` / `GetDefaultOptionFromSetting` to read from `ComboBoxMetadata.Options` instead.
5. Update `SettingItemViewModel.EvaluateRegistrySetting` for the Selection branch.
6. Update Technical Details binding to resolve Selection recommended/default via `ComboBoxMetadata.Options`.
7. Add the runtime validator + unit test covering every `SettingDefinition` in the catalog.
8. Add the NumericRange quick-set button controls in the relevant XAML SettingsCard templates.
9. Add the open-ComboBox-dropdown option highlighting (style + converter or template selector).
10. Run the full test suite; fix any fallout.

### Affected files (estimate)

- Model: 3 files (`RegistrySetting.cs`, `ComboBoxMetadata.cs`, new `ComboBoxOption.cs`).
- SettingDefinition sources using ComboBox: ~10 files across `Winhance.Core/Features/{Optimize,Customize}/Models/*`.
- Service consumers: `BulkSettingsActionService.cs`, `ComboBoxResolver.cs`, config import/export code. Exact call sites identified during the implementation plan via `[Obsolete]` compile errors.
- ViewModels: `SettingItemViewModel.cs` (badge computation + Technical Details binding).
- Views / XAML: `SettingTemplates.xaml` (ComboBox dropdown template), NumericRange spinner template, any `BadgeStyles.xaml` additions for the highlight styles.
- Tests: existing unit tests using `ComboBoxMetadata` or `RegistrySetting.RecommendedOption` need mechanical updates; add the new catalog-validator test.

## Testing

- **Unit tests per consumer:** `BulkSettingsActionServiceTests` (apply recommended/default for Selection using the new model), `SettingItemViewModelTests` (badge state for Selection).
- **Catalog validator test:** iterate every registered `SettingDefinition`; assert the per-InputType invariants. Fails CI if any setting has gaps.
- **Manual smoke test:**
  - On Optimize → Update Policy, confirm ComboBox dropdown highlights "Security Updates Only (Recommended)" in green and "Normal (Windows Default)" in grey.
  - Click Apply Recommended in Quick Actions; verify every registry entry in Technical Details shows the right post-apply value.
  - Click Reset to Windows Defaults; verify badges return to Default state.
  - On a NumericRange setting (e.g. Maximum processor state), click the green star quick-set button; verify value jumps to 100 and badge flips to Recommended.

## Out-of-scope follow-ups

- Applying the same option-record pattern to other places that use parallel dictionaries (e.g. any other metadata type) — addressed only if found during this work.
- Localizing the option tooltips added via `ComboBoxOption.Tooltip` (current codebase already stores display strings literally in these files; no regression).

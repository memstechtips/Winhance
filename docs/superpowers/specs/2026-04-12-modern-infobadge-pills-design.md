# Modern InfoBadge Pills — Design

**Date:** 2026-04-12
**Related issues:** #434, #467
**Status:** Approved, ready for implementation plan
**Supersedes visual portion of:** [2026-04-11 Quick Actions + View + InfoBadges plan](../../plans/2026-04-11-quick-actions-view-infobadges.md) (Part 2 / polish)

---

## Goal

Replace the current icon-only dot-style `InfoBadge` on `SettingsCard` headers with a modern **pill-style badge** that displays both an icon and a text label ("Recommended" / "Default" / "Custom"), with a descriptive tooltip on hover. Centralize the badge icons so the icon rendered on the badge matches the icon rendered in the Quick Actions dropdown menu.

## Reference

Visual target: `C:\Winhance\BadgeSettingsCards.xaml` (general design reference, not codebase-specific).

Screenshot attached to the request shows three pill badges: green "Recommended", neutral "Default", amber "Custom".

## Current State

- `src/Winhance.UI/Features/Common/Resources/SettingTemplates.xaml:81-92` — single `<Border>` with a 10px `FontIcon` glyph, no text label.
- `BadgeStateToColorConverter` maps `SettingBadgeState` enum → solid `SolidColorBrush` (basic green/gray/orange).
- Icons are hardcoded Segoe MDL2 glyphs in both the badge (`BadgeIcon` property on `SettingItemViewModel`) and in the Quick Actions `MenuFlyoutItem.Icon` blocks on `OptimizePage.xaml:170-191` and `CustomizePage.xaml:157-168` (duplicated — not shared).
- Tooltip strings are single-word (`InfoBadge_Recommended` = "Recommended", etc.).

## Design

### 1. Icon centralization in FeatureIcons.xaml

Add three new keys to `src/Winhance.UI/Features/Common/Resources/FeatureIcons.xaml`:

```xaml
<!-- ==================== InfoBadge Icons ==================== -->
<!-- Used on SettingsCard InfoBadges AND on Quick Actions menu items.
     Icons must stay in sync across both surfaces. -->

<!-- StarCheckmark (FluentIcons) — Recommended state + Apply Recommended menu item -->
<x:String x:Key="BadgeRecommendedIconSymbol">StarCheckmark</x:String>

<!-- PersonWrench (FluentIcons) — Custom state (no Quick Actions counterpart) -->
<x:String x:Key="BadgeCustomIconSymbol">PersonWrench</x:String>
```

The existing `WindowsLogoIconPath` key is reused for the Default state (no new key added).

Rendering pattern per icon type:
- `StarCheckmark` / `PersonWrench` → `<fi:SymbolIcon Symbol="..."/>` (FluentIcons.WinUI)
- `WindowsLogoIconPath` → `<PathIcon Data="{StaticResource WindowsLogoIconPath}"/>`

### 2. Badge styles in dedicated ResourceDictionary

New file: `src/Winhance.UI/Features/Common/Resources/BadgeStyles.xaml`

Contents (adapted from `BadgeSettingsCards.xaml` reference, using alpha-blended theme-aware colors):

- `BadgePillBase` (Border) — `CornerRadius=999`, `Padding=7,3,10,3`, `BorderThickness=1`, `VerticalAlignment=Center`
- `BadgeRecommendedStyle` — green tinted background `#224EC94E`, border `#404EC94E`
- `BadgeDefaultStyle` — neutral background `#12FFFFFF`, border `#24FFFFFF`
- `BadgeCustomStyle` — amber background `#22F0A030`, border `#40F0A030`
- `BadgeRecommendedForeground` — `SolidColorBrush #FF4EC94E` (full-opacity green)
- `BadgeDefaultForeground` — `SolidColorBrush #99FFFFFF` (off-white)
- `BadgeCustomForeground` — `SolidColorBrush #FFF0A030` (full-opacity amber)
- `BadgeTextStyle` (TextBlock) — `FontSize=11`, `FontWeight=SemiBold`, `VerticalAlignment=Center`
- `BadgeIconStyle` (FontIcon/SymbolIcon/PathIcon — applied individually) — `FontSize=11`, `VerticalAlignment=Center`, `Margin=0,0,4,0`

Register this dictionary via `App.xaml`'s merged dictionaries so it is globally available.

### 3. Converters and selectors

Three new files in `src/Winhance.UI/Features/Common/Converters/`:

**`BadgeStateToStyleConverter.cs`** (replaces `BadgeStateToColorConverter`)
- `Convert(SettingBadgeState → Style)` — looks up one of `BadgeRecommendedStyle` / `BadgeDefaultStyle` / `BadgeCustomStyle` from `Application.Current.Resources`.

**`BadgeStateToForegroundConverter.cs`**
- `Convert(SettingBadgeState → Brush)` — looks up one of `BadgeRecommendedForeground` / `BadgeDefaultForeground` / `BadgeCustomForeground`.

**`BadgeIconTemplateSelector.cs`** (DataTemplateSelector)
- Three DataTemplate dependency properties: `RecommendedTemplate`, `DefaultTemplate`, `CustomTemplate`
- `SelectTemplateCore(object item, ...)` returns the matching template based on the `SettingBadgeState` content value.
- Registered as a global resource in `BadgeStyles.xaml` with its three DataTemplate DPs set inline at resource-declaration time:
  ```xaml
  <conv:BadgeIconTemplateSelector x:Key="BadgeIconTemplateSelector">
      <conv:BadgeIconTemplateSelector.RecommendedTemplate>
          <DataTemplate>
              <fi:SymbolIcon Symbol="StarCheckmark" ... Foreground="{StaticResource BadgeRecommendedForeground}"/>
          </DataTemplate>
      </conv:BadgeIconTemplateSelector.RecommendedTemplate>
      <conv:BadgeIconTemplateSelector.DefaultTemplate>
          <DataTemplate>
              <PathIcon Data="{StaticResource WindowsLogoIconPath}" ... Foreground="{StaticResource BadgeDefaultForeground}"/>
          </DataTemplate>
      </conv:BadgeIconTemplateSelector.DefaultTemplate>
      <conv:BadgeIconTemplateSelector.CustomTemplate>
          <DataTemplate>
              <fi:SymbolIcon Symbol="PersonWrench" ... Foreground="{StaticResource BadgeCustomForeground}"/>
          </DataTemplate>
      </conv:BadgeIconTemplateSelector.CustomTemplate>
  </conv:BadgeIconTemplateSelector>
  ```
  Consumers reference it once via `ContentTemplateSelector="{StaticResource BadgeIconTemplateSelector}"` — no per-usage template wiring needed.

Delete the old `BadgeStateToColorConverter.cs` and remove its registration from `App.xaml` / merged converter dictionaries.

### 4. Badge XAML in SettingTemplates.xaml

Replace the current InfoBadge Border (`SettingTemplates.xaml:81-92`) with a single reactive Border:

```xaml
<Border
    Style="{x:Bind BadgeState, Mode=OneWay, Converter={StaticResource BadgeStateToStyleConverter}}"
    Visibility="{x:Bind ShowInfoBadge, Mode=OneWay, Converter={StaticResource BoolToVisibilityConverter}}"
    ToolTipService.ToolTip="{x:Bind BadgeTooltip, Mode=OneWay}">
    <StackPanel Orientation="Horizontal">
        <ContentControl
            Content="{x:Bind BadgeState, Mode=OneWay}"
            ContentTemplateSelector="{StaticResource BadgeIconTemplateSelector}"/>
        <TextBlock
            Text="{x:Bind BadgeLabel, Mode=OneWay}"
            Foreground="{x:Bind BadgeState, Mode=OneWay, Converter={StaticResource BadgeStateToForegroundConverter}}"
            Style="{StaticResource BadgeTextStyle}"/>
    </StackPanel>
</Border>
```

**Reactivity:** When `BadgeState` changes on the ViewModel, `PropertyChanged` fires. All `{x:Bind, Mode=OneWay}` bindings re-resolve:
- `Style` → new pill style
- Icon `ContentControl` → selector re-picks DataTemplate → new icon control materializes
- `TextBlock.Text` → new localized label
- `TextBlock.Foreground` → new foreground brush
- `ToolTipService.ToolTip` → new localized tooltip

Header StackPanel ordering stays: `Name → NEW badge → InfoBadge`. The NEW badge is untouched.

### 5. ViewModel changes

`src/Winhance.UI/Features/Optimize/ViewModels/SettingItemViewModel.cs`:

**Remove:**
- `BadgeIcon` property (lines ~215-221) — icon is now selected by `BadgeIconTemplateSelector`, not driven by a glyph string.

**Keep (unchanged):**
- `BadgeState` (existing)
- `ShowInfoBadge` (existing)
- `BadgeTooltip` (update body — see below)

**Add:**
- `BadgeLabel` (string) — returns the localized short label for the current `BadgeState`:
  - `Recommended` → `_localizationService.GetString("InfoBadge_Recommended")`
  - `Default` → `_localizationService.GetString("InfoBadge_Default")`
  - `Custom` → `_localizationService.GetString("InfoBadge_Custom")`

**Update `BadgeTooltip`:** point to the new longer-form keys:
- `Recommended` → `InfoBadge_Recommended_Tooltip`
- `Default` → `InfoBadge_Default_Tooltip`
- `Custom` → `InfoBadge_Custom_Tooltip`

**PropertyChanged propagation:** whenever `BadgeState` changes, raise `PropertyChanged` for `BadgeLabel` and `BadgeTooltip` (and keep existing notifications for anything else). When the localization language changes, raise `PropertyChanged` for both as well (existing pattern in codebase — mirror it).

### 6. Quick Actions dropdown icon update

`src/Winhance.UI/Features/Optimize/OptimizePage.xaml` (lines ~170-191) and `src/Winhance.UI/Features/Customize/CustomizePage.xaml` (lines ~157-168):

Replace:
```xaml
<MenuFlyoutItem.Icon>
    <FontIcon Glyph="&#xE735;"/>
</MenuFlyoutItem.Icon>
```

With (for Apply Recommended):
```xaml
<MenuFlyoutItem.Icon>
    <fi:SymbolIcon Symbol="StarCheckmark"/>
</MenuFlyoutItem.Icon>
```

And for Reset Defaults — replace `FontIcon Glyph="&#xE777;"` with:
```xaml
<MenuFlyoutItem.Icon>
    <PathIcon Data="{StaticResource WindowsLogoIconPath}"/>
</MenuFlyoutItem.Icon>
```

This ensures icon parity between the Quick Actions menu items and the corresponding InfoBadge states.

Ensure the `fi:` XML namespace (`xmlns:fi="using:FluentIcons.WinUI"`) is present in both page XAML files — verify during implementation.

### 7. Localization

**Existing keys retained unchanged** (used for in-badge labels):
- `InfoBadge_Recommended`
- `InfoBadge_Default`
- `InfoBadge_Custom`

**New keys to add** to all 27 localization JSON files in `src/Winhance.UI/Features/Common/Localization/`:
- `InfoBadge_Recommended_Tooltip` = "The Recommended values are applied for this setting"
- `InfoBadge_Default_Tooltip` = "The Default Windows values are applied for this setting"
- `InfoBadge_Custom_Tooltip` = "Custom values are applied for this setting"

All three keys must be translated into the native language of each of the 26 non-English files (de, fr, es, it, pt, pt-BR, nl, nl-BE, pl, sv, cs, hu, tr, el, af, vi, ru, uk, hi, ar, ja, ko, zh-Hans, zh-Hant, lv, lt).

**Constraint (per user feedback memory):** editing of localization JSON files MUST be done via the `Edit` tool or by dispatching subagents — NEVER via `sed`, `bash`, or any other script-based transformation. This constraint is already captured in the project's memory (`feedback_localization_files.md`).

Also add `InfoBadge_Recommended`, `InfoBadge_Default`, `InfoBadge_Custom`, and the three `_Tooltip` keys as `StringKeys` constants in `src/Winhance.UI/Features/Common/Localization/StringKeys.cs` if not already present (check during implementation — at least three existing keys already exist, only the `_Tooltip` variants will be new).

### 8. Cleanup

- Delete `src/Winhance.UI/Features/Common/Converters/BadgeStateToColorConverter.cs`
- Remove its registration in `App.xaml`'s merged converter dictionaries (if present)
- Grep for any other references to `BadgeStateToColorConverter` and remove them
- Grep for references to `BadgeIcon` property on `SettingItemViewModel` — remove any stray bindings

## Architecture Summary

```
FeatureIcons.xaml                                    BadgeStyles.xaml
├── WindowsLogoIconPath (reused)                     ├── BadgePillBase
├── BadgeRecommendedIconSymbol = "StarCheckmark"     ├── BadgeRecommendedStyle
└── BadgeCustomIconSymbol = "PersonWrench"           ├── BadgeDefaultStyle
                                                     ├── BadgeCustomStyle
                                                     ├── BadgeRecommendedForeground
                                                     ├── BadgeDefaultForeground
                                                     ├── BadgeCustomForeground
                                                     ├── BadgeTextStyle
                                                     ├── BadgeIconStyle
                                                     └── DataTemplates (Recommended/Default/Custom icon)
          │                                                    │
          │ referenced by                                      │
          ▼                                                    ▼
  Quick Actions menu items                  SettingTemplates.xaml → <Border>
  (OptimizePage.xaml, CustomizePage.xaml)           │
                                                    │ {x:Bind BadgeState, ...}
                                                    ▼
                                            SettingItemViewModel
                                            ├── BadgeState (existing)
                                            ├── ShowInfoBadge (existing)
                                            ├── BadgeLabel (NEW)
                                            └── BadgeTooltip (updated keys)
                                                    │
                                                    │ reads from
                                                    ▼
                                            LocalizationService
                                            (27 JSON files)
```

## Testing

- **Build:** `Winhance.UI.Tests.csproj` via MSBuild (x64, Debug) — see `CLAUDE.md` memory for exact command. Existing test suite must still pass (1300 tests, per recent history).
- **No new unit tests** required: the changes are rendering-only and ViewModel additions are trivial derived properties. Badge state computation logic is unchanged and already tested.
- **Manual visual verification** in the running app:
  - Open OptimizePage → verify each state renders (toggle settings to trigger Recommended / Default / Custom states)
  - Hover each badge → verify localized tooltip shows
  - Open Quick Actions dropdown → verify icons match the badge icons
  - Switch languages → verify label + tooltip translate live
  - Toggle View → InfoBadges → verify show/hide still works

## Out of Scope

- NEW badge styling (existing red pill, unchanged)
- Overview-page section-level badges (`OptimizePage.xaml` `PrivacyBadge`, `PowerBadge`, etc.) — these use the WinUI `InfoBadge` control for section change counts, an entirely different purpose, untouched
- Flyout sidebar badges — same rationale
- Any logic changes to `ComputeBadgeState()` or `BulkSettingsActionService`

## Risks / Considerations

- **FluentIcons.WinUI namespace:** Confirm `xmlns:fi="using:FluentIcons.WinUI"` is declared wherever `fi:SymbolIcon` is used (SettingTemplates.xaml, OptimizePage.xaml, CustomizePage.xaml, BadgeStyles.xaml). If not, add it.
- **`StarCheckmark` and `PersonWrench` availability:** Confirmed — FluentIcons.WinUI 1.1.271 is referenced in `Winhance.UI.csproj`. If either symbol is missing from this version, the fallback is to bump the package version or source the path data manually.
- **DataTemplate wiring:** Templates are set on the selector instance via its DPs at resource-declaration time in `BadgeStyles.xaml` (see §3). Consumers reference the selector by key only — no per-usage template configuration.
- **Localization JSON files:** Must NEVER be edited via scripts (bash/sed/awk). Use the `Edit` tool directly or dispatch subagents with `Edit`-tool instructions. Parallel subagents are acceptable and encouraged for speed.
- **Theme compatibility:** Alpha-blended colors (`#22`, `#40`, `#12`, `#24` prefixes) give dark-theme appearance; if light theme support is desired, evaluate whether colors need theme-resource variants. Current Winhance is dark-only per observed defaults — acceptable.

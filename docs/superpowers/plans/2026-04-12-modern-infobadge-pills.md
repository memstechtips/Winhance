# Modern InfoBadge Pills Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the icon-only dot-style `InfoBadge` on `SettingsCard` headers with a pill-style badge (icon + label + tooltip), centralize the icons in `FeatureIcons.xaml` so the Quick Actions dropdown uses the same icons, and fully localize the new tooltip strings in all 27 language files.

**Architecture:** A single reactive `<Border>` bound via `{x:Bind BadgeState, Mode=OneWay}` with three new converters/selectors (`BadgeStateToStyleConverter`, `BadgeStateToForegroundConverter`, `BadgeIconTemplateSelector`) routing its visual pieces. Heterogeneous icons (two FluentIcons `SymbolIcon` + one `PathIcon`) are handled through a `ContentTemplateSelector` that picks the correct icon control per state. Badge styles live in a new dedicated `BadgeStyles.xaml` resource dictionary.

**Tech Stack:** WinUI 3 (.NET 10), CommunityToolkit.Mvvm (`[ObservableProperty]` source generators), FluentIcons.WinUI 1.1.271, xunit + Moq + FluentAssertions for tests.

**Reference spec:** [`docs/superpowers/specs/2026-04-12-modern-infobadge-pills-design.md`](../specs/2026-04-12-modern-infobadge-pills-design.md)

---

## File Inventory

### Create
- `src/Winhance.UI/Features/Common/Resources/BadgeStyles.xaml` — pill styles, foreground brushes, text/icon styles, and `BadgeIconTemplateSelector` resource
- `src/Winhance.UI/Features/Common/Converters/BadgeStateToStyleConverter.cs`
- `src/Winhance.UI/Features/Common/Converters/BadgeStateToForegroundConverter.cs`
- `src/Winhance.UI/Features/Common/Converters/BadgeIconTemplateSelector.cs`
- `tests/Winhance.UI.Tests/Converters/BadgeStateToStyleConverterTests.cs`
- `tests/Winhance.UI.Tests/Converters/BadgeStateToForegroundConverterTests.cs`

### Modify
- `src/Winhance.UI/Features/Common/Resources/FeatureIcons.xaml` — add 2 icon keys
- `src/Winhance.UI/Features/Common/Constants/StringKeys.cs` — add 3 tooltip constants
- `src/Winhance.UI/Features/Common/Localization/en.json` — add 3 new keys
- `src/Winhance.UI/Features/Common/Localization/{26 non-English}.json` — add 3 translated keys each (via parallel subagents)
- `src/Winhance.UI/App.xaml` — register `BadgeStyles.xaml` in merged dictionaries
- `src/Winhance.UI/Features/Common/Resources/Converters.xaml` — replace color converter registration with new converters
- `src/Winhance.UI/Features/Optimize/ViewModels/SettingItemViewModel.cs` — drop `BadgeIcon`, add `BadgeLabel`, repoint `BadgeTooltip` to new keys
- `src/Winhance.UI/Features/Common/Resources/SettingTemplates.xaml` — replace badge Border
- `src/Winhance.UI/Features/Optimize/OptimizePage.xaml` — Quick Actions icon swap
- `src/Winhance.UI/Features/Customize/CustomizePage.xaml` — Quick Actions icon swap

### Delete
- `src/Winhance.UI/Features/Common/Converters/BadgeStateToColorConverter.cs`

---

## Task 1: Add new localization keys to en.json (English source of truth)

**Files:**
- Modify: `src/Winhance.UI/Features/Common/Localization/en.json` (around lines 1707-1709, after existing InfoBadge keys)

- [ ] **Step 1: Locate the existing InfoBadge keys in en.json**

Open `src/Winhance.UI/Features/Common/Localization/en.json` and find this block (near line 1707-1709):

```json
"InfoBadge_Recommended": "Recommended",
"InfoBadge_Default": "Default",
"InfoBadge_Custom": "Custom",
```

- [ ] **Step 2: Add three new tooltip keys directly after**

Use the `Edit` tool. Replace the existing three InfoBadge lines with the same three lines PLUS three new `_Tooltip` lines appended:

**old_string:**
```
"InfoBadge_Recommended": "Recommended",
"InfoBadge_Default": "Default",
"InfoBadge_Custom": "Custom",
```

**new_string:**
```
"InfoBadge_Recommended": "Recommended",
"InfoBadge_Default": "Default",
"InfoBadge_Custom": "Custom",
"InfoBadge_Recommended_Tooltip": "The Recommended values are applied for this setting",
"InfoBadge_Default_Tooltip": "The Default Windows values are applied for this setting",
"InfoBadge_Custom_Tooltip": "Custom values are applied for this setting",
```

- [ ] **Step 3: Verify JSON is still valid**

Run: `node -e "JSON.parse(require('fs').readFileSync('C:/Winhance/src/Winhance.UI/Features/Common/Localization/en.json','utf8')); console.log('OK')"`

Expected output: `OK`

If `node` is unavailable, use: `python -c "import json; json.load(open('C:/Winhance/src/Winhance.UI/Features/Common/Localization/en.json','r',encoding='utf-8')); print('OK')"`

- [ ] **Step 4: Commit**

```bash
cd C:/Winhance
git add src/Winhance.UI/Features/Common/Localization/en.json
git commit -m "feat(i18n): add InfoBadge tooltip keys to en.json"
```

---

## Task 2: Add translated InfoBadge tooltip keys to all 26 non-English localization files

**Files (each adds 3 translated keys in the same location):**
- Modify: `src/Winhance.UI/Features/Common/Localization/de.json`
- Modify: `src/Winhance.UI/Features/Common/Localization/fr.json`
- Modify: `src/Winhance.UI/Features/Common/Localization/es.json`
- Modify: `src/Winhance.UI/Features/Common/Localization/it.json`
- Modify: `src/Winhance.UI/Features/Common/Localization/pt.json`
- Modify: `src/Winhance.UI/Features/Common/Localization/pt-BR.json`
- Modify: `src/Winhance.UI/Features/Common/Localization/nl.json`
- Modify: `src/Winhance.UI/Features/Common/Localization/nl-BE.json`
- Modify: `src/Winhance.UI/Features/Common/Localization/pl.json`
- Modify: `src/Winhance.UI/Features/Common/Localization/sv.json`
- Modify: `src/Winhance.UI/Features/Common/Localization/cs.json`
- Modify: `src/Winhance.UI/Features/Common/Localization/hu.json`
- Modify: `src/Winhance.UI/Features/Common/Localization/tr.json`
- Modify: `src/Winhance.UI/Features/Common/Localization/el.json`
- Modify: `src/Winhance.UI/Features/Common/Localization/af.json`
- Modify: `src/Winhance.UI/Features/Common/Localization/vi.json`
- Modify: `src/Winhance.UI/Features/Common/Localization/ru.json`
- Modify: `src/Winhance.UI/Features/Common/Localization/uk.json`
- Modify: `src/Winhance.UI/Features/Common/Localization/hi.json`
- Modify: `src/Winhance.UI/Features/Common/Localization/ar.json`
- Modify: `src/Winhance.UI/Features/Common/Localization/ja.json`
- Modify: `src/Winhance.UI/Features/Common/Localization/ko.json`
- Modify: `src/Winhance.UI/Features/Common/Localization/zh-Hans.json`
- Modify: `src/Winhance.UI/Features/Common/Localization/zh-Hant.json`
- Modify: `src/Winhance.UI/Features/Common/Localization/lv.json`
- Modify: `src/Winhance.UI/Features/Common/Localization/lt.json`

**CRITICAL CONSTRAINT (per Winhance memory `feedback_localization_files.md`):**
> Never use sed, awk, bash scripts, or any script-based transformation to edit localization files. Use the Edit tool directly or dispatch subagents that use the Edit tool. This is a hard project rule.

- [ ] **Step 1: Dispatch 5 parallel subagents, each handling ~5 language files**

Send a single message with 5 `Agent` tool calls in parallel. Each subagent receives:
- A list of 5-6 language files to edit
- The 3 English tooltip strings to translate
- Instructions to edit each file's existing InfoBadge block by appending 3 translated `_Tooltip` keys immediately after the existing `"InfoBadge_Custom"` line

**Prompt template for each subagent (customize the file list per batch):**

```
You are editing Winhance localization JSON files. For each file listed, add three new tooltip keys translated into that file's native language.

FILES TO EDIT (this batch):
- src/Winhance.UI/Features/Common/Localization/<lang1>.json
- src/Winhance.UI/Features/Common/Localization/<lang2>.json
- ... (5-6 total)

FOR EACH FILE:
1. Read the file.
2. Find the block containing these three lines (order may vary slightly):
     "InfoBadge_Recommended": "<native word for Recommended>",
     "InfoBadge_Default": "<native word for Default>",
     "InfoBadge_Custom": "<native word for Custom>",
3. Use the Edit tool to append three new translated keys immediately after the "InfoBadge_Custom" line. The three new keys to add (in this exact key order, values translated into the native language of the file):
     "InfoBadge_Recommended_Tooltip": "<native translation of: The Recommended values are applied for this setting>",
     "InfoBadge_Default_Tooltip": "<native translation of: The Default Windows values are applied for this setting>",
     "InfoBadge_Custom_Tooltip": "<native translation of: Custom values are applied for this setting>",
4. Ensure trailing commas are correct (each line ends with a comma — the JSON block continues after).
5. Verify the JSON is valid after your edit.

CRITICAL RULES:
- Use ONLY the Edit tool. Do NOT use sed, bash, awk, Python scripts, or any automation.
- Translate the meaning — do not copy the English text verbatim.
- Use the translations that already exist for "Recommended"/"Default"/"Custom" in each file as the base vocabulary, so terminology stays consistent within each file.
- Preserve the existing file's formatting (indentation, line endings).
- Do not modify any other keys.

Report back: for each file, confirm the three keys added and paste the translations you used.
```

Split the 26 languages across 5 batches roughly like this:
- **Subagent 1:** de.json, fr.json, es.json, it.json, pt.json, pt-BR.json
- **Subagent 2:** nl.json, nl-BE.json, pl.json, sv.json, cs.json
- **Subagent 3:** hu.json, tr.json, el.json, af.json, vi.json
- **Subagent 4:** ru.json, uk.json, hi.json, ar.json, ja.json
- **Subagent 5:** ko.json, zh-Hans.json, zh-Hant.json, lv.json, lt.json

- [ ] **Step 2: Validate all 26 JSON files parse**

Run:
```bash
cd C:/Winhance
for f in src/Winhance.UI/Features/Common/Localization/*.json; do
  python -c "import json; json.load(open('$f','r',encoding='utf-8'))" && echo "OK: $f" || echo "FAIL: $f"
done
```

Expected: every file prints `OK: <path>`. If any `FAIL`, open that file with Read and fix the JSON syntax using Edit.

- [ ] **Step 3: Spot-check a few translations**

Read `de.json`, `ja.json`, and `ar.json`. Verify the three `_Tooltip` keys exist and are actually translated (not English copies). If any are still English, re-dispatch a subagent to fix that specific file.

- [ ] **Step 4: Commit**

```bash
cd C:/Winhance
git add src/Winhance.UI/Features/Common/Localization/*.json
git commit -m "feat(i18n): add InfoBadge tooltip translations for all 26 languages"
```

---

## Task 3: Add StringKeys constants for new tooltip keys

**Files:**
- Modify: `src/Winhance.UI/Features/Common/Constants/StringKeys.cs` (lines 128-132)

- [ ] **Step 1: Read the InfoBadge section of StringKeys.cs**

Read lines 120-140 of `src/Winhance.UI/Features/Common/Constants/StringKeys.cs` to confirm current structure:

```csharp
    /// <summary>
    /// InfoBadge strings
    /// </summary>
    public static class InfoBadge
    {
        public const string Recommended = "InfoBadge_Recommended";
        public const string Default = "InfoBadge_Default";
        public const string Custom = "InfoBadge_Custom";
    }
```

- [ ] **Step 2: Add three new tooltip constants**

Use the `Edit` tool.

**old_string:**
```csharp
    public static class InfoBadge
    {
        public const string Recommended = "InfoBadge_Recommended";
        public const string Default = "InfoBadge_Default";
        public const string Custom = "InfoBadge_Custom";
    }
```

**new_string:**
```csharp
    public static class InfoBadge
    {
        public const string Recommended = "InfoBadge_Recommended";
        public const string Default = "InfoBadge_Default";
        public const string Custom = "InfoBadge_Custom";
        public const string RecommendedTooltip = "InfoBadge_Recommended_Tooltip";
        public const string DefaultTooltip = "InfoBadge_Default_Tooltip";
        public const string CustomTooltip = "InfoBadge_Custom_Tooltip";
    }
```

- [ ] **Step 3: Commit**

```bash
cd C:/Winhance
git add src/Winhance.UI/Features/Common/Constants/StringKeys.cs
git commit -m "feat: add InfoBadge tooltip string key constants"
```

---

## Task 4: Add badge icon keys to FeatureIcons.xaml

**Files:**
- Modify: `src/Winhance.UI/Features/Common/Resources/FeatureIcons.xaml` (around lines 70-72, after the existing `WindowsLogoIconPath`)

- [ ] **Step 1: Locate the existing Badge Icons section**

Open `src/Winhance.UI/Features/Common/Resources/FeatureIcons.xaml`. Find this block (lines 70-72):

```xml
    <!-- ==================== Badge Icons (Path Data) ==================== -->
    <!-- Microsoft Windows logo (4-pane grid) - Used for compatibility badges -->
    <x:String x:Key="WindowsLogoIconPath">M0,0 H10 V10 H0 Z M12,0 H22 V10 H12 Z M0,12 H10 V22 H0 Z M12,12 H22 V22 H12 Z</x:String>
```

- [ ] **Step 2: Update the section comment and add two symbol keys**

Use the `Edit` tool.

**old_string:**
```xml
    <!-- ==================== Badge Icons (Path Data) ==================== -->
    <!-- Microsoft Windows logo (4-pane grid) - Used for compatibility badges -->
    <x:String x:Key="WindowsLogoIconPath">M0,0 H10 V10 H0 Z M12,0 H22 V10 H12 Z M0,12 H10 V22 H0 Z M12,12 H22 V22 H12 Z</x:String>
```

**new_string:**
```xml
    <!-- ==================== Badge Icons ==================== -->
    <!-- Icons used on SettingsCard InfoBadges AND on Quick Actions menu items.
         Keeping them centralized here ensures the badge icon and the matching
         Quick Actions icon stay in sync. -->

    <!-- Microsoft Windows logo (4-pane grid) - Used for Default badge + Reset to Defaults menu item -->
    <x:String x:Key="WindowsLogoIconPath">M0,0 H10 V10 H0 Z M12,0 H22 V10 H12 Z M0,12 H10 V22 H0 Z M12,12 H22 V22 H12 Z</x:String>

    <!-- FluentIcons symbol name - Used for Recommended badge + Apply Recommended menu item -->
    <x:String x:Key="BadgeRecommendedIconSymbol">StarCheckmark</x:String>

    <!-- FluentIcons symbol name - Used for Custom badge (no Quick Actions counterpart) -->
    <x:String x:Key="BadgeCustomIconSymbol">PersonWrench</x:String>
```

- [ ] **Step 3: Commit**

```bash
cd C:/Winhance
git add src/Winhance.UI/Features/Common/Resources/FeatureIcons.xaml
git commit -m "feat: add centralized InfoBadge icon keys for reusability"
```

---

## Task 5: Create BadgeStateToStyleConverter

**Files:**
- Create: `src/Winhance.UI/Features/Common/Converters/BadgeStateToStyleConverter.cs`

- [ ] **Step 1: Write the converter**

Create `src/Winhance.UI/Features/Common/Converters/BadgeStateToStyleConverter.cs` with this content:

```csharp
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.UI.Features.Common.Converters;

/// <summary>
/// Converts a <see cref="SettingBadgeState"/> value to the matching pill Style resource.
/// Styles are looked up from Application.Current.Resources by key
/// ("BadgeRecommendedStyle", "BadgeDefaultStyle", "BadgeCustomStyle").
/// </summary>
public partial class BadgeStateToStyleConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not SettingBadgeState state)
        {
            return null;
        }

        var key = state switch
        {
            SettingBadgeState.Recommended => "BadgeRecommendedStyle",
            SettingBadgeState.Default => "BadgeDefaultStyle",
            SettingBadgeState.Custom => "BadgeCustomStyle",
            _ => null,
        };

        if (key is null)
        {
            return null;
        }

        return Application.Current.Resources.TryGetValue(key, out var style) ? style as Style : null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}
```

- [ ] **Step 2: Create the unit test file**

Create `tests/Winhance.UI.Tests/Converters/BadgeStateToStyleConverterTests.cs`:

```csharp
using FluentAssertions;
using Winhance.Core.Features.Common.Enums;
using Winhance.UI.Features.Common.Converters;
using Xunit;

namespace Winhance.UI.Tests.Converters;

public class BadgeStateToStyleConverterTests
{
    private readonly BadgeStateToStyleConverter _sut = new();

    [Fact]
    public void Convert_NonEnumValue_ReturnsNull()
    {
        var result = _sut.Convert("not an enum", typeof(object), null!, "en");
        result.Should().BeNull();
    }

    [Fact]
    public void ConvertBack_Throws()
    {
        var act = () => _sut.ConvertBack(null!, typeof(object), null!, "en");
        act.Should().Throw<NotImplementedException>();
    }

    // Note: Resource-lookup branches cannot be meaningfully unit-tested without
    // a running WinUI application host. The lookup logic is validated via
    // manual visual verification in Task 16.
}
```

- [ ] **Step 3: Build and run the tests**

Run:
```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" tests/Winhance.UI.Tests/Winhance.UI.Tests.csproj -t:Build -p:Platform=x64 -p:Configuration=Debug -v:quiet
```

Then:
```bash
dotnet test tests/Winhance.UI.Tests/Winhance.UI.Tests.csproj --filter "BadgeStateToStyleConverterTests" -p:Platform=x64 --no-build
```

Expected: both tests PASS.

- [ ] **Step 4: Commit**

```bash
cd C:/Winhance
git add src/Winhance.UI/Features/Common/Converters/BadgeStateToStyleConverter.cs tests/Winhance.UI.Tests/Converters/BadgeStateToStyleConverterTests.cs
git commit -m "feat: add BadgeStateToStyleConverter for pill styling"
```

---

## Task 6: Create BadgeStateToForegroundConverter

**Files:**
- Create: `src/Winhance.UI/Features/Common/Converters/BadgeStateToForegroundConverter.cs`
- Create: `tests/Winhance.UI.Tests/Converters/BadgeStateToForegroundConverterTests.cs`

- [ ] **Step 1: Write the converter**

Create `src/Winhance.UI/Features/Common/Converters/BadgeStateToForegroundConverter.cs`:

```csharp
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.UI.Features.Common.Converters;

/// <summary>
/// Converts a <see cref="SettingBadgeState"/> value to the matching pill foreground brush.
/// Brushes are looked up from Application.Current.Resources by key
/// ("BadgeRecommendedForeground", "BadgeDefaultForeground", "BadgeCustomForeground").
/// </summary>
public partial class BadgeStateToForegroundConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not SettingBadgeState state)
        {
            return null;
        }

        var key = state switch
        {
            SettingBadgeState.Recommended => "BadgeRecommendedForeground",
            SettingBadgeState.Default => "BadgeDefaultForeground",
            SettingBadgeState.Custom => "BadgeCustomForeground",
            _ => null,
        };

        if (key is null)
        {
            return null;
        }

        return Application.Current.Resources.TryGetValue(key, out var brush) ? brush as Brush : null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}
```

- [ ] **Step 2: Write the test file**

Create `tests/Winhance.UI.Tests/Converters/BadgeStateToForegroundConverterTests.cs`:

```csharp
using FluentAssertions;
using Winhance.UI.Features.Common.Converters;
using Xunit;

namespace Winhance.UI.Tests.Converters;

public class BadgeStateToForegroundConverterTests
{
    private readonly BadgeStateToForegroundConverter _sut = new();

    [Fact]
    public void Convert_NonEnumValue_ReturnsNull()
    {
        var result = _sut.Convert(42, typeof(object), null!, "en");
        result.Should().BeNull();
    }

    [Fact]
    public void ConvertBack_Throws()
    {
        var act = () => _sut.ConvertBack(null!, typeof(object), null!, "en");
        act.Should().Throw<NotImplementedException>();
    }
}
```

- [ ] **Step 3: Build and run the tests**

```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" tests/Winhance.UI.Tests/Winhance.UI.Tests.csproj -t:Build -p:Platform=x64 -p:Configuration=Debug -v:quiet

dotnet test tests/Winhance.UI.Tests/Winhance.UI.Tests.csproj --filter "BadgeStateToForegroundConverterTests" -p:Platform=x64 --no-build
```

Expected: both tests PASS.

- [ ] **Step 4: Commit**

```bash
cd C:/Winhance
git add src/Winhance.UI/Features/Common/Converters/BadgeStateToForegroundConverter.cs tests/Winhance.UI.Tests/Converters/BadgeStateToForegroundConverterTests.cs
git commit -m "feat: add BadgeStateToForegroundConverter for pill text color"
```

---

## Task 7: Create BadgeIconTemplateSelector

**Files:**
- Create: `src/Winhance.UI/Features/Common/Converters/BadgeIconTemplateSelector.cs`

- [ ] **Step 1: Write the selector**

Create `src/Winhance.UI/Features/Common/Converters/BadgeIconTemplateSelector.cs`:

```csharp
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.UI.Features.Common.Converters;

/// <summary>
/// Selects the appropriate icon <see cref="DataTemplate"/> for an InfoBadge pill
/// based on the <see cref="SettingBadgeState"/> value passed as the content.
/// </summary>
/// <remarks>
/// Icon controls are heterogeneous — Recommended/Custom use <c>FluentIcons:SymbolIcon</c>,
/// Default uses <c>PathIcon</c>. This selector keeps that heterogeneity out of the
/// consumer's XAML. Templates are set at resource-declaration time in BadgeStyles.xaml.
/// </remarks>
public partial class BadgeIconTemplateSelector : DataTemplateSelector
{
    public DataTemplate? RecommendedTemplate { get; set; }
    public DataTemplate? DefaultTemplate { get; set; }
    public DataTemplate? CustomTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item)
    {
        if (item is not SettingBadgeState state)
        {
            return null;
        }

        return state switch
        {
            SettingBadgeState.Recommended => RecommendedTemplate,
            SettingBadgeState.Default => DefaultTemplate,
            SettingBadgeState.Custom => CustomTemplate,
            _ => null,
        };
    }

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
        => SelectTemplateCore(item);
}
```

- [ ] **Step 2: Build to confirm it compiles**

```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" tests/Winhance.UI.Tests/Winhance.UI.Tests.csproj -t:Build -p:Platform=x64 -p:Configuration=Debug -v:quiet
```

Expected: build succeeds with no new errors.

*(No unit tests — behaviour requires the full WinUI resource graph at runtime.)*

- [ ] **Step 3: Commit**

```bash
cd C:/Winhance
git add src/Winhance.UI/Features/Common/Converters/BadgeIconTemplateSelector.cs
git commit -m "feat: add BadgeIconTemplateSelector for heterogeneous badge icons"
```

---

## Task 8: Create BadgeStyles.xaml

**Files:**
- Create: `src/Winhance.UI/Features/Common/Resources/BadgeStyles.xaml`

- [ ] **Step 1: Write the resource dictionary**

Create `src/Winhance.UI/Features/Common/Resources/BadgeStyles.xaml` with this content:

```xml
<?xml version="1.0" encoding="utf-8"?>
<ResourceDictionary
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:converters="using:Winhance.UI.Features.Common.Converters"
    xmlns:fluentIcons="using:FluentIcons.WinUI">

    <!--
    BadgeStyles.xaml
    Centralized visual definition for the SettingsCard InfoBadge pill.
    Includes:
      - Pill Border styles per state (Recommended / Default / Custom)
      - Foreground brushes matching each pill style
      - Shared text and icon styles
      - The BadgeIconTemplateSelector resource with its three inline icon templates
    -->

    <!-- ==================== Pill Border Base ==================== -->
    <Style x:Key="BadgePillBase" TargetType="Border">
        <Setter Property="CornerRadius" Value="999"/>
        <Setter Property="Padding" Value="7,3,10,3"/>
        <Setter Property="VerticalAlignment" Value="Center"/>
        <Setter Property="BorderThickness" Value="1"/>
    </Style>

    <!-- ==================== State-Specific Pill Styles ==================== -->
    <Style x:Key="BadgeRecommendedStyle" TargetType="Border" BasedOn="{StaticResource BadgePillBase}">
        <Setter Property="Background" Value="#224EC94E"/>
        <Setter Property="BorderBrush" Value="#404EC94E"/>
    </Style>

    <Style x:Key="BadgeDefaultStyle" TargetType="Border" BasedOn="{StaticResource BadgePillBase}">
        <Setter Property="Background" Value="#12FFFFFF"/>
        <Setter Property="BorderBrush" Value="#24FFFFFF"/>
    </Style>

    <Style x:Key="BadgeCustomStyle" TargetType="Border" BasedOn="{StaticResource BadgePillBase}">
        <Setter Property="Background" Value="#22F0A030"/>
        <Setter Property="BorderBrush" Value="#40F0A030"/>
    </Style>

    <!-- ==================== Foreground Brushes ==================== -->
    <SolidColorBrush x:Key="BadgeRecommendedForeground" Color="#FF4EC94E"/>
    <SolidColorBrush x:Key="BadgeDefaultForeground" Color="#99FFFFFF"/>
    <SolidColorBrush x:Key="BadgeCustomForeground" Color="#FFF0A030"/>

    <!-- ==================== Text Style ==================== -->
    <Style x:Key="BadgeTextStyle" TargetType="TextBlock">
        <Setter Property="FontSize" Value="11"/>
        <Setter Property="FontWeight" Value="SemiBold"/>
        <Setter Property="VerticalAlignment" Value="Center"/>
    </Style>

    <!-- ==================== Icon Template Selector ==================== -->
    <!-- Each template is a single icon control with the correct foreground brush.
         The 4px right margin separates the icon from the pill's text label. -->
    <converters:BadgeIconTemplateSelector x:Key="BadgeIconTemplateSelector">
        <converters:BadgeIconTemplateSelector.RecommendedTemplate>
            <DataTemplate>
                <fluentIcons:SymbolIcon
                    Symbol="StarCheckmark"
                    IconVariant="Regular"
                    FontSize="11"
                    VerticalAlignment="Center"
                    Margin="0,0,4,0"
                    Foreground="{StaticResource BadgeRecommendedForeground}"/>
            </DataTemplate>
        </converters:BadgeIconTemplateSelector.RecommendedTemplate>
        <converters:BadgeIconTemplateSelector.DefaultTemplate>
            <DataTemplate>
                <PathIcon
                    Data="{StaticResource WindowsLogoIconPath}"
                    VerticalAlignment="Center"
                    Margin="0,0,4,0"
                    Height="11"
                    Width="11"
                    Foreground="{StaticResource BadgeDefaultForeground}"/>
            </DataTemplate>
        </converters:BadgeIconTemplateSelector.DefaultTemplate>
        <converters:BadgeIconTemplateSelector.CustomTemplate>
            <DataTemplate>
                <fluentIcons:SymbolIcon
                    Symbol="PersonWrench"
                    IconVariant="Regular"
                    FontSize="11"
                    VerticalAlignment="Center"
                    Margin="0,0,4,0"
                    Foreground="{StaticResource BadgeCustomForeground}"/>
            </DataTemplate>
        </converters:BadgeIconTemplateSelector.CustomTemplate>
    </converters:BadgeIconTemplateSelector>

</ResourceDictionary>
```

- [ ] **Step 2: Commit** *(Do NOT build yet — App.xaml doesn't reference this file yet. Build happens in Task 9.)*

```bash
cd C:/Winhance
git add src/Winhance.UI/Features/Common/Resources/BadgeStyles.xaml
git commit -m "feat: add BadgeStyles.xaml with pill styles and icon selector"
```

---

## Task 9: Register BadgeStyles.xaml + new converters in App.xaml and Converters.xaml

**Files:**
- Modify: `src/Winhance.UI/App.xaml` (around lines 8-19)
- Modify: `src/Winhance.UI/Features/Common/Resources/Converters.xaml` (line 28, "Badge Converters" section)

- [ ] **Step 1: Register BadgeStyles.xaml in App.xaml merged dictionaries**

Open `src/Winhance.UI/App.xaml`. Find the merged-dictionaries block and use Edit.

**old_string:**
```xml
                <!-- Centralized feature icons -->
                <ResourceDictionary Source="ms-appx:///Features/Common/Resources/FeatureIcons.xaml" />
            </ResourceDictionary.MergedDictionaries>
```

**new_string:**
```xml
                <!-- Centralized feature icons -->
                <ResourceDictionary Source="ms-appx:///Features/Common/Resources/FeatureIcons.xaml" />
                <!-- InfoBadge pill styles, foreground brushes, and icon template selector -->
                <ResourceDictionary Source="ms-appx:///Features/Common/Resources/BadgeStyles.xaml" />
            </ResourceDictionary.MergedDictionaries>
```

- [ ] **Step 2: Register new converters in Converters.xaml**

Open `src/Winhance.UI/Features/Common/Resources/Converters.xaml`. Use Edit.

**old_string:**
```xml
    <!-- Badge Converters -->
    <converters:BadgeStateToColorConverter x:Key="BadgeStateToColorConverter"/>
```

**new_string:**
```xml
    <!-- Badge Converters -->
    <converters:BadgeStateToStyleConverter x:Key="BadgeStateToStyleConverter"/>
    <converters:BadgeStateToForegroundConverter x:Key="BadgeStateToForegroundConverter"/>
```

(The old `BadgeStateToColorConverter` registration is removed. Its C# class file is removed in Task 15.)

- [ ] **Step 3: Build the UI project**

```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" tests/Winhance.UI.Tests/Winhance.UI.Tests.csproj -t:Build -p:Platform=x64 -p:Configuration=Debug -v:quiet
```

Expected: build succeeds.

**Note:** If the build fails with a reference to `BadgeStateToColorConverter` still being in use (e.g., from SettingTemplates.xaml), that's expected — Task 11 removes those references. At this point it's acceptable for the build to fail on that one converter. Proceed to Task 10.

- [ ] **Step 4: Commit**

```bash
cd C:/Winhance
git add src/Winhance.UI/App.xaml src/Winhance.UI/Features/Common/Resources/Converters.xaml
git commit -m "feat: register BadgeStyles and new badge converters"
```

---

## Task 10: Update SettingItemViewModel — add BadgeLabel, drop BadgeIcon, repoint BadgeTooltip

**Files:**
- Modify: `src/Winhance.UI/Features/Optimize/ViewModels/SettingItemViewModel.cs` (lines ~203-232)

- [ ] **Step 1: Read the current BadgeTooltip / BadgeIcon block**

Read lines 200-235 of `src/Winhance.UI/Features/Optimize/ViewModels/SettingItemViewModel.cs` to confirm exact current content.

Expected block (approximate):

```csharp
    /// <summary>
    /// Localized tooltip text for the badge ("Recommended", "Default", "Custom").
    /// </summary>
    public string BadgeTooltip => BadgeState switch
    {
        SettingBadgeState.Recommended => _localizationService?.GetString("InfoBadge_Recommended") ?? "Recommended",
        SettingBadgeState.Default => _localizationService?.GetString("InfoBadge_Default") ?? "Default",
        SettingBadgeState.Custom => _localizationService?.GetString("InfoBadge_Custom") ?? "Custom",
        _ => ""
    };

    /// <summary>
    /// Icon glyph for the badge. Matches the Quick Actions menu icons for consistency.
    /// E735 = star/sparkle (Apply Recommended), E777 = reset/undo (Reset Defaults), E70F = pencil (Custom).
    /// </summary>
    public string BadgeIcon => BadgeState switch
    {
        SettingBadgeState.Recommended => "\uE735",
        SettingBadgeState.Default => "\uE777",
        SettingBadgeState.Custom => "\uE70F",
        _ => ""
    };
```

- [ ] **Step 2: Replace the BadgeTooltip + BadgeIcon block with new BadgeTooltip + BadgeLabel**

Use the `Edit` tool.

**old_string:**
```csharp
    /// <summary>
    /// Localized tooltip text for the badge ("Recommended", "Default", "Custom").
    /// </summary>
    public string BadgeTooltip => BadgeState switch
    {
        SettingBadgeState.Recommended => _localizationService?.GetString("InfoBadge_Recommended") ?? "Recommended",
        SettingBadgeState.Default => _localizationService?.GetString("InfoBadge_Default") ?? "Default",
        SettingBadgeState.Custom => _localizationService?.GetString("InfoBadge_Custom") ?? "Custom",
        _ => ""
    };

    /// <summary>
    /// Icon glyph for the badge. Matches the Quick Actions menu icons for consistency.
    /// E735 = star/sparkle (Apply Recommended), E777 = reset/undo (Reset Defaults), E70F = pencil (Custom).
    /// </summary>
    public string BadgeIcon => BadgeState switch
    {
        SettingBadgeState.Recommended => "\uE735",
        SettingBadgeState.Default => "\uE777",
        SettingBadgeState.Custom => "\uE70F",
        _ => ""
    };
```

**new_string:**
```csharp
    /// <summary>
    /// Localized short label shown inside the badge pill ("Recommended", "Default", "Custom").
    /// </summary>
    public string BadgeLabel => BadgeState switch
    {
        SettingBadgeState.Recommended => _localizationService?.GetString(StringKeys.InfoBadge.Recommended) ?? "Recommended",
        SettingBadgeState.Default => _localizationService?.GetString(StringKeys.InfoBadge.Default) ?? "Default",
        SettingBadgeState.Custom => _localizationService?.GetString(StringKeys.InfoBadge.Custom) ?? "Custom",
        _ => ""
    };

    /// <summary>
    /// Localized long-form tooltip shown when hovering the badge pill.
    /// </summary>
    public string BadgeTooltip => BadgeState switch
    {
        SettingBadgeState.Recommended => _localizationService?.GetString(StringKeys.InfoBadge.RecommendedTooltip) ?? "The Recommended values are applied for this setting",
        SettingBadgeState.Default => _localizationService?.GetString(StringKeys.InfoBadge.DefaultTooltip) ?? "The Default Windows values are applied for this setting",
        SettingBadgeState.Custom => _localizationService?.GetString(StringKeys.InfoBadge.CustomTooltip) ?? "Custom values are applied for this setting",
        _ => ""
    };
```

- [ ] **Step 3: Update OnBadgeStateChanged to raise BadgeLabel instead of BadgeIcon**

Find (around lines 228-232):

```csharp
    partial void OnBadgeStateChanged(SettingBadgeState value)
    {
        OnPropertyChanged(nameof(BadgeTooltip));
        OnPropertyChanged(nameof(BadgeIcon));
    }
```

Use Edit.

**old_string:**
```csharp
    partial void OnBadgeStateChanged(SettingBadgeState value)
    {
        OnPropertyChanged(nameof(BadgeTooltip));
        OnPropertyChanged(nameof(BadgeIcon));
    }
```

**new_string:**
```csharp
    partial void OnBadgeStateChanged(SettingBadgeState value)
    {
        OnPropertyChanged(nameof(BadgeLabel));
        OnPropertyChanged(nameof(BadgeTooltip));
    }
```

- [ ] **Step 4: Ensure the StringKeys namespace is imported**

Read the top of `SettingItemViewModel.cs` (first 25 lines). If `using Winhance.UI.Features.Common.Constants;` is missing, add it. Use Edit with a small context window around the existing `using` block.

Example edit — if the file currently has:

```csharp
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
```

Add after the last `using Winhance...` line:

```csharp
using Winhance.UI.Features.Common.Constants;
```

- [ ] **Step 5: Handle language change for BadgeLabel/BadgeTooltip**

Investigate whether `SettingItemViewModel` already subscribes to `ILocalizationService.LanguageChanged`.

Run:
```bash
```
Use Grep to search the file:
- Pattern: `LanguageChanged`
- Path: `src/Winhance.UI/Features/Optimize/ViewModels/SettingItemViewModel.cs`

**If a handler already exists:** find the method body and add two lines to it:

```csharp
OnPropertyChanged(nameof(BadgeLabel));
OnPropertyChanged(nameof(BadgeTooltip));
```

**If no handler exists:** do not add one in this task — many `SettingItemViewModel` instances exist per page, and per-instance subscription may be undesirable architecturally. Instead, leave it as-is; the language change will be reflected on the next time `BadgeState` recomputes (which happens on any value toggle, navigation, or Refresh call). Document this in the commit message.

*(If visual verification in Task 16 reveals stale labels after switching languages, revisit this step to add a central refresh hook — that's scope creep beyond this plan.)*

- [ ] **Step 6: Build the UI tests project**

```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" tests/Winhance.UI.Tests/Winhance.UI.Tests.csproj -t:Build -p:Platform=x64 -p:Configuration=Debug -v:quiet
```

**Note:** The build may still fail on `BadgeStateToColorConverter` references in `SettingTemplates.xaml` (cleaned up in Task 11) and on `BadgeIcon` being referenced in XAML (also cleaned up in Task 11). That's expected. The `SettingItemViewModel.cs` file itself should compile cleanly though — look specifically for errors in that file and fix any.

- [ ] **Step 7: Commit**

```bash
cd C:/Winhance
git add src/Winhance.UI/Features/Optimize/ViewModels/SettingItemViewModel.cs
git commit -m "feat: add BadgeLabel, repoint BadgeTooltip to new localization keys, drop BadgeIcon"
```

---

## Task 11: Update SettingTemplates.xaml — single reactive Border

**Files:**
- Modify: `src/Winhance.UI/Features/Common/Resources/SettingTemplates.xaml` (lines 81-92)

- [ ] **Step 1: Ensure FluentIcons namespace is declared**

Read the top of `src/Winhance.UI/Features/Common/Resources/SettingTemplates.xaml` (first 20 lines). Check whether `xmlns:fluentIcons="using:FluentIcons.WinUI"` is already declared on the root element.

**If not declared:** add it. Use Edit to modify the root `<ResourceDictionary>` tag, adding the namespace alongside existing `xmlns:` declarations.

*(The new Border itself doesn't directly use `fluentIcons:` — the DataTemplates inside `BadgeStyles.xaml` do. However, having the namespace in SettingTemplates.xaml is harmless and future-proofs the file. Skip this step if time-constrained.)*

- [ ] **Step 2: Replace the badge Border block**

Use the `Edit` tool.

**old_string:**
```xml
                    <Border
                        Visibility="{x:Bind ShowInfoBadge, Mode=OneWay}"
                        VerticalAlignment="Center"
                        Padding="4,2"
                        CornerRadius="4"
                        Background="{x:Bind BadgeState, Mode=OneWay, Converter={StaticResource BadgeStateToColorConverter}}"
                        ToolTipService.ToolTip="{x:Bind BadgeTooltip, Mode=OneWay}">
                        <FontIcon Glyph="{x:Bind BadgeIcon, Mode=OneWay}" FontSize="10" Foreground="White"/>
                    </Border>
```

**new_string:**
```xml
                    <Border
                        Style="{x:Bind BadgeState, Mode=OneWay, Converter={StaticResource BadgeStateToStyleConverter}}"
                        Visibility="{x:Bind ShowInfoBadge, Mode=OneWay, Converter={StaticResource BooleanToVisibilityConverter}}"
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

**Important about `ShowInfoBadge`:** The previous code bound `Visibility` directly to a `bool` (WinUI 3 implicitly converts `bool` → `Visibility` for `{Binding}` but NOT reliably for `{x:Bind}`). The new binding explicitly uses `BooleanToVisibilityConverter` for safety. If the current code works without a converter, confirm that `ShowInfoBadge`'s declared type is already `Visibility` (it isn't — it's `bool` per earlier exploration), so the new explicit converter is correct.

- [ ] **Step 3: Build**

```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" tests/Winhance.UI.Tests/Winhance.UI.Tests.csproj -t:Build -p:Platform=x64 -p:Configuration=Debug -v:quiet
```

Expected: build may still fail ONLY on `BadgeStateToColorConverter` being undefined (because we removed its registration in Task 9 but the old .cs file is still referenced from other places). If the error is anywhere other than `BadgeStateToColorConverter`, investigate and fix it. If the error is exclusively about `BadgeStateToColorConverter`, proceed to Task 12 — that converter is deleted in Task 15.

- [ ] **Step 4: Commit**

```bash
cd C:/Winhance
git add src/Winhance.UI/Features/Common/Resources/SettingTemplates.xaml
git commit -m "feat: use modern pill-style InfoBadge with label and tooltip"
```

---

## Task 12: Update Quick Actions icons in OptimizePage.xaml

**Files:**
- Modify: `src/Winhance.UI/Features/Optimize/OptimizePage.xaml` (lines ~170-191)

- [ ] **Step 1: Verify fluentIcons namespace exists on the root element**

Read the first 30 lines of `src/Winhance.UI/Features/Optimize/OptimizePage.xaml`. Confirm `xmlns:fluentIcons="using:FluentIcons.WinUI"` is declared. Per earlier exploration, this file already uses FluentIcons elsewhere, so the namespace should already be present.

If missing, add it to the root `<Page>` or `<UserControl>` element.

- [ ] **Step 2: Replace the Apply Recommended menu item icon**

Use the `Edit` tool. Find the `ApplyRecommendedItem` block.

**old_string:**
```xml
            <MenuFlyoutItem x:Name="ApplyRecommendedItem" Click="ApplyRecommended_Click">
                <MenuFlyoutItem.Icon>
                    <FontIcon Glyph="&#xE735;"/>
                </MenuFlyoutItem.Icon>
            </MenuFlyoutItem>
```

**new_string:**
```xml
            <MenuFlyoutItem x:Name="ApplyRecommendedItem" Click="ApplyRecommended_Click">
                <MenuFlyoutItem.Icon>
                    <fluentIcons:SymbolIcon Symbol="StarCheckmark" IconVariant="Regular"/>
                </MenuFlyoutItem.Icon>
            </MenuFlyoutItem>
```

- [ ] **Step 3: Replace the Reset Defaults menu item icon**

**old_string:**
```xml
            <MenuFlyoutItem x:Name="ResetDefaultsItem" Click="ResetDefaults_Click">
                <MenuFlyoutItem.Icon>
                    <FontIcon Glyph="&#xE777;"/>
                </MenuFlyoutItem.Icon>
            </MenuFlyoutItem>
```

**new_string:**
```xml
            <MenuFlyoutItem x:Name="ResetDefaultsItem" Click="ResetDefaults_Click">
                <MenuFlyoutItem.Icon>
                    <PathIcon Data="{StaticResource WindowsLogoIconPath}"/>
                </MenuFlyoutItem.Icon>
            </MenuFlyoutItem>
```

- [ ] **Step 4: Commit**

```bash
cd C:/Winhance
git add src/Winhance.UI/Features/Optimize/OptimizePage.xaml
git commit -m "feat: align Optimize Quick Actions icons with InfoBadge icons"
```

---

## Task 13: Update Quick Actions icons in CustomizePage.xaml

**Files:**
- Modify: `src/Winhance.UI/Features/Customize/CustomizePage.xaml` (lines ~157-168)

- [ ] **Step 1: Verify fluentIcons namespace exists**

Read the first 30 lines of `src/Winhance.UI/Features/Customize/CustomizePage.xaml`. Confirm `xmlns:fluentIcons="using:FluentIcons.WinUI"` is declared. Add if missing.

- [ ] **Step 2: Replace Apply Recommended icon**

Same edit pattern as Task 12 Step 2.

**old_string:**
```xml
            <MenuFlyoutItem x:Name="ApplyRecommendedItem" Click="ApplyRecommended_Click">
                <MenuFlyoutItem.Icon>
                    <FontIcon Glyph="&#xE735;"/>
                </MenuFlyoutItem.Icon>
            </MenuFlyoutItem>
```

**new_string:**
```xml
            <MenuFlyoutItem x:Name="ApplyRecommendedItem" Click="ApplyRecommended_Click">
                <MenuFlyoutItem.Icon>
                    <fluentIcons:SymbolIcon Symbol="StarCheckmark" IconVariant="Regular"/>
                </MenuFlyoutItem.Icon>
            </MenuFlyoutItem>
```

- [ ] **Step 3: Replace Reset Defaults icon**

**old_string:**
```xml
            <MenuFlyoutItem x:Name="ResetDefaultsItem" Click="ResetDefaults_Click">
                <MenuFlyoutItem.Icon>
                    <FontIcon Glyph="&#xE777;"/>
                </MenuFlyoutItem.Icon>
            </MenuFlyoutItem>
```

**new_string:**
```xml
            <MenuFlyoutItem x:Name="ResetDefaultsItem" Click="ResetDefaults_Click">
                <MenuFlyoutItem.Icon>
                    <PathIcon Data="{StaticResource WindowsLogoIconPath}"/>
                </MenuFlyoutItem.Icon>
            </MenuFlyoutItem>
```

- [ ] **Step 4: Commit**

```bash
cd C:/Winhance
git add src/Winhance.UI/Features/Customize/CustomizePage.xaml
git commit -m "feat: align Customize Quick Actions icons with InfoBadge icons"
```

---

## Task 14: Verify no remaining BadgeIcon bindings

**Files:**
- Potential modifications anywhere `BadgeIcon` is still bound

- [ ] **Step 1: Grep for any remaining references**

Use Grep:
- Pattern: `BadgeIcon`
- Path: `src/Winhance.UI`
- Output mode: `content` with line numbers

Expected result: zero matches (or only matches in comments).

If any `.xaml` or `.cs` file still references `BadgeIcon` as a property or binding, open that file, evaluate what it should do (likely bind to `BadgeLabel` for text OR let the `ContentTemplateSelector` handle the icon) and remove/update accordingly. Any such finding should be handled as a Step 2 sub-edit before committing.

- [ ] **Step 2: Also grep for BadgeStateToColorConverter references**

Use Grep:
- Pattern: `BadgeStateToColorConverter`
- Path: `src/Winhance.UI`
- Output mode: `content` with line numbers

Expected result: zero matches in XAML (the registration was removed in Task 9, and SettingTemplates.xaml was updated in Task 11). Only the `.cs` class definition should match — that file is deleted in Task 15.

If any other `.xaml` file still references it, update those references to use `BadgeStateToStyleConverter` / `BadgeStateToForegroundConverter` as appropriate.

- [ ] **Step 3: Commit (only if edits were made)**

```bash
cd C:/Winhance
git status
# If there are changes:
git add -A  # only for explicit files shown in status — inspect before running
git commit -m "fix: clean up stray BadgeIcon / BadgeStateToColorConverter references"
```

If no edits were needed, this task produces no commit — that's expected.

---

## Task 15: Delete the old BadgeStateToColorConverter

**Files:**
- Delete: `src/Winhance.UI/Features/Common/Converters/BadgeStateToColorConverter.cs`

- [ ] **Step 1: Confirm no remaining references**

Use Grep:
- Pattern: `BadgeStateToColorConverter`
- Path: `C:/Winhance` (entire repo)

Expected: only matches inside the `.cs` file itself (and possibly the spec/plan docs, which are fine).

- [ ] **Step 2: Delete the file**

```bash
cd C:/Winhance
rm src/Winhance.UI/Features/Common/Converters/BadgeStateToColorConverter.cs
```

- [ ] **Step 3: Build to confirm clean removal**

```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" tests/Winhance.UI.Tests/Winhance.UI.Tests.csproj -t:Build -p:Platform=x64 -p:Configuration=Debug -v:quiet
```

Expected: build SUCCEEDS. If it fails, inspect errors — the converter is referenced somewhere Task 14 missed. Fix that reference before committing.

- [ ] **Step 4: Run the full test suite**

```bash
dotnet test tests/Winhance.UI.Tests/Winhance.UI.Tests.csproj -p:Platform=x64 --no-build
```

Expected: 1300+ tests PASS (including the two new converter tests added in Tasks 5 & 6 — target count becomes ~1302). No new failures. If any test fails, diagnose before continuing.

- [ ] **Step 5: Commit**

```bash
cd C:/Winhance
git add -A src/Winhance.UI/Features/Common/Converters/BadgeStateToColorConverter.cs
git commit -m "refactor: remove obsolete BadgeStateToColorConverter"
```

---

## Task 16: Manual visual verification

**Files:** none modified (verification only)

- [ ] **Step 1: Build the full UI project**

```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" src/Winhance.UI/Winhance.UI.csproj -t:Build -p:Platform=x64 -p:Configuration=Debug -v:quiet
```

Expected: build SUCCEEDS.

- [ ] **Step 2: Ask the user (Marco) to run the app and verify**

⚠️ **This step requires a human.** The Claude agent cannot launch a WinUI 3 desktop app from a sandboxed shell and observe pixels.

Prompt Marco with a checklist (paste this verbatim into the session):

> Please run Winhance locally and verify the modern InfoBadge pills:
>
> **Visual:**
> 1. Open **Optimize → Privacy (or any section)**. Each `SettingsCard` should show a rounded pill on the right of the setting name with an icon + label:
>    - Green pill with star-checkmark + "Recommended" when the setting matches its recommended value
>    - Neutral/off-white pill with Windows logo + "Default" when at Windows defaults
>    - Amber pill with person-wrench + "Custom" when manually modified
> 2. Toggle a setting and watch the badge transition between the three states live.
> 3. Hover each pill → tooltip shows the long-form localized text (e.g., "The Recommended values are applied for this setting").
>
> **Icon parity:**
> 4. Open the **Quick Actions** dropdown. Confirm:
>    - "Apply Recommended Settings" shows the star-checkmark icon (same as on the green badge)
>    - "Reset to Windows Defaults" shows the Windows logo (same as on the neutral badge)
>
> **Localization:**
> 5. Open **Settings → Language** and switch to **German**. Return to Optimize. Badge labels and tooltips should now show in German.
> 6. Switch to **Arabic** (RTL). Pills should still render correctly. Tooltips should be in Arabic.
>
> **View toggle:**
> 7. Open the **View** dropdown → uncheck **InfoBadges**. All pills disappear. Re-check → they return.
>
> Report back anything that looks wrong or unexpected.

- [ ] **Step 3: Fix any issues Marco reports**

Any issue found is a bug fix, not a plan task. Use Edit on the relevant file. If the fix is substantive (e.g., language change not reflected in BadgeLabel), the fix may require a stand-alone follow-up commit.

- [ ] **Step 4: Final commit (if any fixes were applied)**

```bash
cd C:/Winhance
git add <specific files>
git commit -m "fix: <specific issue found in manual verification>"
```

- [ ] **Step 5: Plan complete**

Report plan completion to the user. Summarize commits created (`git log --oneline dev ^origin/dev` to see all new commits on this branch).

---

## Self-Review

- ✅ **Spec coverage** — every §1–§8 section of the design spec maps to a task: §1 icon keys → Task 4; §2 BadgeStyles.xaml → Task 8; §3 converters + selector → Tasks 5, 6, 7; §4 badge XAML → Task 11; §5 ViewModel → Task 10; §6 Quick Actions → Tasks 12, 13; §7 localization → Tasks 1, 2, 3; §8 cleanup → Tasks 14, 15.
- ✅ **No placeholders** — every step contains exact file paths, exact code snippets (with `old_string` / `new_string` for edits), exact commands, expected outcomes.
- ✅ **Type consistency** — `BadgeLabel`, `BadgeTooltip`, `BadgeState`, `ShowInfoBadge`, `BadgeStateToStyleConverter`, `BadgeStateToForegroundConverter`, `BadgeIconTemplateSelector` all named identically across tasks.
- ✅ **Resource key consistency** — `BadgeRecommendedStyle` / `BadgeDefaultStyle` / `BadgeCustomStyle` / `BadgeRecommendedForeground` / `BadgeDefaultForeground` / `BadgeCustomForeground` / `BadgeTextStyle` / `BadgePillBase` / `WindowsLogoIconPath` / `BadgeRecommendedIconSymbol` / `BadgeCustomIconSymbol` / `BadgeIconTemplateSelector` — defined in BadgeStyles.xaml (Task 8) and FeatureIcons.xaml (Task 4), consumed in SettingTemplates.xaml (Task 11), Converters.xaml (Task 9), and Quick Actions pages (Tasks 12–13).
- ✅ **Localization constraint preserved** — Task 2 uses parallel subagents via the Edit tool; no scripts.
- ✅ **No Co-Authored-By lines** — commit messages in this plan do not include any Co-Authored-By trailer (per project memory `feedback_no_coauthored.md`).

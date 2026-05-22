# Software & Apps — Light-mode Icon Variants

**Status:** Brainstormed and approved by Marco — ready for implementation plan.
**Date:** 2026-05-19
**Scope:** Software & Apps page, both tabs (Windows Apps and External Apps), all icons resolved by `IAppIconResolver`.

## Problem

Some app icons sourced by `AppIconResolver` are white-on-transparent (e.g. monochrome vendor marks like GitHub Desktop). In dark mode they render correctly against the dark card background. In light mode they render white-on-white and are effectively invisible.

The current cache is theme-agnostic: each icon is stored as a single PNG, whatever the first successful source returned, with no post-processing beyond `TryTrimTransparentBordersAsync`. The display side is a plain `<Image Source="{Binding IconSource}" />` with no per-theme treatment.

## Reference: how Windows handles this

Two real mechanisms exist in the platform:

1. **AppX `altform-lightunplated` variants** — apps that ship a `Square44x44Logo.altform-lightunplated.png` alongside the default `unplated` asset get the light-theme variant picked automatically by Windows when in light mode. The "grey-looking" appearance Marco observed is not a computed grey — it is a separate asset the app's designer drew. Windows Settings → Installed apps relies on this.
2. **Backplates** — Start menu tiles render a colored rounded backplate behind the logo, using the `BackgroundColor` declared in the AppX manifest. This is what gives Start tiles their guaranteed-readable contrast regardless of icon color.

Neither mechanism is reusable verbatim for Winhance: most non-AppX icons (Layer 1 `IconSources`, Layer 3 binary extraction, Layer 4 Store CDN) ship no theme variants, and adding backplates to every card would make the layout visually busier than the current design.

## Approach

For each cached icon, detect whether it is a monochrome-white icon at write time. If yes, generate a companion file with the same shape recolored to a dark target color, alpha preserved so anti-aliasing survives. At render time, the view-model picks between the original and the companion file based on the current effective theme.

This is closest in spirit to mechanism (1) above — the light-mode variant is a uniform dark tone of the same silhouette, which is what `lightunplated` AppX assets typically look like in practice.

Colored icons (Spotify, Chrome, etc.) do not get a companion file generated. They look identical in both themes, same as today.

## File layout

```
%ProgramData%\Winhance\IconCache\
  github-desktop.f7a3.png         ← original (what we cache today)
  github-desktop.f7a3.light.png   ← NEW, generated if monochrome-white
  spotify.b8e1.png                ← no .light.png (colored, detection skipped it)
```

Naming convention: `<id>.<short-hash>.light.png` — same `id.hash` prefix as the primary so they prune together when `PruneOldVersions` runs, and so users poking around the cache can pair them at a glance.

## Components and changes

### A. `AppIconResolver.WriteStreamToCacheAsync` (Infrastructure)

After the existing trim step, run a new `TryGenerateLightVariantAsync(primaryBytes, ct)`. If it returns non-null bytes, write them atomically to `LightVariantPath(cachePath)` next to the primary. If null, skip — no orphan files, no zero-byte markers.

```csharp
private async Task WriteStreamToCacheAsync(Stream source, string cachePath, CancellationToken ct)
{
    var sourceBytes = await ReadAllBytesAsync(source, ct).ConfigureAwait(false);
    var primaryBytes = await TryTrimTransparentBordersAsync(sourceBytes, ct).ConfigureAwait(false)
                      ?? sourceBytes;

    await WriteAtomicAsync(cachePath, primaryBytes, ct).ConfigureAwait(false);

    var lightBytes = await TryGenerateLightVariantAsync(primaryBytes, ct).ConfigureAwait(false);
    if (lightBytes is not null)
    {
        await WriteAtomicAsync(LightVariantPath(cachePath), lightBytes, ct).ConfigureAwait(false);
    }
}

private static string LightVariantPath(string primaryPath) =>
    Path.ChangeExtension(primaryPath, null) + ".light.png";
```

`WriteAtomicAsync` is the existing tmp-then-move pattern factored out so both primary and variant writes share it.

### B. `TryGenerateLightVariantAsync` — detect-and-recolor

Pure function over bytes: decodes the primary PNG once, walks opaque pixels, decides monochrome-or-not, optionally re-encodes with all opaque RGB replaced.

**Detection** (returns `null` if it fails):

- Decode to `BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight`. Straight (not premultiplied) matters here because the recolor step replaces raw RGB values — premultiplied storage would force a denormalize step first. The existing trim path uses Premultiplied because it only inspects alpha, not color.
- For every pixel with `alpha > AlphaTrimThreshold` (same constant the trim uses, so we measure the visible silhouette and ignore antialiasing halo):
  - Convert to HSL via the standard formula (Wikipedia, lightness = (max+min)/2, saturation defined piecewise on lightness), accumulate mean lightness and mean saturation.
- Decide: monochrome-white if `meanLightness > MonochromeMinLightness` (0.85) AND `meanSaturation < MonochromeMaxSaturation` (0.15).
- If the icon has no pixels above the alpha threshold, return `null`.

**Recolor:**

- For every pixel with `alpha > 0` (NOT the trim threshold — we want soft-edge halo pixels recolored too, otherwise the silhouette ends up with a faint white glow around it in light mode), replace RGB with `LightVariantTargetColor` (`#1F1F1F`). Keep the original alpha untouched so anti-aliased edges and soft halos survive intact, just in the new color.
- Re-encode as PNG via `BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, …)` from the modified `SoftwareBitmap`.

**Tuning knobs**, named constants next to `AlphaTrimThreshold` in `AppIconResolver.cs`:

```csharp
// Target color for the synthesized light-mode variant. Sampled from Win11's
// own `lightunplated` AppX renders in Settings → Apps (typical range
// #1A1A1A to #2A2A2A). Looks like a real dark icon, not a washed-out grey.
private static readonly (byte R, byte G, byte B) LightVariantTargetColor = (0x1F, 0x1F, 0x1F);

// Detection thresholds. An icon is treated as monochrome-white if the mean
// HSL lightness of its opaque pixels exceeds MonochromeMinLightness AND the
// mean saturation falls below MonochromeMaxSaturation. Tuned to catch white
// vendor marks (GitHub Desktop, etc.) while leaving colored logos (Spotify,
// Chrome) and partially-colored marks (anything with a saturated accent)
// alone. If these change meaningfully, manually wipe
// %ProgramData%\Winhance\IconCache so cached files re-extract.
private const double MonochromeMinLightness = 0.85;
private const double MonochromeMaxSaturation = 0.15;
```

The detection runs unconditionally for every cached icon. Cost: one decode + one O(width × height) walk over a 96×96 buffer = ~10 ms on a modern CPU, dwarfed by the network/AppX fetch the resolver already did. No theme check inside the resolver — variants are written if applicable regardless of the current theme so theme switches are instant later.

### C. `AppItemViewModel.IconSource` (UI)

The getter currently returns a memoized `BitmapImage` keyed on `Definition.IconPath`. Two changes:

1. Resolve the actual file path before memoization, consulting `IThemeService.GetEffectiveTheme()`:

   ```csharp
   public BitmapImage? IconSource
   {
       get
       {
           var basePath = Definition.IconPath;
           if (string.IsNullOrEmpty(basePath)) return null;

           var resolvedPath = ResolveThemeAwarePath(basePath);

           if (_iconSource is not null && _iconSourcePath == resolvedPath)
               return _iconSource;

           var bmp = new BitmapImage(new Uri(resolvedPath));
           _iconSource = bmp;
           _iconSourcePath = resolvedPath;
           return _iconSource;
       }
   }

   private string ResolveThemeAwarePath(string basePath)
   {
       if (_themeService.GetEffectiveTheme() != ElementTheme.Light) return basePath;
       var lightPath = Path.ChangeExtension(basePath, null) + ".light.png";
       return File.Exists(lightPath) ? lightPath : basePath;
   }
   ```

   The memoization key changes from `Definition.IconPath` to the resolved path. Without this change, a theme toggle hands out the previously cached `BitmapImage` even after the variant should swap in.

2. Constructor takes `IThemeService` and subscribes to `ThemeChanged` for live swap:

   ```csharp
   public AppItemViewModel(ItemDefinition def, IThemeService themeService /* + existing deps */)
   {
       Definition = def;
       _themeService = themeService;
       _themeService.ThemeChanged += OnThemeChanged;
   }

   private void OnThemeChanged(object? sender, WinhanceTheme theme)
   {
       OnPropertyChanged(nameof(IconSource));
   }
   ```

   Unsubscribe in `Dispose` (or finalizer if no Dispose exists today — wiring decision pinned in the implementation plan).

   Marco confirmed during brainstorming that the user can only switch themes from the Settings page, so they are never visually staring at the Software/Apps cards at the moment of the toggle. The live-swap is kept anyway because (a) the Windows system theme can change out from under the app via `UISettings.ColorValuesChanged`, and (b) the cost is negligible — one event subscription per VM, fired rarely.

### D. XAML

No changes. The existing `<Image Source="{Binding IconSource}" />` bindings re-evaluate when the VM raises `PropertyChanged(nameof(IconSource))`, which is what step C does on theme change.

## Cache invalidation

Two scenarios:

- **Primary cache file changes** (source URL update, AppX version bump): the existing `<id>.<hash>.png` hash-in-filename invalidates the primary. The `.light.png` is generated alongside the new primary on the next resolve. Old primary + old `.light.png` get pruned together by `PruneOldVersions` since they share the `<id>.` prefix.
- **Detection/recolor algorithm changes in a future commit**: existing `.light.png` files would be stale. Same convention as the existing `AlphaTrimThreshold` knob — a comment in code documenting "if these change meaningfully, wipe `%ProgramData%\Winhance\IconCache`." No automatic invalidation.

This feature has not shipped yet — only Marco has any cache files locally — so no migration logic is needed for the rollout itself. Marco wipes his local cache once when this lands and starts fresh.

## Out of scope

- **Vector sources (SVG):** would be ideal for theme tinting but the resolver only handles bitmaps today. Out of scope.
- **Dark-mode-broken icons** (very dark icons disappearing on dark cards): symmetric problem, not what triggered this design. The same machinery would solve it — generate a `.dark.png` when an icon is monochrome-dark — but defer until someone reports it.
- **Explicit AppX `lightunplated` request**: the AppX layer currently calls `GetLogo(LogoSize)` which returns whatever Windows picks for the *current* theme. Asking for both variants explicitly is a more faithful approach (the "Approach C" from brainstorming) but adds complexity and theme-aware cache keys. Deferred.

## Risks

- **Cache size:** roughly doubles for affected icons only (monochrome-whites). Current cache is single-digit MB; this stays single-digit MB. Colored icons add no overhead.
- **HSL conversion cost:** decoding + walking a 96×96 buffer takes ~10 ms per icon. The resolver runs in the background and resolves icons in parallel — no UI-thread impact.
- **Theme subscription leak:** each `AppItemViewModel` adds an event handler to `IThemeService.ThemeChanged`. The list of VMs is bounded (a few dozen at most) and lives as long as the page does, but unsubscribe-on-dispose still matters for correctness. Implementation plan covers the disposal contract.
- **Partial-white icons** (e.g. a mostly-white mark with a saturated accent dot): the detection's saturation threshold rejects these as "not monochrome." They remain visible-but-imperfect in light mode (white parts wash out, accent stays). Accepting this trade-off — uniformly desaturating partially-colored icons looked worse in the brainstorming preview.

## Test plan

New unit tests under `tests/`, parallel to the existing `AppIconResolver` fixture:

1. **Detects monochrome-white icon → writes `.light.png`** — fixture PNG that's all-white-on-transparent; assert the variant file is written and its pixels are `#1F1F1F` with alpha preserved.
2. **Skips colored icon → no `.light.png` written** — fixture PNG with saturated green pixels; assert variant file does not exist.
3. **Skips mixed mark (white + saturated accent)** — fixture PNG with majority-white + a few saturated pixels; assert mean saturation exceeds threshold, no variant written.
4. **Empty / all-transparent input → no variant written** — guard against divide-by-zero in mean accumulation.
5. **`AppItemViewModel.IconSource` memoization keys on resolved path** — regression test for the swap bug: in dark mode the getter returns the primary; flip the theme service to light; assert next getter call returns a different `BitmapImage` (or at least a different `_iconSourcePath`).
6. **`ThemeChanged` triggers `PropertyChanged(IconSource)`** — VM-level test, fake `IThemeService` raises the event, assert the property-changed handler fires for `IconSource`.

## Rollout

- Branch `agent/light-mode-app-icons`.
- Spec → implementation plan → implementation (TDD per the existing test plan) → PR into `dev`.
- Marco wipes `%ProgramData%\Winhance\IconCache` once before testing.
- No release-notes change — feature ships with the next release as part of the broader icon-pipeline work that has not gone public yet.

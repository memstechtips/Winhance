# Light-mode App Icons Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Generate a dark-tinted sibling cache file for monochrome-white app icons at write time, and have `AppItemViewModel` pick between the original and the sibling at render time based on the effective theme. Solves white-on-white icons on the Software & Apps page in light mode.

**Architecture:** Approach A from the spec. A new `LightVariantSynthesizer` static helper detects monochrome-white PNGs and re-encodes them with all opaque RGB replaced by `#1F1F1F`. `AppIconResolver.WriteStreamToCacheAsync` calls it after the existing trim step and writes a `.light.png` sibling when bytes come back. `AppItemViewModel` consults `IThemeService.GetEffectiveTheme()` to decide which file to bind, subscribes to `ThemeChanged` for live swap, and re-keys its `BitmapImage` memoization on the resolved path so theme flips don't return stale bitmaps. Parent VMs (`WindowsAppsViewModel`, `ExternalAppsViewModel`) accept and forward `IThemeService` to each `AppItemViewModel` they create.

**Tech Stack:** .NET 10, WinUI 3, Windows.Graphics.Imaging (`BitmapDecoder`/`BitmapEncoder`/`SoftwareBitmap`) for PNG processing. xUnit + FluentAssertions + Moq for tests.

**Spec:** `docs/superpowers/specs/2026-05-19-light-mode-app-icons-design.md`

**Marco's verification rules (CLAUDE.md + memory):**
- This is a non-trivial Winhance behavior change. **Do NOT push, do NOT open a PR, do NOT comment on any GitHub issue.** Per memory: Winhance work commits directly on local `dev` (no feature branches); push waits for Marco's Windows build + confirmation.
- Do NOT run `dotnet build` or `dotnet test` from this Linux host. Verification of compile correctness happens via the Opus-pinned sub-agent in Task 7.
- Per memory: planned execution runs end-to-end without per-step asks. Single notify-when-done at Task 7 Step 4.

---

## File Structure

**New files:**
- `src/Winhance.Infrastructure/Features/SoftwareApps/Services/LightVariantSynthesizer.cs` — detect + recolor static helper
- `tests/Winhance.Infrastructure.Tests/Services/LightVariantSynthesizerTests.cs` — unit tests for the helper
- `tests/Winhance.Infrastructure.Tests/Helpers/PngTestHelper.cs` — synthesize real PNG bytes for tests

**Modified files:**
- `src/Winhance.Infrastructure/Features/SoftwareApps/Services/AppIconResolver.cs` — wire synthesizer into `WriteStreamToCacheAsync`, extract `WriteBytesAtomicAsync`, add `LightVariantPath` static helper
- `src/Winhance.UI/Features/SoftwareApps/ViewModels/AppItemViewModel.cs` — accept `IThemeService`, resolve theme-aware path, subscribe to `ThemeChanged`, unsubscribe in `Dispose`
- `src/Winhance.UI/Features/SoftwareApps/ViewModels/WindowsAppsViewModel.cs` — accept `IThemeService` from DI, forward into `new AppItemViewModel(...)`
- `src/Winhance.UI/Features/SoftwareApps/ViewModels/ExternalAppsViewModel.cs` — accept `IThemeService` from DI, forward into `new AppItemViewModel(...)`
- `tests/Winhance.Infrastructure.Tests/Services/AppIconResolverTests.cs` — add a test that verifies a real white PNG flowing through `ResolveBatchAsync` produces a sibling `.light.png` with the expected pixel values
- `tests/Winhance.UI.Tests/ViewModels/AppItemViewModelTests.cs` — update `CreateViewModel` to inject `IThemeService` mock; add theme tests
- `tests/Winhance.UI.Tests/Services/NavBadgeServiceTests.cs` — pass `IThemeService` mock to the new `WindowsAppsViewModel`/`ExternalAppsViewModel` constructors
- `tests/Winhance.UI.Tests/Services/SelectedAppsProviderTests.cs` — pass `IThemeService` mock to the new `WindowsAppsViewModel` constructor

---

## Task 1: Add `PngTestHelper` for synthesizing real PNG bytes

**Why first:** Tasks 2 and 3 assert on real bitmap decoding. Garbage byte strings (the pattern in existing `AppIconResolverTests`) won't satisfy `BitmapDecoder`. We need a tiny helper that produces well-formed PNG bytes from a per-pixel BGRA painter so the synthesizer tests can express scenarios cleanly.

**Files:**
- Create: `tests/Winhance.Infrastructure.Tests/Helpers/PngTestHelper.cs`

- [ ] **Step 1: Create the helper file**

```csharp
// tests/Winhance.Infrastructure.Tests/Helpers/PngTestHelper.cs
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Winhance.Infrastructure.Tests.Helpers;

/// <summary>
/// Synthesizes well-formed PNG bytes in-memory for tests that exercise the
/// resolver's BitmapDecoder pipeline. Each pixel is BGRA (matches what the
/// resolver reads with BitmapPixelFormat.Bgra8).
/// </summary>
public static class PngTestHelper
{
    public delegate (byte B, byte G, byte R, byte A) PixelPainter(int x, int y);

    public static async Task<byte[]> MakePngAsync(int width, int height, PixelPainter paint)
    {
        var pixels = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var (b, g, r, a) = paint(x, y);
                int i = (y * width + x) * 4;
                pixels[i + 0] = b;
                pixels[i + 1] = g;
                pixels[i + 2] = r;
                pixels[i + 3] = a;
            }
        }

        using var stream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Straight,
            (uint)width,
            (uint)height,
            96.0, 96.0,
            pixels);
        await encoder.FlushAsync();

        stream.Seek(0);
        using var managed = stream.AsStreamForRead();
        using var collector = new MemoryStream();
        await managed.CopyToAsync(collector);
        return collector.ToArray();
    }

    public static Task<byte[]> MakeSolidPngAsync(int width, int height, byte r, byte g, byte b, byte a = 0xFF) =>
        MakePngAsync(width, height, (_, _) => (b, g, r, a));
}
```

- [ ] **Step 2: Smoke test the helper**

Create `tests/Winhance.Infrastructure.Tests/Helpers/PngTestHelperTests.cs`:

```csharp
using System.Runtime.InteropServices.WindowsRuntime;
using FluentAssertions;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Xunit;

namespace Winhance.Infrastructure.Tests.Helpers;

public class PngTestHelperTests
{
    [Fact]
    public async Task MakeSolidPngAsync_ProducesDecodablePngWithExpectedPixels()
    {
        var bytes = await PngTestHelper.MakeSolidPngAsync(4, 4, 0xFF, 0xFF, 0xFF);

        using var stream = new InMemoryRandomAccessStream();
        await stream.WriteAsync(bytes.AsBuffer());
        stream.Seek(0);

        var decoder = await BitmapDecoder.CreateAsync(stream);
        decoder.PixelWidth.Should().Be(4);
        decoder.PixelHeight.Should().Be(4);

        var sw = await decoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight);
        var buffer = new Windows.Storage.Streams.Buffer((uint)(sw.PixelWidth * sw.PixelHeight * 4));
        sw.CopyToBuffer(buffer);
        var pixels = buffer.ToArray();

        // BGRA at (0,0) of an opaque-white image: 0xFF, 0xFF, 0xFF, 0xFF
        pixels[0].Should().Be(0xFF);
        pixels[1].Should().Be(0xFF);
        pixels[2].Should().Be(0xFF);
        pixels[3].Should().Be(0xFF);
    }
}
```

- [ ] **Step 3: Verify tests via reviewer sub-agent**

We do NOT run `dotnet test` from this Linux host. Note this commit; verification of the smoke test runs as part of Task 7's reviewer-agent dispatch.

- [ ] **Step 4: Commit**

```bash
cd /srv/projects/winhance
git add tests/Winhance.Infrastructure.Tests/Helpers/PngTestHelper.cs \
        tests/Winhance.Infrastructure.Tests/Helpers/PngTestHelperTests.cs
git commit -m "test: add PngTestHelper for synthesizing real PNG bytes in tests"
```

---

## Task 2: `LightVariantSynthesizer` — detect monochrome-white and recolor

**Files:**
- Create: `src/Winhance.Infrastructure/Features/SoftwareApps/Services/LightVariantSynthesizer.cs`
- Create: `tests/Winhance.Infrastructure.Tests/Services/LightVariantSynthesizerTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/Winhance.Infrastructure.Tests/Services/LightVariantSynthesizerTests.cs
using System.Runtime.InteropServices.WindowsRuntime;
using FluentAssertions;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Winhance.Infrastructure.Features.SoftwareApps.Services;
using Winhance.Infrastructure.Tests.Helpers;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class LightVariantSynthesizerTests
{
    [Fact]
    public async Task TryGenerateAsync_SolidWhiteOpaque_ReturnsRecoloredBytes()
    {
        var input = await PngTestHelper.MakeSolidPngAsync(8, 8, 0xFF, 0xFF, 0xFF);

        var output = await LightVariantSynthesizer.TryGenerateAsync(input, CancellationToken.None);

        output.Should().NotBeNull();
        var (r, g, b, a) = await SamplePixelAsync(output!, 0, 0);
        r.Should().Be(0x1F);
        g.Should().Be(0x1F);
        b.Should().Be(0x1F);
        a.Should().Be(0xFF);
    }

    [Fact]
    public async Task TryGenerateAsync_SolidSaturatedGreen_ReturnsNull()
    {
        var input = await PngTestHelper.MakeSolidPngAsync(8, 8, 0x10, 0xC0, 0x20);

        var output = await LightVariantSynthesizer.TryGenerateAsync(input, CancellationToken.None);

        output.Should().BeNull();
    }

    [Fact]
    public async Task TryGenerateAsync_MixedWhiteAndSaturatedAccent_ReturnsNull()
    {
        // 8x8 image: 75% white pixels + 25% saturated red. Mean saturation
        // pulls above MonochromeMaxSaturation (0.15) so detection rejects it.
        var input = await PngTestHelper.MakePngAsync(8, 8, (x, y) =>
            (x < 6) ? ((byte)0xFF, (byte)0xFF, (byte)0xFF, (byte)0xFF)
                    : ((byte)0x00, (byte)0x00, (byte)0xC0, (byte)0xFF));

        var output = await LightVariantSynthesizer.TryGenerateAsync(input, CancellationToken.None);

        output.Should().BeNull();
    }

    [Fact]
    public async Task TryGenerateAsync_FullyTransparent_ReturnsNull()
    {
        var input = await PngTestHelper.MakeSolidPngAsync(8, 8, 0xFF, 0xFF, 0xFF, a: 0x00);

        var output = await LightVariantSynthesizer.TryGenerateAsync(input, CancellationToken.None);

        output.Should().BeNull();
    }

    [Fact]
    public async Task TryGenerateAsync_WhiteWithAntialiasedEdge_PreservesAlphaInRecolor()
    {
        // 4x4 image: opaque white center pixel (1,1), feathered edge pixel
        // (0,0) at alpha=80. After recolor: (1,1) should be #1F1F1F/0xFF,
        // (0,0) should be #1F1F1F/0x50 — RGB replaced, alpha preserved.
        var input = await PngTestHelper.MakePngAsync(4, 4, (x, y) =>
        {
            if (x == 1 && y == 1) return ((byte)0xFF, (byte)0xFF, (byte)0xFF, (byte)0xFF);
            if (x == 0 && y == 0) return ((byte)0xFF, (byte)0xFF, (byte)0xFF, (byte)0x50);
            return ((byte)0x00, (byte)0x00, (byte)0x00, (byte)0x00);
        });

        var output = await LightVariantSynthesizer.TryGenerateAsync(input, CancellationToken.None);

        output.Should().NotBeNull();
        var center = await SamplePixelAsync(output!, 1, 1);
        center.Should().Be(((byte)0x1F, (byte)0x1F, (byte)0x1F, (byte)0xFF));
        var edge = await SamplePixelAsync(output!, 0, 0);
        edge.Should().Be(((byte)0x1F, (byte)0x1F, (byte)0x1F, (byte)0x50));
    }

    private static async Task<(byte R, byte G, byte B, byte A)> SamplePixelAsync(byte[] pngBytes, int x, int y)
    {
        using var stream = new InMemoryRandomAccessStream();
        await stream.WriteAsync(pngBytes.AsBuffer());
        stream.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(stream);
        var sw = await decoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight);
        var buffer = new Windows.Storage.Streams.Buffer((uint)(sw.PixelWidth * sw.PixelHeight * 4));
        sw.CopyToBuffer(buffer);
        var pixels = buffer.ToArray();
        int i = (y * (int)sw.PixelWidth + x) * 4;
        return (pixels[i + 2], pixels[i + 1], pixels[i + 0], pixels[i + 3]);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail with compilation errors**

Skipped on this host — verification batched into Task 7. Expected failure: `LightVariantSynthesizer` does not exist yet.

- [ ] **Step 3: Implement `LightVariantSynthesizer`**

```csharp
// src/Winhance.Infrastructure/Features/SoftwareApps/Services/LightVariantSynthesizer.cs
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

/// <summary>
/// Detects "monochrome-white" PNG icons and produces a darkened companion
/// PNG with all opaque RGB replaced by <see cref="LightVariantTargetColor"/>.
/// Alpha is preserved per-pixel so anti-aliased edges survive intact.
///
/// Caller is <see cref="AppIconResolver.WriteStreamToCacheAsync"/>, which
/// writes the result alongside the primary cache file as <c>&lt;name&gt;.light.png</c>.
/// </summary>
public static class LightVariantSynthesizer
{
    // Target color for the synthesized variant. Sampled from Win11's own
    // `lightunplated` AppX renders in Settings → Apps (typical range
    // #1A1A1A to #2A2A2A). Looks like a real dark icon, not a washed-out grey.
    // If this changes meaningfully, manually wipe %ProgramData%\Winhance\IconCache.
    private static readonly (byte R, byte G, byte B) LightVariantTargetColor = (0x1F, 0x1F, 0x1F);

    // Same threshold the trim step uses — measure the visible silhouette,
    // ignore antialiasing halo so a soft-edge white icon isn't misclassified
    // by the halo dragging mean saturation around.
    private const byte AlphaDetectionThreshold = 32;

    // Detection thresholds. An icon counts as monochrome-white if the mean
    // HSL lightness of its opaque pixels exceeds MonochromeMinLightness AND
    // the mean saturation falls below MonochromeMaxSaturation. Tuned to catch
    // white vendor marks (GitHub Desktop, etc.) while leaving colored logos
    // and partially-colored marks alone.
    private const double MonochromeMinLightness = 0.85;
    private const double MonochromeMaxSaturation = 0.15;

    /// <summary>
    /// Returns recolored PNG bytes if the input is a monochrome-white icon,
    /// otherwise <c>null</c>. Errors during decode/encode return <c>null</c>
    /// (the resolver treats this as "no variant generated").
    /// </summary>
    public static async Task<byte[]?> TryGenerateAsync(byte[] primaryBytes, CancellationToken ct)
    {
        if (primaryBytes is null || primaryBytes.Length == 0) return null;

        try
        {
            using var inStream = new InMemoryRandomAccessStream();
            await inStream.WriteAsync(primaryBytes.AsBuffer());
            inStream.Seek(0);

            var decoder = await BitmapDecoder.CreateAsync(inStream);

            // Straight (not premultiplied) — we replace raw RGB values; premultiplied
            // storage would force a denormalize step first.
            var sw = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight);

            int width = (int)sw.PixelWidth;
            int height = (int)sw.PixelHeight;
            if (width <= 0 || height <= 0) return null;

            var buffer = new Windows.Storage.Streams.Buffer((uint)(width * height * 4));
            sw.CopyToBuffer(buffer);
            var pixels = buffer.ToArray();

            if (!IsMonochromeLight(pixels))
                return null;

            RecolorOpaquePixels(pixels, LightVariantTargetColor);

            sw.CopyFromBuffer(pixels.AsBuffer());

            using var outStream = new InMemoryRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, outStream);
            encoder.SetSoftwareBitmap(sw);
            await encoder.FlushAsync();

            outStream.Seek(0);
            using var managed = outStream.AsStreamForRead();
            using var collector = new MemoryStream();
            await managed.CopyToAsync(collector, ct).ConfigureAwait(false);
            return collector.ToArray();
        }
        catch
        {
            // Bad PNG, decoder failure, or anything else — caller treats as no variant.
            return null;
        }
    }

    private static bool IsMonochromeLight(byte[] pixels)
    {
        double sumLightness = 0;
        double sumSaturation = 0;
        int opaqueCount = 0;

        for (int i = 0; i < pixels.Length; i += 4)
        {
            byte alpha = pixels[i + 3];
            if (alpha <= AlphaDetectionThreshold) continue;

            byte b = pixels[i + 0];
            byte g = pixels[i + 1];
            byte r = pixels[i + 2];
            var (l, s) = RgbToLightnessSaturation(r, g, b);
            sumLightness += l;
            sumSaturation += s;
            opaqueCount++;
        }

        if (opaqueCount == 0) return false;

        double meanLightness = sumLightness / opaqueCount;
        double meanSaturation = sumSaturation / opaqueCount;

        return meanLightness > MonochromeMinLightness
            && meanSaturation < MonochromeMaxSaturation;
    }

    private static void RecolorOpaquePixels(byte[] pixels, (byte R, byte G, byte B) target)
    {
        for (int i = 0; i < pixels.Length; i += 4)
        {
            // alpha > 0 (not the detection threshold): recolor antialiasing halo
            // too, otherwise the dark silhouette ends up rimmed by a faint white
            // glow in light mode.
            if (pixels[i + 3] == 0) continue;

            pixels[i + 0] = target.B;
            pixels[i + 1] = target.G;
            pixels[i + 2] = target.R;
        }
    }

    /// <summary>
    /// Standard HSL conversion (Wikipedia). Returns (lightness, saturation),
    /// each in [0, 1]. Hue isn't needed for the detection so we skip it.
    /// </summary>
    private static (double L, double S) RgbToLightnessSaturation(byte rByte, byte gByte, byte bByte)
    {
        double r = rByte / 255.0;
        double g = gByte / 255.0;
        double b = bByte / 255.0;

        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double l = (max + min) / 2.0;

        double s;
        if (max == min)
        {
            s = 0;
        }
        else
        {
            double d = max - min;
            s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);
        }

        return (l, s);
    }
}
```

- [ ] **Step 4: Verify tests via reviewer (Task 7)**

Skipped on this host.

- [ ] **Step 5: Commit**

```bash
cd /srv/projects/winhance
git add src/Winhance.Infrastructure/Features/SoftwareApps/Services/LightVariantSynthesizer.cs \
        tests/Winhance.Infrastructure.Tests/Services/LightVariantSynthesizerTests.cs
git commit -m "feat: add LightVariantSynthesizer for monochrome-white icon recolor"
```

---

## Task 3: Wire `LightVariantSynthesizer` into `AppIconResolver`

**Files:**
- Modify: `src/Winhance.Infrastructure/Features/SoftwareApps/Services/AppIconResolver.cs` (around lines 638-648 — `WriteStreamToCacheAsync`)
- Modify: `tests/Winhance.Infrastructure.Tests/Services/AppIconResolverTests.cs` (add integration test)

- [ ] **Step 1: Write the failing integration test**

Append to `tests/Winhance.Infrastructure.Tests/Services/AppIconResolverTests.cs`:

```csharp
[Fact]
public async Task ResolveBatchAsync_WhiteAppxIcon_WritesLightVariantSibling()
{
    var def = Def("white-app", appxName: "Vendor.WhiteApp");
    var fullName = "Vendor.WhiteApp_1.0.0_x64__abc";
    var whitePngBytes = await PngTestHelper.MakeSolidPngAsync(16, 16, 0xFF, 0xFF, 0xFF);

    _mockIconSource.Setup(s => s.GetInstalledPackageMapAsync(It.IsAny<CancellationToken>()))
        .ReturnsAsync(new Dictionary<string, string> { ["Vendor.WhiteApp"] = fullName });
    _mockIconSource.Setup(s => s.GetLogoStreamAsync(fullName, It.IsAny<Size>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new MemoryStream(whitePngBytes));

    await _resolver.ResolveBatchAsync(new[] { def });

    def.IconPath.Should().NotBeNull();
    var primaryPath = def.IconPath!;
    var lightPath = Path.ChangeExtension(primaryPath, null) + ".light.png";
    File.Exists(lightPath).Should().BeTrue("a monochrome-white primary must produce a .light.png sibling");

    // Sibling decodes back to #1F1F1F opaque pixels.
    var lightBytes = await File.ReadAllBytesAsync(lightPath);
    using var stream = new InMemoryRandomAccessStream();
    await stream.WriteAsync(lightBytes.AsBuffer());
    stream.Seek(0);
    var decoder = await BitmapDecoder.CreateAsync(stream);
    var sw = await decoder.GetSoftwareBitmapAsync(
        BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight);
    var buffer = new Windows.Storage.Streams.Buffer((uint)(sw.PixelWidth * sw.PixelHeight * 4));
    sw.CopyToBuffer(buffer);
    var pixels = buffer.ToArray();
    pixels[0].Should().Be(0x1F);   // B
    pixels[1].Should().Be(0x1F);   // G
    pixels[2].Should().Be(0x1F);   // R
    pixels[3].Should().Be(0xFF);   // A
}

[Fact]
public async Task ResolveBatchAsync_ColoredAppxIcon_DoesNotWriteLightVariant()
{
    var def = Def("green-app", appxName: "Vendor.GreenApp");
    var fullName = "Vendor.GreenApp_1.0.0_x64__abc";
    var greenPngBytes = await PngTestHelper.MakeSolidPngAsync(16, 16, 0x10, 0xC0, 0x20);

    _mockIconSource.Setup(s => s.GetInstalledPackageMapAsync(It.IsAny<CancellationToken>()))
        .ReturnsAsync(new Dictionary<string, string> { ["Vendor.GreenApp"] = fullName });
    _mockIconSource.Setup(s => s.GetLogoStreamAsync(fullName, It.IsAny<Size>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new MemoryStream(greenPngBytes));

    await _resolver.ResolveBatchAsync(new[] { def });

    def.IconPath.Should().NotBeNull();
    var lightPath = Path.ChangeExtension(def.IconPath!, null) + ".light.png";
    File.Exists(lightPath).Should().BeFalse("colored icons get no .light.png");
}
```

The test file needs new `using` directives at the top:

```csharp
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Winhance.Infrastructure.Tests.Helpers;
```

- [ ] **Step 2: Modify `WriteStreamToCacheAsync` and add helpers**

In `src/Winhance.Infrastructure/Features/SoftwareApps/Services/AppIconResolver.cs`, replace the existing `WriteStreamToCacheAsync` method (current lines 638-648) with:

```csharp
private async Task WriteStreamToCacheAsync(Stream source, string cachePath, CancellationToken ct)
{
    var sourceBytes = await ReadAllBytesAsync(source, ct).ConfigureAwait(false);
    var primaryBytes = await TryTrimTransparentBordersAsync(sourceBytes, ct).ConfigureAwait(false)
                      ?? sourceBytes;

    await WriteBytesAtomicAsync(cachePath, primaryBytes, ct).ConfigureAwait(false);

    var lightBytes = await LightVariantSynthesizer.TryGenerateAsync(primaryBytes, ct).ConfigureAwait(false);
    if (lightBytes is not null)
    {
        await WriteBytesAtomicAsync(LightVariantPath(cachePath), lightBytes, ct).ConfigureAwait(false);
    }
}

private static async Task WriteBytesAtomicAsync(string path, byte[] bytes, CancellationToken ct)
{
    var tmpPath = path + ".tmp";
    await File.WriteAllBytesAsync(tmpPath, bytes, ct).ConfigureAwait(false);
    File.Move(tmpPath, path, overwrite: true);
}

/// <summary>
/// Sibling path for the light-mode variant of <paramref name="primaryPath"/>.
/// Replaces the trailing <c>.png</c> with <c>.light.png</c>. Pairs with the
/// primary on prune since both share the <c>&lt;id&gt;.</c> prefix used by
/// <see cref="PruneOldVersions"/>.
/// </summary>
internal static string LightVariantPath(string primaryPath) =>
    Path.ChangeExtension(primaryPath, null) + ".light.png";
```

`internal` on `LightVariantPath` (rather than `private`) so the UI project's path-resolution logic in Task 4 can reference the same single source of truth — see Task 4 Step 2.

Verify the existing import list at the top of the file already includes `System.IO`; it does (`using System.IO;` line 4). No new imports needed for this task.

- [ ] **Step 3: Add `InternalsVisibleTo` for the Winhance.UI assembly**

`LightVariantPath` is `internal` and needs to be reachable from the UI project. Find or create `src/Winhance.Infrastructure/Properties/AssemblyInfo.cs`. If it exists, append the attribute; otherwise create the file with:

```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Winhance.UI")]
[assembly: InternalsVisibleTo("Winhance.Infrastructure.Tests")]
```

Before creating, check whether the project already exposes internals to the tests assembly — if `InternalsVisibleTo("Winhance.Infrastructure.Tests")` already exists somewhere, just add the `Winhance.UI` line next to it. Run:

```bash
grep -rn "InternalsVisibleTo" /srv/projects/winhance/src/Winhance.Infrastructure/ 2>/dev/null
```

If it's already on the `.csproj` as an MSBuild item (`<ItemGroup><InternalsVisibleTo Include="..." /></ItemGroup>`), add `Winhance.UI` there instead. Pick whichever form is already in use.

- [ ] **Step 4: Verify tests via reviewer (Task 7)**

Skipped on this host.

- [ ] **Step 5: Commit**

```bash
cd /srv/projects/winhance
git add src/Winhance.Infrastructure/Features/SoftwareApps/Services/AppIconResolver.cs \
        src/Winhance.Infrastructure/Properties/AssemblyInfo.cs \
        tests/Winhance.Infrastructure.Tests/Services/AppIconResolverTests.cs
git commit -m "feat: wire LightVariantSynthesizer into AppIconResolver write path"
```

If `AssemblyInfo.cs` was an edit on the `.csproj` instead, swap the path appropriately. If neither was needed (Winhance.UI doesn't end up referencing `LightVariantPath` because Task 4 chooses to inline the path-derivation logic — see the open decision in Task 4 Step 2), drop the file from the `git add` line.

---

## Task 4: `AppItemViewModel` — theme-aware path resolution + ThemeChanged subscription

**Files:**
- Modify: `src/Winhance.UI/Features/SoftwareApps/ViewModels/AppItemViewModel.cs`
- Modify: `tests/Winhance.UI.Tests/ViewModels/AppItemViewModelTests.cs`

- [ ] **Step 1: Update test fixture to inject `IThemeService`**

In `tests/Winhance.UI.Tests/ViewModels/AppItemViewModelTests.cs`, update the fixture:

```csharp
using Microsoft.UI.Xaml;
// ... existing usings ...
using Winhance.UI.Features.Common.Interfaces;
// ...

public class AppItemViewModelTests
{
    private readonly Mock<ILocalizationService> _mockLocalization = new();
    private readonly Mock<IDispatcherService> _mockDispatcher = new();
    private readonly Mock<IThemeService> _mockThemeService = new();
    private readonly ItemDefinition _defaultDefinition;

    public AppItemViewModelTests()
    {
        _mockDispatcher
            .Setup(d => d.RunOnUIThread(It.IsAny<Action>()))
            .Callback<Action>(action => action());

        _mockLocalization
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);

        // Default to dark theme so existing tests are unaffected.
        _mockThemeService
            .Setup(t => t.GetEffectiveTheme())
            .Returns(ElementTheme.Dark);

        _defaultDefinition = new ItemDefinition
        {
            Id = "test-app",
            Name = "Test App",
            Description = "A test application",
            GroupName = "TestGroup",
            IsInstalled = false,
            CanBeReinstalled = true,
        };
    }

    private AppItemViewModel CreateViewModel(ItemDefinition? definition = null)
    {
        return new AppItemViewModel(
            definition ?? _defaultDefinition,
            _mockLocalization.Object,
            _mockDispatcher.Object,
            _mockThemeService.Object);
    }
    // ... rest of file unchanged ...
}
```

- [ ] **Step 2: Add the failing theme tests**

Append to `AppItemViewModelTests`:

```csharp
[Fact]
public void IconSource_LightTheme_WithLightSibling_DecodesFromLightPath()
{
    var tmpDir = Path.Combine(Path.GetTempPath(), "WinhanceTest_" + Path.GetRandomFileName());
    Directory.CreateDirectory(tmpDir);
    try
    {
        var primaryPath = Path.Combine(tmpDir, "icon.f7a3.png");
        var lightPath = Path.Combine(tmpDir, "icon.f7a3.light.png");
        File.WriteAllBytes(primaryPath, MinimalPng());
        File.WriteAllBytes(lightPath, MinimalPng());

        var def = new ItemDefinition { Id = "x", Name = "X", Description = "X", IconPath = primaryPath };
        _mockThemeService.Setup(t => t.GetEffectiveTheme()).Returns(ElementTheme.Light);

        var vm = CreateViewModel(def);
        var bmp = vm.IconSource;

        bmp.Should().NotBeNull();
        bmp!.UriSource.LocalPath.Should().Be(lightPath);
    }
    finally
    {
        Directory.Delete(tmpDir, recursive: true);
    }
}

[Fact]
public void IconSource_LightTheme_NoLightSibling_FallsBackToPrimary()
{
    var tmpDir = Path.Combine(Path.GetTempPath(), "WinhanceTest_" + Path.GetRandomFileName());
    Directory.CreateDirectory(tmpDir);
    try
    {
        var primaryPath = Path.Combine(tmpDir, "icon.b8e1.png");
        File.WriteAllBytes(primaryPath, MinimalPng());

        var def = new ItemDefinition { Id = "x", Name = "X", Description = "X", IconPath = primaryPath };
        _mockThemeService.Setup(t => t.GetEffectiveTheme()).Returns(ElementTheme.Light);

        var vm = CreateViewModel(def);
        vm.IconSource!.UriSource.LocalPath.Should().Be(primaryPath);
    }
    finally
    {
        Directory.Delete(tmpDir, recursive: true);
    }
}

[Fact]
public void IconSource_DarkTheme_AlwaysUsesPrimary()
{
    var tmpDir = Path.Combine(Path.GetTempPath(), "WinhanceTest_" + Path.GetRandomFileName());
    Directory.CreateDirectory(tmpDir);
    try
    {
        var primaryPath = Path.Combine(tmpDir, "icon.f7a3.png");
        var lightPath = Path.Combine(tmpDir, "icon.f7a3.light.png");
        File.WriteAllBytes(primaryPath, MinimalPng());
        File.WriteAllBytes(lightPath, MinimalPng());

        var def = new ItemDefinition { Id = "x", Name = "X", Description = "X", IconPath = primaryPath };
        _mockThemeService.Setup(t => t.GetEffectiveTheme()).Returns(ElementTheme.Dark);

        var vm = CreateViewModel(def);
        vm.IconSource!.UriSource.LocalPath.Should().Be(primaryPath);
    }
    finally
    {
        Directory.Delete(tmpDir, recursive: true);
    }
}

[Fact]
public void ThemeChanged_RaisesPropertyChangedForIconSource()
{
    var def = new ItemDefinition { Id = "x", Name = "X", Description = "X", IconPath = "C:\\fake\\icon.png" };
    _mockThemeService.Setup(t => t.GetEffectiveTheme()).Returns(ElementTheme.Dark);

    var vm = CreateViewModel(def);

    var raised = new List<string?>();
    vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

    _mockThemeService.Raise(t => t.ThemeChanged += null, this, WinhanceTheme.LightNative);

    raised.Should().Contain(nameof(AppItemViewModel.IconSource));
}

[Fact]
public void Dispose_UnsubscribesFromThemeChanged()
{
    var def = new ItemDefinition { Id = "x", Name = "X", Description = "X", IconPath = "C:\\fake\\icon.png" };
    _mockThemeService.Setup(t => t.GetEffectiveTheme()).Returns(ElementTheme.Dark);

    var vm = CreateViewModel(def);
    vm.Dispose();

    var raised = new List<string?>();
    vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

    _mockThemeService.Raise(t => t.ThemeChanged += null, this, WinhanceTheme.LightNative);

    raised.Should().NotContain(nameof(AppItemViewModel.IconSource));
}

/// <summary>
/// 1×1 transparent PNG — small enough to roundtrip without expensive setup.
/// Just needs to exist on disk; BitmapImage construction is lazy and the
/// tests assert on UriSource, not decoded pixels.
/// </summary>
private static byte[] MinimalPng() => new byte[]
{
    0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A,
    0x00,0x00,0x00,0x0D,0x49,0x48,0x44,0x52,
    0x00,0x00,0x00,0x01,0x00,0x00,0x00,0x01,
    0x08,0x06,0x00,0x00,0x00,0x1F,0x15,0xC4,
    0x89,0x00,0x00,0x00,0x0D,0x49,0x44,0x41,
    0x54,0x78,0x9C,0x62,0x00,0x01,0x00,0x00,
    0x05,0x00,0x01,0x0D,0x0A,0x2D,0xB4,0x00,
    0x00,0x00,0x00,0x49,0x45,0x4E,0x44,0xAE,
    0x42,0x60,0x82
};
```

- [ ] **Step 3: Update `AppItemViewModel`**

In `src/Winhance.UI/Features/SoftwareApps/ViewModels/AppItemViewModel.cs`, modify:

1. Add field, ctor parameter, subscription:

```csharp
// Add to imports if not present
using Microsoft.UI.Xaml;

// In the class
private readonly IThemeService _themeService;

public AppItemViewModel(
    ItemDefinition definition,
    ILocalizationService localizationService,
    IDispatcherService dispatcherService,
    IThemeService themeService)
{
    _definition = definition;
    _localizationService = localizationService;
    _dispatcherService = dispatcherService;
    _themeService = themeService;

    _localizationService.LanguageChanged += OnLanguageChanged;
    _themeService.ThemeChanged += OnThemeChanged;
}
```

2. Replace `Dispose`:

```csharp
public void Dispose()
{
    if (!_disposed)
    {
        _localizationService.LanguageChanged -= OnLanguageChanged;
        _themeService.ThemeChanged -= OnThemeChanged;
        _disposed = true;
    }
}
```

3. Add the handler near `OnLanguageChanged`:

```csharp
private void OnThemeChanged(object? sender, WinhanceTheme theme)
{
    _dispatcherService.RunOnUIThread(() => OnPropertyChanged(nameof(IconSource)));
}
```

4. Replace the `IconSource` getter (currently lines 98-117 of `AppItemViewModel.cs`):

```csharp
public BitmapImage? IconSource
{
    get
    {
        var basePath = Definition.IconPath;
        if (string.IsNullOrEmpty(basePath))
        {
            _iconSource = null;
            _iconSourcePath = null;
            return null;
        }

        var resolvedPath = ResolveThemeAwarePath(basePath);

        if (_iconSource is not null && _iconSourcePath == resolvedPath)
            return _iconSource;

        var bmp = new BitmapImage { DecodePixelWidth = 64 };
        bmp.UriSource = new Uri(resolvedPath);
        _iconSource = bmp;
        _iconSourcePath = resolvedPath;
        return _iconSource;
    }
}

private string ResolveThemeAwarePath(string basePath)
{
    if (_themeService.GetEffectiveTheme() != ElementTheme.Light)
        return basePath;

    // Same naming convention as AppIconResolver.LightVariantPath. Kept inline
    // here (single line) rather than crossing an assembly boundary to call it.
    var lightPath = Path.ChangeExtension(basePath, null) + ".light.png";
    return File.Exists(lightPath) ? lightPath : basePath;
}
```

Note: this inlines the path derivation rather than calling `AppIconResolver.LightVariantPath`. The trade-off is two strings to keep in sync vs. an `InternalsVisibleTo` for one tiny static method. Task 3's `InternalsVisibleTo` change for `Winhance.UI` becomes unused if we go inline — drop it. (If the implementer prefers the cross-assembly call, leave `InternalsVisibleTo` and replace the line above with `AppIconResolver.LightVariantPath(basePath)`. Either is fine.) Adding `using System.IO;` at the top of the file may be needed if it's not already imported — the file currently doesn't have it; add it.

- [ ] **Step 4: Verify tests via reviewer (Task 7)**

Skipped on this host.

- [ ] **Step 5: Commit**

```bash
cd /srv/projects/winhance
git add src/Winhance.UI/Features/SoftwareApps/ViewModels/AppItemViewModel.cs \
        tests/Winhance.UI.Tests/ViewModels/AppItemViewModelTests.cs
git commit -m "feat: AppItemViewModel picks .light.png in light mode + ThemeChanged swap"
```

---

## Task 5: Forward `IThemeService` through `WindowsAppsViewModel` and `ExternalAppsViewModel`

**Files:**
- Modify: `src/Winhance.UI/Features/SoftwareApps/ViewModels/WindowsAppsViewModel.cs`
- Modify: `src/Winhance.UI/Features/SoftwareApps/ViewModels/ExternalAppsViewModel.cs`
- Modify: `tests/Winhance.UI.Tests/Services/NavBadgeServiceTests.cs`
- Modify: `tests/Winhance.UI.Tests/Services/SelectedAppsProviderTests.cs`

- [ ] **Step 1: Update `WindowsAppsViewModel`**

In `src/Winhance.UI/Features/SoftwareApps/ViewModels/WindowsAppsViewModel.cs`:

Add field and ctor parameter (around line 33):

```csharp
private readonly IThemeService _themeService;

public WindowsAppsViewModel(
    IWindowsAppsService windowsAppsService,
    IAppInstallationService appInstallationService,
    IAppUninstallationService appUninstallationService,
    ITaskProgressService progressService,
    ILogService logService,
    IDialogService dialogService,
    ILocalizationService localizationService,
    IDispatcherService dispatcherService,
    IThemeService themeService,
    IAppIconResolver? iconResolver = null)
{
    _windowsAppsService = windowsAppsService;
    _appInstallationService = appInstallationService;
    _appUninstallationService = appUninstallationService;
    _progressService = progressService;
    _logService = logService;
    _dialogService = dialogService;
    _localizationService = localizationService;
    _dispatcherService = dispatcherService;
    _themeService = themeService;
    // ... existing rest unchanged ...
}
```

Then update `LoadAppsIntoItems` (around line 261):

```csharp
private void LoadAppsIntoItems(IEnumerable<ItemDefinition> definitions)
{
    foreach (var definition in definitions)
    {
        var viewModel = new AppItemViewModel(
            definition,
            _localizationService,
            _dispatcherService,
            _themeService);
        viewModel.PropertyChanged += Item_PropertyChanged;
        Items.Add(viewModel);
    }
}
```

Add `using Winhance.UI.Features.Common.Interfaces;` to the file if `IThemeService` isn't already in scope — check the existing using block first.

- [ ] **Step 2: Update `ExternalAppsViewModel`**

Identical pattern in `src/Winhance.UI/Features/SoftwareApps/ViewModels/ExternalAppsViewModel.cs`:

```csharp
private readonly IThemeService _themeService;

public ExternalAppsViewModel(
    IExternalAppsService externalAppsService,
    ITaskProgressService progressService,
    ILogService logService,
    IDialogService dialogService,
    ILocalizationService localizationService,
    IDispatcherService dispatcherService,
    IThemeService themeService,
    IAppIconResolver? iconResolver = null)
{
    _externalAppsService = externalAppsService;
    _progressService = progressService;
    _logService = logService;
    _dialogService = dialogService;
    _localizationService = localizationService;
    _dispatcherService = dispatcherService;
    _themeService = themeService;
    _iconResolver = iconResolver;
    // ... existing rest unchanged ...
}
```

And `LoadAppsIntoItems` (around line 264):

```csharp
private void LoadAppsIntoItems(IEnumerable<ItemDefinition> definitions)
{
    foreach (var definition in definitions)
    {
        var viewModel = new AppItemViewModel(
            definition,
            _localizationService,
            _dispatcherService,
            _themeService);
        viewModel.PropertyChanged += Item_PropertyChanged;
        Items.Add(viewModel);
    }
}
```

- [ ] **Step 3: Fix existing test fixtures broken by the ctor change**

`tests/Winhance.UI.Tests/Services/NavBadgeServiceTests.cs` (around lines 34 and 46): add `IThemeService` mock to the test class and pass it to both constructors.

```csharp
private readonly Mock<IThemeService> _mockThemeService = new();

// In the constructor, after existing setup:
_mockThemeService
    .Setup(t => t.GetEffectiveTheme())
    .Returns(Microsoft.UI.Xaml.ElementTheme.Dark);

// In the WindowsAppsViewModel construction call, append _mockThemeService.Object
// as the new parameter immediately before the existing optional iconResolver
// argument. Same for ExternalAppsViewModel.
```

`tests/Winhance.UI.Tests/Services/SelectedAppsProviderTests.cs` (around line 50): same pattern — `IThemeService` mock + pass it to the `WindowsAppsViewModel` constructor.

Add `using Winhance.UI.Features.Common.Interfaces;` and `using Microsoft.UI.Xaml;` to both test files if not already present.

- [ ] **Step 4: Verify the DI container still resolves**

`IThemeService` is already registered as a singleton (`src/Winhance.UI/Features/Common/Extensions/DI/UIServicesExtensions.cs` line 45). `WindowsAppsViewModel` and `ExternalAppsViewModel` are also registered as singletons (lines 160 and 162). The default Microsoft.Extensions.DependencyInjection container resolves the new constructor parameter automatically by type — no DI registration changes needed. Confirm by reading lines 40-60 and 155-165 of `UIServicesExtensions.cs`; do not modify.

- [ ] **Step 5: Verify tests via reviewer (Task 7)**

Skipped on this host.

- [ ] **Step 6: Commit**

```bash
cd /srv/projects/winhance
git add src/Winhance.UI/Features/SoftwareApps/ViewModels/WindowsAppsViewModel.cs \
        src/Winhance.UI/Features/SoftwareApps/ViewModels/ExternalAppsViewModel.cs \
        tests/Winhance.UI.Tests/Services/NavBadgeServiceTests.cs \
        tests/Winhance.UI.Tests/Services/SelectedAppsProviderTests.cs
git commit -m "feat: forward IThemeService through Windows/External AppsViewModels"
```

---

## Task 6: Manual cache wipe documentation

**Files:**
- Modify: `src/Winhance.Infrastructure/Features/SoftwareApps/Services/AppIconResolver.cs`

- [ ] **Step 1: Add a code comment near the tuning knobs**

Find the `AlphaTrimThreshold` constant comment block (around lines 90-103) and append a similar comment near the top of `LightVariantSynthesizer.cs`, OR add a one-line note to the `AppIconResolver` cache-dir docstring referencing the synthesizer's tuning knobs. The synthesizer already has its own documented constants — Task 2 included them with the "wipe `%ProgramData%\Winhance\IconCache` if changed" note, so this step is just verifying that note exists. If it does, skip to Step 2.

- [ ] **Step 2: No code change — commit nothing**

If Step 1 only verified existing documentation, there is nothing to commit. Move on to Task 7.

---

## Task 7: Reviewer agent, manual cache wipe, hand off to Marco

**Goal:** Run the Opus-pinned compile-correctness reviewer per CLAUDE.md (Winhance cannot build on Linux). Leave commits on local `dev` unpushed for Marco to test on his Windows PC.

- [ ] **Step 1: Sanity-check the working tree**

```bash
cd /srv/projects/winhance
git status
git log --oneline origin/dev..dev
```

Expected: the log should show the spec commit + plan commit + Tasks 1, 2, 3, 4, 5 commits, in that order, on top of `origin/dev`. Marco's pre-existing unstaged changes are NOT part of this work — do not touch them.

- [ ] **Step 2: Dispatch the Opus-pinned reviewer agent**

Use the `Agent` tool with `subagent_type: general-purpose` and `model: opus`. Prompt:

```
Review the unpushed commits on local `dev` (since `origin/dev`) in the Winhance
repo at /srv/projects/winhance against the spec at
docs/superpowers/specs/2026-05-19-light-mode-app-icons-design.md.

Winhance is a WinUI 3 .NET 10 Windows app; you cannot run dotnet build or
dotnet test from this Linux host. Verify by reading. Specifically check:

1. Does LightVariantSynthesizer.TryGenerateAsync (in
   src/Winhance.Infrastructure/Features/SoftwareApps/Services/LightVariantSynthesizer.cs)
   correctly detect monochrome-white inputs and recolor opaque pixels to
   #1F1F1F while preserving alpha? Walk the HSL conversion and confirm the
   thresholds match the spec.

2. Does AppIconResolver.WriteStreamToCacheAsync produce a .light.png sibling
   when (and only when) the synthesizer returns non-null bytes?

3. Does AppItemViewModel.IconSource resolve to <basePath>.light.png in light
   mode (when the file exists), fall back to <basePath> otherwise, and
   re-key its BitmapImage memoization on the resolved path so theme flips
   produce a fresh BitmapImage?

4. Does AppItemViewModel subscribe to IThemeService.ThemeChanged in the
   constructor and unsubscribe in Dispose? Does the handler raise
   PropertyChanged for IconSource via the dispatcher?

5. Did WindowsAppsViewModel and ExternalAppsViewModel get the new
   IThemeService parameter, forward it into new AppItemViewModel(...), and
   leave all other call sites consistent? Did the test fixtures that
   construct these VMs (NavBadgeServiceTests, SelectedAppsProviderTests)
   get updated to inject an IThemeService mock?

6. Are there any compile-correctness issues — missing usings, name mismatches,
   wrong type signatures, references to undefined symbols?

7. Are the tests well-structured and covering the cases in the spec's "Test
   plan" section? Any obvious test smell or missing case?

Report in under 400 words: a punch list of issues found (file:line, what's
wrong, what to change), or "no issues" if the branch is ready.
```

- [ ] **Step 3: Address any issues the reviewer surfaces**

If the reviewer returns issues, fix them locally, commit, then re-dispatch the reviewer with a "follow-up review" prompt referencing the previous round. Loop until "no issues."

- [ ] **Step 4: Hand off to Marco**

Post a status message in the conversation:

```
Local `dev` (unpushed, ahead of origin/dev) is ready for your Windows test.

Local commits (not pushed):
  - docs spec
  - test: PngTestHelper
  - feat: LightVariantSynthesizer
  - feat: wire into AppIconResolver
  - feat: AppItemViewModel theme-aware swap
  - feat: forward IThemeService through parent VMs

Before testing: wipe %ProgramData%\Winhance\IconCache so the new write path
runs from scratch. Build on Windows, exercise the Software & Apps page in
light mode, confirm previously-invisible white icons now render as a dark
silhouette. When you're happy, tell me to push.
```

Do NOT `git push` until Marco explicitly approves.

---

## Self-Review

**Spec coverage:**

| Spec section | Implementing task |
|---|---|
| §"Approach" — detect monochrome at cache time + companion .light.png | Tasks 2-3 |
| §"File layout" — `<id>.<hash>.light.png` sibling | Task 3 (`LightVariantPath` helper) |
| §"Components and changes — A" — `WriteStreamToCacheAsync` + `WriteAtomicAsync` factor-out + `LightVariantPath` | Task 3 |
| §"Components and changes — B" — `TryGenerateLightVariantAsync` decode → detect → recolor → encode | Task 2 (named `TryGenerateAsync` on `LightVariantSynthesizer`) |
| §"Components and changes — C" — `AppItemViewModel.IconSource` theme-aware path, memoization, ThemeService subscription | Task 4 |
| §"Components and changes — D" — no XAML changes | (implicit — no task needed) |
| §"Cache invalidation" — no migration logic; manual wipe documented | Task 6 (verification) + Task 7 Step 4 (Marco's wipe) |
| §"Out of scope" — SVG/dark-mode-broken/explicit lightunplated request | (intentionally no task) |
| §"Risks" — cache size, subscription leak, partial-white icons | Addressed by design — no extra task needed |
| §"Test plan" #1 — monochrome-white detected and recolored | Task 2 test 1 + 5 |
| §"Test plan" #2 — colored icon skipped | Task 2 test 2 |
| §"Test plan" #3 — partial-white skipped | Task 2 test 3 |
| §"Test plan" #4 — empty/transparent skipped | Task 2 test 4 |
| §"Test plan" #5 — memoization keys on resolved path | Task 4 tests 1-3 (the light/dark/fallback trio implicitly exercises this) |
| §"Test plan" #6 — ThemeChanged triggers PropertyChanged | Task 4 test 4 + Dispose test |

**Placeholder scan:** none found.

**Type consistency:**
- `LightVariantSynthesizer.TryGenerateAsync(byte[], CancellationToken)` — consistent name and signature in Tasks 2, 3, 7.
- `LightVariantPath(string)` — consistent name in Tasks 3, 4 (the inline alternative noted explicitly).
- `WriteBytesAtomicAsync(string, byte[], CancellationToken)` — consistent name in Task 3.
- `ResolveThemeAwarePath(string)` — consistent name in Task 4.
- `IThemeService.GetEffectiveTheme()` / `IThemeService.ThemeChanged` — names match the live interface (verified above).
- `AppItemViewModel` ctor: `(ItemDefinition, ILocalizationService, IDispatcherService, IThemeService)` — consistent across Tasks 4 and 5 and the test fixture update.

**Notes for executor:**
- Marco's pre-existing unstaged changes (`docs/2026-05-18-registry-keypath-array.md` and `src/Winhance.UI/Winhance.UI.csproj`) are NOT part of this work. Do not stage or touch them.
- Task 7 is mandatory before Marco builds — the reviewer agent stands in for the build verification we cannot perform on Linux.
- No `git push` until Marco explicitly approves.

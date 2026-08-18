using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

/// <summary>Machine-independent conformance for <see cref="Setting.CustomStateScripts"/> -- the UN-BAKED
/// setting-level scripts the autounattend script-gen runs for a CUSTOM state (a selection whose live value matches
/// no preset option, so no state's baked ScriptEffects apply). The emitter substitutes the config item's
/// CustomStateValues into the placeholders at generation time.
///
/// This is the ONLY enforcement of CustomStateScripts for gaming-touch-keyboard-service, which is
/// precedence-corrected (CatalogDetectionModelConformanceTests.PrecedenceCorrectedIds) and therefore EXEMPT
/// from the authored-vs-converter structural gate - without it that setting's script payload has ZERO coverage.
///
/// The catalog is the source of truth (the authored bodies are Windows-validated), so what this pins is the
/// DURABLE contract:
///   1. EXACTLY these 4 settings carry CustomStateScripts, with exactly these per-setting counts (5 entries).
///      A new script-bearing selection must therefore surface HERE, so its custom-state home gets authored.
///   2. Each script is non-empty and carries its authored RunContext.
///   3. THE LOAD-BEARING ONE: gaming-dns-server's scripts keep their placeholders INTACT ({{primary}},
///      {{secondary}}, {{dohtemplate}}) -- i.e. they are the RAW scripts, never an option-BAKED body. If someone
///      ever authored the baked form, custom-state generation would hard-code a preset DNS instead of writing the
///      user's own custom values, silently. That is exactly the regression worth catching, and it
///      is fully checkable against the catalog alone.
///   4. The complement: no OTHER setting carries any.</summary>
public class CatalogCustomStateScriptsConformanceTests
{
    /// <summary>(settingId, script index) -> the authored RunContext. The shipped population, exhaustively.</summary>
    private static readonly (string Id, int Count, RunContext[] Runs)[] Expected =
    {
        ("explorer-customization-shortcut-arrow", 1, new[] { RunContext.System }),
        ("gaming-dns-server", 2, new[] { RunContext.User, RunContext.User }),
        ("gaming-touch-keyboard-service", 1, new[] { RunContext.System }),
        ("taskbar-system-tray-icons-11", 1, new[] { RunContext.User }),
    };

    [Fact]
    public void CustomStateScripts_AreCarriedByExactlyTheScriptBearingSelections()
    {
        var carriers = SettingCatalog.All
            .Where(s => s.CustomStateScripts.Count > 0)
            .OrderBy(s => s.Id, System.StringComparer.Ordinal)
            .ToList();

        Assert.Equal(Expected.Select(e => e.Id).OrderBy(x => x, System.StringComparer.Ordinal).ToArray(),
            carriers.Select(s => s.Id).ToArray());
        Assert.Equal(5, carriers.Sum(s => s.CustomStateScripts.Count));

        foreach (var (id, count, runs) in Expected)
        {
            var setting = SettingCatalog.Find(id);
            Assert.NotNull(setting);
            Assert.Equal(count, setting!.CustomStateScripts.Count);
            for (int i = 0; i < count; i++)
            {
                var fx = setting.CustomStateScripts[i];
                Assert.False(string.IsNullOrWhiteSpace(fx.Script), $"{id}[{i}]: empty custom-state script");
                Assert.Equal(runs[i], fx.Run);
            }
        }
    }

    /// <summary>The un-baked guard. gaming-dns-server is the only setting whose custom-state scripts are
    /// parameterised; its placeholders MUST survive into the catalog, or the emitter would substitute nothing and
    /// the generated script would carry a hard-coded preset DNS instead of the user's custom values.
    ///
    /// Asserted PER ENTRY, deliberately: a blob-level Contains() would let the important half through. entry[1]
    /// (the DoH script) carries all three placeholders, so baking entry[0] -- the script that actually writes the
    /// adapter's servers -- would leave a joined blob still containing all three and pass GREEN. Per-entry also
    /// pins the emit ORDER, which the RunContext table cannot (both entries are RunContext.User).</summary>
    [Fact]
    public void DnsServer_CustomStateScripts_KeepTheirPlaceholders_NeverAnOptionBakedBody()
    {
        var setting = SettingCatalog.Find("gaming-dns-server");
        Assert.NotNull(setting);
        Assert.Equal(2, setting!.CustomStateScripts.Count);

        var setDns = setting.CustomStateScripts[0].Script;   // Set-DnsClientServerAddress
        var doh = setting.CustomStateScripts[1].Script;      // netsh dns add encryption

        // entry[0] writes the adapter's servers: it MUST carry primary+secondary, and must NOT carry the DoH
        // template -- that absence is what pins the two entries' order.
        AssertRaw(setDns, 0, "{{primary}}");
        AssertRaw(setDns, 0, "{{secondary}}");
        Assert.False(setDns.Contains("{{dohtemplate}}"),
            "gaming-dns-server CustomStateScripts[0] carries the DoH template -- the two scripts look reordered.");

        // entry[1] (DoH) carries all three.
        AssertRaw(doh, 1, "{{primary}}");
        AssertRaw(doh, 1, "{{secondary}}");
        AssertRaw(doh, 1, "{{dohtemplate}}");
    }

    private static void AssertRaw(string body, int index, string placeholder) =>
        Assert.True(body.Contains(placeholder),
            $"gaming-dns-server CustomStateScripts[{index}] lost the {placeholder} placeholder -- it looks OPTION-BAKED. "
                + "Custom-state generation would then write a preset DNS instead of the user's own values.");
}

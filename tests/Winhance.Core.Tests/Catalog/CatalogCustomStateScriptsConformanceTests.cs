using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

// The ONLY enforcement of CustomStateScripts for gaming-touch-keyboard-service (precedence-corrected, so exempt
// from the structural gate). Pins the durable contract: exactly these 4 settings carry CustomStateScripts (5
// entries), each non-empty with its RunContext; gaming-dns-server's keep their placeholders INTACT ({{primary}},
// {{secondary}}, {{dohtemplate}}) - a baked body would hard-code a preset DNS instead of the user's custom
// values, silently; and no other setting carries any.
public class CatalogCustomStateScriptsConformanceTests
{
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

    // Asserted PER ENTRY on purpose: entry[1] (DoH) carries all three placeholders, so baking entry[0] - the script
    // that actually writes the adapter's servers - would leave a joined blob still containing all three and pass
    // GREEN. Per-entry also pins the emit ORDER.
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

using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

// The ONLY enforcement of CustomStateScripts for gaming-touch-keyboard-service (precedence-corrected, so exempt
// from the structural gate). Pins the durable contract: exactly these settings carry CustomStateScripts, each
// non-empty with its RunContext; the DNS one keeps its {{servers}} placeholder INTACT and the album's keeps {{value}}
// - a baked body would hard-code a preset DNS, or one machine's folder, instead of the user's own, silently; and no
// other setting carries any.
public class CatalogCustomStateScriptsConformanceTests
{
    private static readonly (string Id, int Count, RunContext[] Runs)[] Expected =
    {
        ("autounattend-user-folders", 1, new[] { RunContext.User }),
        ("explorer-customization-shortcut-arrow", 1, new[] { RunContext.System }),
        ("gaming-dns-server", 1, new[] { RunContext.User }),
        ("gaming-touch-keyboard-service", 1, new[] { RunContext.System }),
        ("taskbar-system-tray-icons-11", 1, new[] { RunContext.User }),
        ("theme-wallpaper-album", 1, new[] { RunContext.User }),
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
        Assert.Equal(Expected.Sum(e => e.Count), carriers.Sum(s => s.CustomStateScripts.Count));

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

    // The answer file fills the server list on the new install, where the adapters exist.
    [Fact]
    public void The_dns_script_keeps_its_server_list_placeholder()
        => Assert.Contains("$servers = @({{servers}})", SettingCatalog.Find("gaming-dns-server")!.CustomStateScripts[0].Script);

    // It writes what the live apply writes, never the netsh table that holds Windows' own DoH templates.
    [Fact]
    public void The_dns_script_writes_the_adapter_registry_and_nudges_the_dns_client()
    {
        var script = SettingCatalog.Find("gaming-dns-server")!.CustomStateScripts[0].Script;

        Assert.DoesNotContain("netsh", script);
        Assert.Contains(@"Tcpip6\Parameters\Interfaces", script);
        Assert.Contains("DohInterfaceSettings", script);
        Assert.Contains("sc.exe control dnscache paramchange", script);
    }

    [Fact]
    public void The_album_script_keeps_its_value_placeholder()
    {
        var setting = SettingCatalog.Find("theme-wallpaper-album");
        Assert.NotNull(setting);
        Assert.Contains("{{value}}", setting!.CustomStateScripts[0].Script);
    }

    // ScriptPreambleSectionTests owns the here-string terminator invariant for Add-WinhanceDesktopType.
    [Fact]
    public void The_album_script_compiles_the_interop_through_the_shared_helper()
    {
        var script = SettingCatalog.Find("theme-wallpaper-album")!.CustomStateScripts[0].Script;

        Assert.Contains("Add-WinhanceDesktopType", script);
        Assert.DoesNotContain("Add-Type", script);
    }
}

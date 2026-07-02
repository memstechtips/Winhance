using System.Linq;
using Microsoft.Win32;
using Winhance.Core.Features.Optimize.Models;
using Xunit;

namespace Winhance.Infrastructure.Tests.Migration;

/// <summary>Locks the load-bearing invariant the catalog existence filter relies on: EVERY powercfg
/// EnablementRegistrySetting (the "unhide this hidden power setting" write) writes the SAME constant -
/// DWord "Attributes" = 0. The new catalog model keeps only a bare RegTarget (EnablementKey: path + name +
/// type) and NOT the write value, so the existence filter reproduces the write as SetValue(path, "Attributes",
/// 0, DWord). If a future enablement ever uses a different value/name/type, THIS fails - a signal that the
/// existence filter (and possibly the PowerCfgTarget model) must carry the enablement value explicitly.</summary>
public class PowerEnablementConstantTests
{
    [Fact]
    public void Every_power_enablement_writes_the_constant_unhide_value()
    {
        var enablements = PowerOptimizations.GetPowerOptimizations().Settings
            .Where(d => d.PowerCfgSettings != null)
            .SelectMany(d => d.PowerCfgSettings!)
            .Select(p => p.EnablementRegistrySetting)
            .Where(e => e != null)
            .Select(e => e!)
            .ToList();

        Assert.NotEmpty(enablements); // guards against a scoping regression that would vacuously pass
        foreach (var e in enablements)
        {
            Assert.Equal("Attributes", e.ValueName);
            Assert.Equal(RegistryValueKind.DWord, e.ValueType);
            Assert.NotNull(e.EnabledValue);
            Assert.Single(e.EnabledValue!);
            Assert.Equal(0, Assert.IsType<int>(e.EnabledValue![0]));
        }
    }
}

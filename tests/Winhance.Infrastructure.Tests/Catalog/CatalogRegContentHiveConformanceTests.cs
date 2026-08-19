using Winhance.Core.Features.Common.Catalog;
using Winhance.Infrastructure.Features.AdvancedTools.Helpers;
using Xunit;

namespace Winhance.Infrastructure.Tests.Catalog;

// ApplyOpScriptEmitter routes each .reg block to ONE autounattend pass, so a block carrying both HKCU and a
// system-hive header would lose half its content under the hive filter - it throws instead of emitting that.
// Nothing in the catalog mixes hives today, and only a synthetic block covered the guard: an author who mixed
// them would ship green tests and a hard failure in front of the user.
// Run: winhance-harness CatalogRegContentHiveConformanceTests
public class CatalogRegContentHiveConformanceTests
{
    [Fact]
    public void No_catalog_reg_content_block_mixes_HKCU_with_a_system_hive()
    {
        var offenders = new SortedSet<string>(StringComparer.Ordinal);
        int inspected = 0;

        void Inspect(string owner, IReadOnlyList<Effect> effects)
        {
            foreach (var reg in effects.OfType<RegContentEffect>())
            {
                if (string.IsNullOrEmpty(reg.Content)) continue;
                inspected++;
                if (ApplyOpScriptEmitter.MixesHives(reg.Content))
                    offenders.Add(owner);
            }
        }

        foreach (var setting in SettingCatalog.All)
        {
            Inspect(setting.Id, setting.Effects);
            foreach (var state in setting.States)
                Inspect($"{setting.Id} [{state.Label}]", state.Effects);
        }

        Assert.True(inspected > 0,
            "no RegContentEffect bodies were reached - the catalog walk regressed, which is not a clean result.");

        Assert.True(offenders.Count == 0,
            "A .reg block mixing HKEY_CURRENT_USER with a system hive throws when the autounattend is generated. "
            + "Split it into one RegContentEffect per hive:\n"
            + string.Join("\n", offenders.Select(o => $"  {o}")));
    }
}

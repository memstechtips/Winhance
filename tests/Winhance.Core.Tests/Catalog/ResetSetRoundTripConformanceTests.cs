using Winhance.Core.Features.Common.Catalog;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

// A profile switch skips a setting only when it detects as the profile's state without the fallback answering.
// A Windows-default state writes its ResetSet on every apply, so what the reset leaves behind must satisfy the
// same state's Set, or the setting reads back through the fallback and every switch re-applies it.
public class ResetSetRoundTripConformanceTests
{
    [Fact]
    public void ResetSetWrites_AreAcceptedByTheSameStatesSet()
    {
        var failures = new List<string>();
        foreach (var setting in SettingCatalog.All)
        {
            foreach (var state in setting.States)
            {
                if (state.ResetSet is null || !state.Roles.Any(r => r.Kind == RoleKind.WindowsDefault))
                    continue;
                foreach (var (key, written) in state.ResetSet)
                {
                    if (!state.Set.TryGetValue(key, out var expected))
                        continue;
                    bool present = !written.DeleteOnWrite;
                    if (present && written.WritePayload is null)
                        continue;
                    if (!expected.Matches(written.WritePayload, present))
                        failures.Add($"{setting.Id} [{state.Label.Value}] {key}");
                }
            }
        }

        Assert.True(failures.Count == 0,
            "ResetSet leaves a value the state's own Set rejects:\n" + string.Join("\n", failures));
    }
}

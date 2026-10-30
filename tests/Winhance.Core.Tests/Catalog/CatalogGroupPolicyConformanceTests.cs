using Winhance.Core.Features.Common.Catalog;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

// Windows ships no group policy, so "how Windows has it" never includes a value under SOFTWARE\Policies.
public class CatalogGroupPolicyConformanceTests
{
    private static readonly WinBuild[] Builds = [new(19045), new(26200)];

    [Fact]
    public void Windows_default_apply_never_writes_a_group_policy_path()
    {
        var failures = new List<string>();
        int checkedPolicyStates = 0;
        foreach (var setting in SettingCatalog.All)
        {
            bool hasPolicyPath = setting.Targets.OfType<RegTarget>().Any(t => t.Paths.Any(RegTarget.IsPolicyPath));
            foreach (var build in Builds)
            {
                if (!setting.Availability.Allows(build))
                    continue;
                foreach (var state in setting.States.Where(s => s.HasRole(RoleKind.WindowsDefault, build)))
                {
                    if (hasPolicyPath)
                        checkedPolicyStates++;
                    foreach (var op in ApplyPlanBuilder.Build(setting, state, build))
                    {
                        if (WrittenPath(op) is { } path && RegTarget.IsPolicyPath(path))
                            failures.Add($"{setting.Id} [{state.Label.Value}] build {build.Build}: {op.GetType().Name} {path}");
                    }
                }
            }
        }

        Assert.True(checkedPolicyStates >= 40,
            $"only {checkedPolicyStates} Windows-default states touch a policy path - the scan is not reaching the catalog.");
        Assert.True(failures.Count == 0,
            "A Windows-default apply writes a group policy value:\n" + string.Join("\n", failures));
    }

    [Fact]
    public void Policy_only_target_windows_default_accepts_the_value_absent()
    {
        var failures = new List<string>();
        int checkedTargets = 0;
        foreach (var setting in SettingCatalog.All)
        {
            foreach (var reg in setting.Targets.OfType<RegTarget>())
            {
                if (!reg.IsGroupPolicy || !reg.Paths.All(RegTarget.IsPolicyPath))
                    continue;
                foreach (var state in setting.States.Where(s => s.Roles.Any(r => r.Kind == RoleKind.WindowsDefault)))
                {
                    if (!state.Set.TryGetValue(reg.Key, out var expected))
                        continue;
                    checkedTargets++;
                    if (!expected.Matches(null, present: false))
                        failures.Add($"{setting.Id} [{state.Label.Value}] {reg.Key}");
                }
            }
        }

        Assert.True(checkedTargets >= 40,
            $"only {checkedTargets} policy-only Windows-default targets found - the scan is not reaching the catalog.");
        Assert.True(failures.Count == 0,
            "A Windows-default state rejects the policy value being absent, so a clean install reads as Custom:\n"
            + string.Join("\n", failures));
    }

    [Fact]
    public void Group_policy_targets_have_a_policy_path()
    {
        var failures = NoPolicyPathTargets().ToList();

        Assert.True(failures.Count == 0,
            "IsGroupPolicy target with no SOFTWARE\\Policies path (the Group Policy chip would mislabel it; a target "
            + "that only needs to win detection is Overrides):\n"
            + string.Join("\n", failures));
    }

    private static IEnumerable<string> NoPolicyPathTargets() =>
        SettingCatalog.All.SelectMany(s => s.Targets.OfType<RegTarget>()
            .Where(t => t.IsGroupPolicy && !t.Paths.Any(RegTarget.IsPolicyPath))
            .Select(t => $"{s.Id}/{t.Key}"));

    private static string? WrittenPath(ApplyOp op) => op switch
    {
        RegistryDeleteOp or RegistryPerSubkeyDeleteOp => null,
        RegistryWriteOp w => w.Path,
        RegistryEnsureKeyOp e => e.Path,
        RegistryBitSetOp b => b.Path,
        RegistryByteSetOp y => y.Path,
        RegistryStringFlagSetOp f => f.Path,
        RegistryListItemSetOp l => l.Path,
        RegistryCompositeSetOp c => c.Path,
        RegistryPerSubkeyWriteOp p => p.ParentPath,
        EffectOp { Effect: RegistryWriteEffect r } => r.Path,
        _ => null,
    };
}

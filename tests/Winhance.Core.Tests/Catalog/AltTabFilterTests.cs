using Winhance.Core.Features.Common.Catalog;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

public class AltTabFilterTests
{
    private const string Id = "gaming-explorer-alt-tab-filter";
    private const string Name = "MultiTaskingAltTabFilter";

    [Theory]
    [InlineData(19045, 0, false)]
    [InlineData(19045, 1, true)]
    [InlineData(19045, 2, false)]
    [InlineData(19045, 3, false)]
    [InlineData(26300, 0, false)]
    [InlineData(26300, 1, false)]
    [InlineData(26300, 2, true)]
    [InlineData(26300, 3, false)]
    public void Selection_MatchesTheDwordAndAppliesItsEffectiveChoice(int buildNumber, int index, bool deletes)
    {
        var build = new WinBuild(buildNumber);
        var setting = SettingCatalog.Find(Id)!;
        var target = Assert.Single(setting.Targets, t => t.AppliesTo.Any(range => range.Contains(build)));
        Assert.Equal(ControlKind.Selection, setting.Control);
        Assert.Equal("Explorer", Assert.IsType<RestartProcess>(setting.Apply.Restart).Name);
        Assert.Equal(setting.States[index], Assert.Single(setting.States, s => s.Set[target.Key].Matches(index, true)));
        Assert.Equal(index == 3, setting.States[index].HasRole(RoleKind.Recommended, build));

        var op = Assert.Single(ApplyRequestResolver.Resolve(Id, true, index, false, build)!);
        if (deletes)
            Assert.Equal(Name, Assert.IsType<RegistryDeleteOp>(op).Target.ValueName);
        else
        {
            var write = Assert.IsType<RegistryWriteOp>(op);
            Assert.Equal(Name, write.Target.ValueName);
            Assert.Equal(index, Assert.IsType<int>(write.Value));
        }
    }

    [Theory]
    [InlineData(19045, 4)]
    [InlineData(26300, 4)]
    [InlineData(19045, null)]
    [InlineData(26300, null)]
    public void CustomFilter_ChangesOnlyTheTargetForThisBuild(int buildNumber, int? value)
    {
        var payload = new Dictionary<string, object> { [Name] = value! };
        var op = Assert.Single(ApplyRequestResolver.Resolve(Id, true, payload, false, new WinBuild(buildNumber))!);
        if (value is null)
            Assert.Equal(Name, Assert.IsType<RegistryDeleteOp>(op).Target.ValueName);
        else
        {
            var write = Assert.IsType<RegistryWriteOp>(op);
            Assert.Equal(Name, write.Target.ValueName);
            Assert.Equal(value.Value, Assert.IsType<int>(write.Value));
        }
    }

    [Theory]
    [InlineData(19045, 1)]
    [InlineData(26300, 2)]
    public void Reset_ReturnsToTheBuildsAbsentStateRatherThanTheExplicitMaximum(int buildNumber, int defaultIndex)
    {
        var build = new WinBuild(buildNumber);
        var setting = SettingCatalog.Find(Id)!;
        var target = Assert.Single(setting.Targets, t => t.AppliesTo.Any(range => range.Contains(build)));
        Assert.Equal(setting.States[defaultIndex], Assert.Single(setting.States, s => s.Set[target.Key].Matches(null, false)));
        Assert.True(setting.States[defaultIndex].HasRole(RoleKind.WindowsDefault, build));
        var op = Assert.Single(ApplyRequestResolver.Resolve(Id, true, 0, true, build)!);
        Assert.Equal(Name, Assert.IsType<RegistryDeleteOp>(op).Target.ValueName);
    }
}

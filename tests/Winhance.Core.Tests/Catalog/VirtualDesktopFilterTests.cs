using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Localization;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

public class VirtualDesktopFilterTests
{
    [Theory]
    [InlineData("explorer-taskbar-all-desktops", "VirtualDesktopTaskbarFilter")]
    [InlineData("explorer-alt-tab-all-desktops", "VirtualDesktopAltTabFilter")]
    public void AllDesktops_WritesOnlyItsOwnFilterAndRequiresExplorerRestart(string id, string name)
    {
        var build = new WinBuild(26300, 9457);
        var setting = SettingCatalog.Find(id)!;
        Assert.Equal("Explorer", Assert.IsType<RestartProcess>(setting.Apply.Restart).Name);
        Assert.False(setting.Availability.Allows(new WinBuild(26200)));
        Assert.True(setting.Availability.Allows(build));

        const string path = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        var enable = ApplyRequestResolver.Resolve(id, true, null, false, build);
        var write = Assert.IsType<RegistryWriteOp>(Assert.Single(enable!));
        Assert.Equal(path, write.Path);
        Assert.Equal(name, write.Target.ValueName);
        Assert.Equal(0, Assert.IsType<int>(write.Value));

        var disable = ApplyRequestResolver.Resolve(id, false, null, false, build);
        var delete = Assert.IsType<RegistryDeleteOp>(Assert.Single(disable!));
        Assert.Equal(path, delete.Path);
        Assert.Equal(name, delete.Target.ValueName);

        var off = setting.States.Single(s => s.Label == LocKey.Common.Disabled);
        var on = setting.States.Single(s => s.Label == LocKey.Common.Enabled);
        Assert.True(off.Set[name].Matches(1, true));
        Assert.True(off.Set[name].Matches(null, false));
        Assert.False(on.Set[name].Matches(null, false));
        Assert.False(off.HasRole(RoleKind.WindowsDefault, new WinBuild(26200)));
        var reset = ApplyRequestResolver.Resolve(id, true, null, true, build);
        Assert.Equal(name, Assert.IsType<RegistryDeleteOp>(Assert.Single(reset!)).Target.ValueName);
    }
}

using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Localization;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

public class StorageAndUsbPreferenceTests
{
    [Fact]
    public void UsbReset_RestoresAbsenceOnTheVerifiedBuild()
    {
        var setting = SettingCatalog.ById["notifications-usb-errors"];
        var state = setting.States.Single(s => s.Label == LocKey.Common.Enabled);
        var build = new WinBuild(26300, 9457);
        Assert.True(state.HasRole(RoleKind.WindowsDefault, build));
        Assert.False(state.HasRole(RoleKind.WindowsDefault, new WinBuild(26200)));
        Assert.False(state.HasRole(RoleKind.WindowsDefault, new WinBuild(26400)));
        var delete = Assert.IsType<RegistryDeleteOp>(Assert.Single(ApplyRequestResolver.Resolve(setting.Id, enable: false, value: null, resetToDefault: true, build: build)!));
        Assert.Equal("NotifyOnUsbErrors", delete.Target.ValueName);
    }

    [Theory]
    [InlineData("gaming-storage-sense-temporary-files", @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy", "04")]
    [InlineData("notifications-usb-errors", @"HKEY_CURRENT_USER\Software\Microsoft\Shell\USB", "NotifyOnUsbErrors")]
    public void Toggle_WritesOnlyItsOwnPreference(string id, string path, string name)
    {
        var setting = SettingCatalog.ById[id];
        foreach (bool on in new[] { true, false })
        {
            var label = on ? LocKey.Common.Enabled : LocKey.Common.Disabled;
            int value = on ? 1 : 0;
            var op = Assert.Single(ApplyRequestResolver.Resolve(id, on, value: null, resetToDefault: false, build: new WinBuild(26300, 9457))!);
            if (id == "notifications-usb-errors" && on)
            {
                var delete = Assert.IsType<RegistryDeleteOp>(op);
                Assert.Equal(path, delete.Path);
                Assert.Equal(name, delete.Target.ValueName);
            }
            else
            {
                var write = Assert.IsType<RegistryWriteOp>(op);
                Assert.Equal(path, write.Path);
                Assert.Equal(name, write.Target.ValueName);
                Assert.Equal(RegistryValueKind.DWord, write.Target.Type);
                Assert.Equal(value, Assert.IsType<int>(write.Value));
            }

            var state = setting.States.Single(s => s.Label == label);
            var match = Assert.Single(state.Set).Value;
            Assert.True(match.Matches(value, true));
            Assert.False(match.Matches(1 - value, true));
            Assert.Equal(on, match.Matches(null, false));
            Assert.False(state.IsFallback);
        }
    }
}

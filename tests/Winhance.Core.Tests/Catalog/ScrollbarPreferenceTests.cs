using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Localization;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

public class ScrollbarPreferenceTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void OffAndReset_RestoreAbsenceOnTheVerifiedBuild(bool enable, bool reset)
    {
        var op = Assert.Single(ApplyRequestResolver.Resolve("explorer-always-show-scrollbars", enable, value: null, resetToDefault: reset, build: new WinBuild(26300, 9457))!);
        var delete = Assert.IsType<RegistryDeleteOp>(op);
        Assert.Equal(@"HKEY_CURRENT_USER\Control Panel\Accessibility", delete.Path);
        Assert.Equal("DynamicScrollbars", delete.Target.ValueName);
    }

    [Theory]
    [InlineData(26300, true, 0)]
    [InlineData(26400, true, 0)]
    [InlineData(26400, false, 1)]
    public void AlwaysShow_InvertsDynamicScrollbars(int build, bool alwaysShow, int expected)
    {
        var setting = SettingCatalog.ById["explorer-always-show-scrollbars"];
        var label = alwaysShow ? LocKey.Common.Enabled : LocKey.Common.Disabled;
        var write = Assert.IsType<RegistryWriteOp>(Assert.Single(ApplyRequestResolver.Resolve(setting.Id, alwaysShow, value: null, resetToDefault: false, build: new WinBuild(build))!));
        Assert.Equal(@"HKEY_CURRENT_USER\Control Panel\Accessibility", write.Path);
        Assert.Equal("DynamicScrollbars", write.Target.ValueName);
        Assert.Equal(RegistryValueKind.DWord, write.Target.Type);
        Assert.Equal(expected, Assert.IsType<int>(write.Value));

        var state = setting.States.Single(s => s.Label == label);
        var match = state.Set[write.Target.Key];
        Assert.True(match.Matches(expected, true));
        Assert.False(match.Matches(1 - expected, true));
        Assert.Equal(!alwaysShow, match.Matches(null, false));
        Assert.False(state.IsFallback);
    }
}

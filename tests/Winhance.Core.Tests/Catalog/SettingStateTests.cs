using System.Collections.Generic;
using Winhance.Core.Features.Common.Catalog;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

public class SettingStateTests
{
    private static SettingState Make(params StateRole[] roles) => new()
    {
        Label = "Test",
        Roles = roles,
        Set = new Dictionary<string, StateValue> { ["K"] = StateValue.Of(1) },
        Effects = System.Array.Empty<Effect>(),
    };

    [Fact]
    public void HasRole_defaults_to_Always_context()
    {
        var s = Make(new StateRole(RoleKind.Recommended));
        Assert.True(s.HasRole(RoleKind.Recommended));
        Assert.False(s.HasRole(RoleKind.WindowsDefault));
    }

    [Fact]
    public void HasRole_is_context_scoped_for_power()
    {
        var s = Make(
            new StateRole(RoleKind.Recommended, PowerContext.AC),
            new StateRole(RoleKind.WindowsDefault, PowerContext.AC));
        Assert.True(s.HasRole(RoleKind.Recommended, PowerContext.AC));
        Assert.False(s.HasRole(RoleKind.Recommended, PowerContext.DC));
    }
}

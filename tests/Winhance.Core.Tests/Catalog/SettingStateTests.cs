using Winhance.Core.Features.Common.Catalog;
using Xunit;

using Winhance.TestSupport;
namespace Winhance.Core.Tests.Catalog;

public class SettingStateTests
{
    private static SettingState Make(params StateRole[] roles) => new()
    {
        Label = TestKeys.Of("Test"),
        Roles = roles,
        Set = new Dictionary<string, StateValue> { ["K"] = StateValue.Of(1) },
        Effects = System.Array.Empty<Effect>(),
    };

    [Fact]
    public void HasRole_WithoutContext_DefaultsToAlwaysContext()
    {
        var s = Make(new StateRole(RoleKind.Recommended));
        Assert.True(s.HasRole(RoleKind.Recommended));
        Assert.False(s.HasRole(RoleKind.WindowsDefault));
    }

    [Fact]
    public void HasRole_PowerRoles_AreContextScoped()
    {
        var s = Make(
            new StateRole(RoleKind.Recommended, PowerContext.AC),
            new StateRole(RoleKind.WindowsDefault, PowerContext.AC));
        Assert.True(s.HasRole(RoleKind.Recommended, PowerContext.AC));
        Assert.False(s.HasRole(RoleKind.Recommended, PowerContext.DC));
    }
}

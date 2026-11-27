using Winhance.Core.Features.Common.Catalog;
using Xunit;
using Winhance.TestSupport;

namespace Winhance.Core.Tests.Catalog;

public class LinkTests
{
    [Fact]
    public void Constructor_RequiresLink_DefaultsToReverseCascadeWithoutForce()
    {
        var l = new Link("other", LinkKind.Requires, TestKeys.Of("On"));
        Assert.True(l.ReverseCascade);
        Assert.False(l.Force);
        Assert.Equal(TestKeys.Of("On"), l.RequiredState);
    }

    [Fact]
    public void Constructor_EnablesLink_AllowsNoReverseCascadeWithForce()
    {
        var l = new Link("other", LinkKind.Enables, TestKeys.Of("On")) { ReverseCascade = false, Force = true };
        Assert.Equal(LinkKind.Enables, l.Kind);
        Assert.False(l.ReverseCascade);
        Assert.True(l.Force);
    }

    [Fact]
    public void Setting_Default_HasEmptyRelationshipFields()
    {
        var setting = new Setting { Id = "s", Display = new() { Name = TestKeys.Of("s"), Description = TestKeys.Of("s") } };
        Assert.Null(setting.UiParentId);

        var state = new SettingState { Label = TestKeys.Of("x") };
        Assert.Empty(state.Links);
        Assert.Null(state.Controls);
    }
}

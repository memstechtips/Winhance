using Winhance.Core.Features.Common.Catalog;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

public class LinkTests
{
    [Fact]
    public void Requires_link_defaults_to_reverse_cascade_no_force()
    {
        var l = new Link("other", LinkKind.Requires, "On");
        Assert.True(l.ReverseCascade);
        Assert.False(l.Force);
        Assert.Equal("On", l.RequiredState);
    }

    [Fact]
    public void Auto_enable_style_link_is_enables_no_reverse_force()
    {
        var l = new Link("other", LinkKind.Enables, "On") { ReverseCascade = false, Force = true };
        Assert.Equal(LinkKind.Enables, l.Kind);
        Assert.False(l.ReverseCascade);
        Assert.True(l.Force);
    }

    [Fact]
    public void Setting_and_state_relationship_fields_default_empty()
    {
        var setting = new Setting { Id = "s", Display = new() { Name = "s", Description = "s" } };
        Assert.Empty(setting.Links);
        Assert.Null(setting.UiParentId);

        var state = new SettingState { Label = "x" };
        Assert.Null(state.Controls);
    }
}

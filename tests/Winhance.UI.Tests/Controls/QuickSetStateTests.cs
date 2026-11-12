using CommunityToolkit.Mvvm.Input;
using FluentAssertions;
using Winhance.UI.Features.Common.Controls;
using Xunit;

namespace Winhance.UI.Tests.Controls;

// Each name returned is a PropertyChanged the quick-set buttons raise, and every raise re-runs their bindings on
// every card, so an unchanged value must not be reported.
public class QuickSetStateTests
{
    private static readonly IRelayCommand Recommended = new RelayCommand(() => { });
    private static readonly IRelayCommand Default = new RelayCommand(() => { });

    private static QuickSetState Shown() =>
        new(true, Recommended, Default, "Set to recommended", "Set to default", "Recommended", "Default");

    [Fact]
    public void Changed_EqualStates_ReportsNothing()
    {
        QuickSetState.Changed(Shown(), Shown()).Should().BeEmpty();
        QuickSetState.Changed(QuickSetState.Hidden, QuickSetState.Hidden).Should().BeEmpty();
    }

    [Fact]
    public void Changed_HiddenToShown_ReportsEveryValueThatMovedOffHidden()
    {
        QuickSetState.Changed(QuickSetState.Hidden, Shown()).Should().BeEquivalentTo(
            nameof(SettingQuickSetButtons.ButtonsVisibility),
            nameof(SettingQuickSetButtons.RecommendedCommand),
            nameof(SettingQuickSetButtons.DefaultCommand),
            nameof(SettingQuickSetButtons.RecommendedTooltip),
            nameof(SettingQuickSetButtons.DefaultTooltip),
            nameof(SettingQuickSetButtons.RecommendedAutomationName),
            nameof(SettingQuickSetButtons.DefaultAutomationName));
    }

    [Fact]
    public void Changed_NewCommandInstanceThatLooksTheSame_IsReported()
    {
        var after = Shown() with { DefaultCommand = new RelayCommand(() => { }) };

        QuickSetState.Changed(Shown(), after).Should().Equal(nameof(SettingQuickSetButtons.DefaultCommand));
    }

    [Fact]
    public void Changed_TooltipChange_ReportsOnlyThatTooltip()
    {
        var after = Shown() with { RecommendedTooltip = "Changed" };

        QuickSetState.Changed(Shown(), after).Should().Equal(nameof(SettingQuickSetButtons.RecommendedTooltip));
    }
}

using FluentAssertions;
using Winhance.Core.Features.Common.Catalog;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

public class ThemeModeApplyPathTests
{
    // ThemeModeApplier runs this setting through the synchronous ApplyExecutor, so an async effect added here
    // would be split off the plan and never run.
    [Fact]
    public void No_theme_state_carries_an_effect_the_synchronous_apply_path_cannot_run()
    {
        SettingCatalog.All.First(s => s.Id == "theme-mode-windows")
            .States.SelectMany(st => st.Effects)
            .Where(e => e.IsAsyncIo)
            .Should().BeEmpty();
    }
}

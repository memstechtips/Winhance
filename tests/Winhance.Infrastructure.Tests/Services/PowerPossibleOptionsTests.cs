using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

// Runs against THIS machine's powercfg data, because the defect these cover is Windows answering something the
// docs do not describe: for a setting that takes a range, the possible-setting walk never ends. A mock answers
// only what the test tells it to, so it cannot show that.
public class PowerPossibleOptionsTests
{
    private static IReadOnlyList<(string Id, PowerCfgTarget Target)> PowerCards() =>
        SettingCatalog.All
            .Select(s => (s.Id, Target: s.Targets.OfType<PowerCfgTarget>().FirstOrDefault()))
            .Where(c => c.Target is not null)
            .Select(c => (c.Id, c.Target!))
            .ToList();

    [Fact]
    public async Task Every_power_card_answers_its_choices_and_the_walk_ends()
    {
        var service = new PowerSettingsQueryService(new Mock<ILogService>().Object);
        var runaway = new List<string>();
        var withChoices = 0;

        foreach (var (id, target) in PowerCards())
        {
            var call = service.GetPossibleValuesAsync(target.SubgroupGuid, target.SettingGuid);
            if (await Task.WhenAny(call, Task.Delay(TimeSpan.FromSeconds(10))) != call)
            {
                runaway.Add($"{id} (never answered)");
                continue;
            }

            var options = await call;
            // 64 is the walk's guard. Hitting it means the walk was cut off rather than finished, which is the
            // shape of the runaway.
            if (options.Count >= 64)
                runaway.Add($"{id} ({options.Count} choices)");

            if (options.Count > 0)
            {
                withChoices++;
                options.Select(o => o.Index).Should().Equal(
                    Enumerable.Range(0, options.Count),
                    "{0}'s choices are numbered by possible-setting index, which is what a catalog state carries", id);
                options.Should().OnlyContain(o => o.Name.Length > 0);
            }
        }

        runaway.Should().BeEmpty("a card whose choice walk does not end blocks the whole Power page");
        withChoices.Should().BeGreaterThan(0, "no power card offered any choice at all, so this test proved nothing");
    }

    [Fact]
    public async Task A_setting_that_takes_a_range_offers_no_choices()
    {
        var service = new PowerSettingsQueryService(new Mock<ILogService>().Object);
        var ranges = PowerCards().Where(c => c.Id is "power-display-timeout" or "power-sleep-timeout").ToList();
        ranges.Should().NotBeEmpty();

        foreach (var (id, target) in ranges)
        {
            var options = await service.GetPossibleValuesAsync(target.SubgroupGuid, target.SettingGuid);
            options.Should().BeEmpty("{0} takes a number of seconds, not one of a list", id);
        }
    }
}

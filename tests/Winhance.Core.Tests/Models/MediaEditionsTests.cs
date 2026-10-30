using FluentAssertions;
using Winhance.Core.Features.Autounattend.Catalogs;
using Winhance.Core.Features.WimUtil.Models;
using Xunit;

namespace Winhance.Core.Tests.Models;

public class MediaEditionsTests
{
    // Microsoft's generic install key for each EditionId an install.wim reports, so a reordered dropdown cannot
    // quietly hand a single-edition medium another edition's key.
    [Theory]
    [InlineData("Core", "YTMG3-N6DKC-DKB77-7M9GH-8HVX7")]
    [InlineData("CoreN", "4CPRK-NM3K3-X6XXQ-RXX86-WXCHW")]
    [InlineData("CoreSingleLanguage", "BT79Q-G7N6G-PGBYW-4YWX6-6F4BT")]
    [InlineData("Professional", "VK7JG-NPHTM-C97JM-9MPGT-3V66T")]
    [InlineData("ProfessionalN", "2B87N-8KFHP-DKV6R-Y2C8J-PKCKT")]
    [InlineData("ProfessionalWorkstation", "DXG7C-N36C4-C4HTG-X4T3X-2YV77")]
    [InlineData("ProfessionalWorkstationN", "WYPNQ-8C467-V2W6J-TX4WX-WT2RQ")]
    [InlineData("ProfessionalEducation", "8PTT6-RNW4C-6V7J2-C2D3X-MHBPB")]
    [InlineData("ProfessionalEducationN", "GJTYN-HDMQY-FRR76-HVGC7-QPF8P")]
    [InlineData("Education", "YNMGQ-8RYV3-4PGQ3-C8XTP-7CFBY")]
    [InlineData("EducationN", "84NGF-MHBT6-FXBX8-QWJK7-DRR8H")]
    [InlineData("Enterprise", "XGVPP-NMH47-7TTHJ-W3FW7-8HV2C")]
    [InlineData("EnterpriseN", "WGGHN-J84D6-QYCPR-T7PJ7-X766F")]
    public void Each_edition_maps_to_the_option_carrying_its_generic_key(string editionId, string key)
    {
        var states = AutounattendCatalog.All.First(s => s.Id == "autounattend-edition").States;

        var option = MediaEditions.OptionFor(editionId);

        option.Should().NotBeNull();
        states[option!.Value].Set["key"].WritePayload.Should().Be(key);
    }

    [Fact]
    public void An_edition_with_no_generic_key_is_left_for_Setup_to_ask()
    {
        MediaEditions.OptionFor("ServerStandard").Should().BeNull();
        MediaEditions.OptionFor(string.Empty).Should().BeNull();
    }

    [Theory]
    [InlineData(1, "Professional", true)]
    [InlineData(4, "Professional", false)]
    [InlineData(0, "Professional", false)]
    [InlineData(null, "Professional", false)]
    [InlineData(4, "ServerStandard", true)]
    public void PicksAnother_is_true_only_when_the_pick_names_a_different_edition(int? option, string keptEditionId, bool expected)
    {
        MediaEditions.PicksAnother(option, keptEditionId).Should().Be(expected);
    }
}

using FluentAssertions;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Infrastructure.Features.WimUtil.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.WimUtil.WimServices;

public class OfflinePowerSchemesTests
{
    private static readonly PowerCfgTarget Target = new(
        "Power", "7516B95F-F776-4464-8C53-06167F40CC99", "{3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e}", PowerModeSupport.Separate);

    [Fact]
    public void SettingKey_NamesTheSchemeSubgroupAndSettingInLowerCaseWithoutBraces()
    {
        var key = OfflinePowerSchemes.SettingKey(Target, Guid.Parse("8C5E7FDA-E8BF-4A96-9A85-A6E23A8C635C"));

        key.Should().Be(
            @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes\8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c"
            + @"\7516b95f-f776-4464-8c53-06167f40cc99\3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e");
    }

    [Theory]
    [InlineData(PowerContext.AC, new[] { "ACSettingIndex" })]
    [InlineData(PowerContext.DC, new[] { "DCSettingIndex" })]
    [InlineData(PowerContext.Always, new[] { "ACSettingIndex", "DCSettingIndex" })]
    public void ValueNames_FollowTheContext(PowerContext context, string[] expected) =>
        OfflinePowerSchemes.ValueNames(context).Should().Equal(expected);

    [Theory]
    [InlineData("381b4222-f694-41f0-9685-ff5bb260df2e", true)]
    [InlineData("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", true)]
    [InlineData("a1841308-3541-4fab-bc81-f71556f20b4a", true)]
    [InlineData("e9a42b02-d5df-448d-aa00-03f14749eb61", false)]
    [InlineData("57696e68-616e-6365-506f-776572000000", false)]
    public void IsStock_IsTrueOnlyForTheThreeSchemesWindowsShips(string scheme, bool expected) =>
        OfflinePowerSchemes.IsStock(Guid.Parse(scheme)).Should().Be(expected);
}

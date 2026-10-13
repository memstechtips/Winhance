using FluentAssertions;
using Winhance.Core.Features.Common.Catalog;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

public class PowerPlanCatalogTests
{
    [Theory]
    [InlineData("57696e68-616e-6365-506f-776572000000", true)]
    [InlineData("57696E68-616E-6365-506F-776572000000", true)]
    [InlineData("381b4222-f694-41f0-9685-ff5bb260df2e", false)]
    [InlineData(null, false)]
    public void IsWinhancePowerPlan_MatchesTheGuidOnly(string? guid, bool expected) =>
        PowerPlanCatalog.IsWinhancePowerPlan(guid).Should().Be(expected);

    [Theory]
    [InlineData("Winhance Power Plan", true)]
    [InlineData("\u0637\u0631\u062D \u0627\u0646\u0631\u0698\u06CC Winhance", true)]
    [InlineData("Balanced", false)]
    [InlineData(null, false)]
    public void HasWinhanceName_MatchesTheBrandInTheName(string? name, bool expected) =>
        PowerPlanCatalog.HasWinhanceName(name).Should().Be(expected);
}

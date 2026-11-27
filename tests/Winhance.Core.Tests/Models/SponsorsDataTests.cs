using System.Text.Json;
using FluentAssertions;
using Winhance.Core.Features.Common.Models;
using Xunit;

namespace Winhance.Core.Tests.Models;

public class SponsorsDataTests
{
    [Fact]
    public void Supporter_MonthlyWithMonths_ReadsBoth()
    {
        var doc = JsonSerializer.Deserialize<SponsorsDocument>(
            """{"supporters":[{"name":"Max","since":"2026-10","monthly":true,"months":4}]}""");

        doc!.Supporters[0].Monthly.Should().BeTrue();
        doc.Supporters[0].Months.Should().Be(4);
    }

    [Fact]
    public void Supporter_OneTime_IsNotMonthly()
    {
        var doc = JsonSerializer.Deserialize<SponsorsDocument>(
            """{"supporters":[{"name":"Max","since":"2026-10","months":3}]}""");

        doc!.Supporters[0].Monthly.Should().BeFalse();
    }
}

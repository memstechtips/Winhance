// File: tests/Winhance.Infrastructure.Tests/Services/SpecialSettingHandlerRegistryTests.cs
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class SpecialSettingHandlerRegistryTests
{
    [Fact]
    public void TryGet_RegisteredId_ReturnsHandler()
    {
        var handler = new Mock<ISpecialSettingHandler>().Object;
        var sut = new SpecialSettingHandlerRegistry(new Dictionary<string, ISpecialSettingHandler>
        {
            ["power-plan-selection"] = handler
        });

        sut.TryGet("power-plan-selection").Should().BeSameAs(handler);
    }

    [Fact]
    public void TryGet_UnregisteredId_ReturnsNull()
    {
        var sut = new SpecialSettingHandlerRegistry(new Dictionary<string, ISpecialSettingHandler>());

        sut.TryGet("nope").Should().BeNull();
    }

    [Fact]
    public void TryGet_MultipleRegistered_ReturnsCorrectOne()
    {
        var power = new Mock<ISpecialSettingHandler>().Object;
        var update = new Mock<ISpecialSettingHandler>().Object;
        var sut = new SpecialSettingHandlerRegistry(new Dictionary<string, ISpecialSettingHandler>
        {
            ["power-plan-selection"] = power,
            ["updates-policy-mode"] = update,
        });

        sut.TryGet("power-plan-selection").Should().BeSameAs(power);
        sut.TryGet("updates-policy-mode").Should().BeSameAs(update);
    }
}

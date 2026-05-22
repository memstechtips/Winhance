// File: tests/Winhance.Infrastructure.Tests/Services/ActionCommandRegistryTests.cs
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class ActionCommandRegistryTests
{
    [Fact]
    public void TryGet_RegisteredId_ReturnsProvider()
    {
        var provider = new Mock<IActionCommandProvider>().Object;
        var sut = new ActionCommandRegistry(new Dictionary<string, IActionCommandProvider>
        {
            ["taskbar-clean"] = provider
        });

        sut.TryGet("taskbar-clean").Should().BeSameAs(provider);
    }

    [Fact]
    public void TryGet_UnregisteredId_ReturnsNull()
    {
        var sut = new ActionCommandRegistry(new Dictionary<string, IActionCommandProvider>());

        sut.TryGet("nope").Should().BeNull();
    }
}

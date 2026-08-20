using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.UI;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Services;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class HardwareFilterServiceTests
{
    private readonly Mock<IEventBus> _eventBus = new();
    private readonly Mock<ILogService> _log = new();

    private HardwareFilterService CreateSut() => new(_eventBus.Object, _log.Object);

    [Fact]
    public void IsFilterEnabled_DefaultsToOn()
    {
        CreateSut().IsFilterEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task SetAsync_TurningTheFilterOff_PublishesAndRaises()
    {
        var sut = CreateSut();
        bool? raised = null;
        sut.FilterStateChanged += (_, enabled) => raised = enabled;

        await sut.SetAsync(false);

        sut.IsFilterEnabled.Should().BeFalse();
        raised.Should().BeFalse();
        _eventBus.Verify(b => b.Publish(It.Is<FilterStateChangedEvent>(e => !e.IsFilterEnabled)), Times.Once);
    }

    [Fact]
    public async Task SetAsync_WithTheValueItAlreadyHas_PublishesNothing()
    {
        var sut = CreateSut();
        bool raised = false;
        sut.FilterStateChanged += (_, _) => raised = true;

        await sut.SetAsync(true);

        sut.IsFilterEnabled.Should().BeTrue();
        raised.Should().BeFalse();
        _eventBus.Verify(b => b.Publish(It.IsAny<FilterStateChangedEvent>()), Times.Never);
    }

    [Fact]
    public async Task ResetAsync_PutsTheFilterBackOn()
    {
        var sut = CreateSut();
        await sut.SetAsync(false);

        await sut.ResetAsync();

        sut.IsFilterEnabled.Should().BeTrue();
        _eventBus.Verify(b => b.Publish(It.Is<FilterStateChangedEvent>(e => e.IsFilterEnabled)), Times.Once);
    }

    [Fact]
    public async Task ResetAsync_WhenTheFilterIsAlreadyOn_PublishesNothing()
    {
        var sut = CreateSut();

        await sut.ResetAsync();

        _eventBus.Verify(b => b.Publish(It.IsAny<FilterStateChangedEvent>()), Times.Never);
    }
}

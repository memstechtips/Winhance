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
    public void Set_TurningTheFilterOff_PublishesAndRaises()
    {
        var sut = CreateSut();
        bool? raised = null;
        sut.FilterStateChanged += (_, enabled) => raised = enabled;

        sut.Set(false);

        sut.IsFilterEnabled.Should().BeFalse();
        raised.Should().BeFalse();
        _eventBus.Verify(b => b.Publish(It.Is<FilterStateChangedEvent>(e => !e.IsFilterEnabled)), Times.Once);
    }

    [Fact]
    public void Set_WithTheValueItAlreadyHas_PublishesNothing()
    {
        var sut = CreateSut();
        bool raised = false;
        sut.FilterStateChanged += (_, _) => raised = true;

        sut.Set(true);

        sut.IsFilterEnabled.Should().BeTrue();
        raised.Should().BeFalse();
        _eventBus.Verify(b => b.Publish(It.IsAny<FilterStateChangedEvent>()), Times.Never);
    }

    [Fact]
    public void Reset_PutsTheFilterBackOn()
    {
        var sut = CreateSut();
        sut.Set(false);

        sut.Reset();

        sut.IsFilterEnabled.Should().BeTrue();
        _eventBus.Verify(b => b.Publish(It.Is<FilterStateChangedEvent>(e => e.IsFilterEnabled)), Times.Once);
    }

    [Fact]
    public void Reset_WhenTheFilterIsAlreadyOn_PublishesNothing()
    {
        var sut = CreateSut();

        sut.Reset();

        _eventBus.Verify(b => b.Publish(It.IsAny<FilterStateChangedEvent>()), Times.Never);
    }
}

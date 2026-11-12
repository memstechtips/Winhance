using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.UI;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Services;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class TargetWindowsServiceTests
{
    private readonly Mock<IEventBus> _eventBus = new();
    private readonly Mock<ILogService> _log = new();

    private TargetWindowsService CreateSut() => new(_eventBus.Object, _log.Object);

    [Fact]
    public void Build_DefaultsToTheNewestRelease()
    {
        CreateSut().Build.Should().Be(WindowsReleases.Latest.Build);
    }

    [Fact]
    public void Set_ANewTarget_PublishesSoPagesReload()
    {
        var sut = CreateSut();
        bool raised = false;
        sut.Changed += (_, _) => raised = true;

        sut.Set(26100);

        sut.Build.Should().Be(26100);
        raised.Should().BeTrue();
        _eventBus.Verify(b => b.Publish(It.IsAny<FilterStateChangedEvent>()), Times.Once);
    }

    [Fact]
    public void Set_TheSameTarget_PublishesNothing()
    {
        var sut = CreateSut();

        sut.Set(WindowsReleases.Latest.Build);

        _eventBus.Verify(b => b.Publish(It.IsAny<FilterStateChangedEvent>()), Times.Never);
    }

    [Fact]
    public void Reset_GoesBackToTheNewestRelease()
    {
        var sut = CreateSut();
        sut.Set(null);

        sut.Reset();

        sut.Build.Should().Be(WindowsReleases.Latest.Build);
    }
}

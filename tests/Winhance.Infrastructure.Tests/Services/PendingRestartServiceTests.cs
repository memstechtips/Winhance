using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class PendingRestartServiceTests
{
    private readonly Mock<IEventBus> _mockEventBus = new();
    private readonly Mock<ILogService> _mockLog = new();
    private readonly PendingRestartService _sut;

    public PendingRestartServiceTests()
    {
        _sut = new PendingRestartService(_mockEventBus.Object, _mockLog.Object);
    }

    [Fact]
    public void IsPending_Initially_IsFalse()
    {
        _sut.IsPending.Should().BeFalse();
        _sut.PendingSettingIds.Should().BeEmpty();
    }

    [Fact]
    public void Register_AddsSettingAndPublishesEvent()
    {
        _sut.Register("explorer-show-file-extensions");

        _sut.IsPending.Should().BeTrue();
        _sut.PendingSettingIds.Should().ContainSingle()
            .Which.Should().Be("explorer-show-file-extensions");
        _mockEventBus.Verify(
            b => b.Publish(It.Is<PendingRestartChangedEvent>(e => e.IsPending)), Times.Once);
    }

    [Fact]
    public void Register_SameSettingTwice_DoesNotDuplicateOrRepublish()
    {
        _sut.Register("explorer-show-file-extensions");
        _sut.Register("explorer-show-file-extensions");

        _sut.PendingSettingIds.Should().ContainSingle();
        _mockEventBus.Verify(b => b.Publish(It.IsAny<PendingRestartChangedEvent>()), Times.Once);
    }

    [Fact]
    public void Register_NullOrWhitespace_IsIgnored()
    {
        _sut.Register(null!);
        _sut.Register("   ");

        _sut.IsPending.Should().BeFalse();
        _mockEventBus.Verify(b => b.Publish(It.IsAny<PendingRestartChangedEvent>()), Times.Never);
    }

    [Fact]
    public void Clear_EmptiesSetAndPublishesNotPending()
    {
        _sut.Register("a");
        _mockEventBus.Invocations.Clear();

        _sut.Clear();

        _sut.IsPending.Should().BeFalse();
        _sut.PendingSettingIds.Should().BeEmpty();
        _mockEventBus.Verify(
            b => b.Publish(It.Is<PendingRestartChangedEvent>(e => !e.IsPending)), Times.Once);
    }

    [Fact]
    public void Clear_WhenAlreadyEmpty_PublishesNothing()
    {
        _sut.Clear();

        _mockEventBus.Verify(b => b.Publish(It.IsAny<PendingRestartChangedEvent>()), Times.Never);
    }

    [Fact]
    public void PendingSettingIds_IsASnapshot_NotALiveView()
    {
        _sut.Register("a");
        var snapshot = _sut.PendingSettingIds;

        _sut.Register("b");

        snapshot.Should().ContainSingle("the returned collection must not mutate under the caller");
    }

    [Fact]
    public void Register_IsThreadSafe()
    {
        Parallel.For(0, 200, i => _sut.Register($"setting-{i % 50}"));

        _sut.PendingSettingIds.Should().HaveCount(50);
    }
}

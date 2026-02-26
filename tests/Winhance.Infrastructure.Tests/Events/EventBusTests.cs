using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.Settings;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Events;
using Xunit;

namespace Winhance.Infrastructure.Tests.Events;

public class EventBusTests
{
    private readonly Mock<ILogService> _mockLog = new();
    private readonly EventBus _eventBus;

    public EventBusTests()
    {
        _eventBus = new EventBus(_mockLog.Object);
    }

    [Fact]
    public void Constructor_NullLogService_ThrowsArgumentNull()
    {
        var action = () => new EventBus(null!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Publish_NullEvent_ThrowsArgumentNull()
    {
        var action = () => _eventBus.Publish<SettingAppliedEvent>(null!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Subscribe_NullHandler_ThrowsArgumentNull()
    {
        Action<SettingAppliedEvent>? handler = null;
        var action = () => _eventBus.Subscribe(handler!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SubscribeAsync_NullHandler_ThrowsArgumentNull()
    {
        Func<SettingAppliedEvent, Task>? handler = null;
        var action = () => _eventBus.SubscribeAsync(handler!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Unsubscribe_NullToken_ThrowsArgumentNull()
    {
        var action = () => _eventBus.Unsubscribe(null!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Publish_WithSubscriber_NotifiesSubscriber()
    {
        SettingAppliedEvent? received = null;
        _eventBus.Subscribe<SettingAppliedEvent>(e => received = e);

        var evt = new SettingAppliedEvent("test-id", true);
        _eventBus.Publish(evt);

        received.Should().NotBeNull();
        received.Should().BeSameAs(evt);
    }

    [Fact]
    public void Publish_MultipleSubscribers_NotifiesAll()
    {
        var count = 0;
        _eventBus.Subscribe<SettingAppliedEvent>(_ => count++);
        _eventBus.Subscribe<SettingAppliedEvent>(_ => count++);
        _eventBus.Subscribe<SettingAppliedEvent>(_ => count++);

        _eventBus.Publish(new SettingAppliedEvent("id", true));

        count.Should().Be(3);
    }

    [Fact]
    public void Publish_NoSubscribers_DoesNotThrow()
    {
        var action = () => _eventBus.Publish(new SettingAppliedEvent("id", true));
        action.Should().NotThrow();
    }

    [Fact]
    public void Publish_DifferentEventType_DoesNotNotifyWrongSubscribers()
    {
        var received = false;
        _eventBus.Subscribe<SettingAppliedEvent>(_ => received = true);

        _eventBus.Publish(new ReviewModeExitedEvent());

        received.Should().BeFalse();
    }

    [Fact]
    public void Unsubscribe_ViaToken_StopsReceivingEvents()
    {
        var count = 0;
        var token = _eventBus.Subscribe<SettingAppliedEvent>(_ => count++);

        _eventBus.Publish(new SettingAppliedEvent("id", true));
        count.Should().Be(1);

        _eventBus.Unsubscribe(token);
        _eventBus.Publish(new SettingAppliedEvent("id", true));
        count.Should().Be(1);
    }

    [Fact]
    public void Unsubscribe_ViaTokenDispose_StopsReceivingEvents()
    {
        var count = 0;
        var token = _eventBus.Subscribe<SettingAppliedEvent>(_ => count++);

        _eventBus.Publish(new SettingAppliedEvent("id", true));
        count.Should().Be(1);

        token.Dispose();
        _eventBus.Publish(new SettingAppliedEvent("id", true));
        count.Should().Be(1);
    }

    [Fact]
    public void Token_DisposeMultipleTimes_DoesNotThrow()
    {
        var token = _eventBus.Subscribe<SettingAppliedEvent>(_ => { });

        var action = () =>
        {
            token.Dispose();
            token.Dispose();
        };
        action.Should().NotThrow();
    }

    [Fact]
    public void Subscribe_ReturnsTokenWithCorrectEventType()
    {
        var token = _eventBus.Subscribe<SettingAppliedEvent>(_ => { });

        token.EventType.Should().Be(typeof(SettingAppliedEvent));
        token.SubscriptionId.Should().NotBeEmpty();
    }

    [Fact]
    public void Publish_HandlerThrows_LogsErrorAndContinues()
    {
        var secondCalled = false;
        _eventBus.Subscribe<SettingAppliedEvent>(_ => throw new InvalidOperationException("test error"));
        _eventBus.Subscribe<SettingAppliedEvent>(_ => secondCalled = true);

        _eventBus.Publish(new SettingAppliedEvent("id", true));

        secondCalled.Should().BeTrue();
        _mockLog.Verify(l => l.Log(
            LogLevel.Error,
            It.Is<string>(s => s.Contains("test error")),
            null), Times.Once);
    }

    [Fact]
    public void SubscribeAsync_PublishInvokesAsyncHandler()
    {
        var received = false;
        _eventBus.SubscribeAsync<SettingAppliedEvent>(async e =>
        {
            await Task.Delay(1);
            received = true;
        });

        _eventBus.Publish(new SettingAppliedEvent("id", true));

        // Give async handler time to complete
        Thread.Sleep(100);
        received.Should().BeTrue();
    }

    [Fact]
    public void SubscribeAsync_ReturnsTokenWithCorrectEventType()
    {
        var token = _eventBus.SubscribeAsync<ReviewModeExitedEvent>(async _ => await Task.CompletedTask);

        token.EventType.Should().Be(typeof(ReviewModeExitedEvent));
    }

    [Fact]
    public void Publish_OnlyNotifiesMatchingEventType()
    {
        var settingReceived = false;
        var reviewReceived = false;

        _eventBus.Subscribe<SettingAppliedEvent>(_ => settingReceived = true);
        _eventBus.Subscribe<ReviewModeExitedEvent>(_ => reviewReceived = true);

        _eventBus.Publish(new SettingAppliedEvent("id", true));

        settingReceived.Should().BeTrue();
        reviewReceived.Should().BeFalse();
    }

    [Fact]
    public void Unsubscribe_OneOfMany_OthersStillReceive()
    {
        var count1 = 0;
        var count2 = 0;
        var token1 = _eventBus.Subscribe<SettingAppliedEvent>(_ => count1++);
        _eventBus.Subscribe<SettingAppliedEvent>(_ => count2++);

        _eventBus.Unsubscribe(token1);
        _eventBus.Publish(new SettingAppliedEvent("id", true));

        count1.Should().Be(0);
        count2.Should().Be(1);
    }
}

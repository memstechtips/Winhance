using System.Runtime.CompilerServices;
using FluentAssertions;
using Winhance.UI.Features.Common.Services;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class LiveInstanceTrackerTests
{
    private sealed class Alpha;

    private sealed class Beta;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RegisterGarbage(LiveInstanceTracker tracker, int count)
    {
        for (var i = 0; i < count; i++)
            tracker.Register(new Alpha());
    }

    private static void CollectAll()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    [Fact]
    public void AliveCounts_CountsHeldInstancesByTypeName()
    {
        var tracker = new LiveInstanceTracker();
        var held = new List<object> { new Alpha(), new Alpha(), new Beta() };
        held.ForEach(tracker.Register);

        CollectAll();

        tracker.AliveCounts().Should().BeEquivalentTo(new Dictionary<string, int> { ["Alpha"] = 2, ["Beta"] = 1 });
        GC.KeepAlive(held);
    }

    [Fact]
    public void AliveCounts_DropsCollectedInstancesAndTypesWithNoneLeft()
    {
        var tracker = new LiveInstanceTracker();
        var kept = new Beta();
        tracker.Register(kept);
        RegisterGarbage(tracker, 5);

        CollectAll();

        tracker.AliveCounts().Should().BeEquivalentTo(new Dictionary<string, int> { ["Beta"] = 1 });
        GC.KeepAlive(kept);
    }

    [Fact]
    public void Register_WhenDisabled_TracksNothing()
    {
        var tracker = new LiveInstanceTracker { Enabled = false };
        var page = new Alpha();

        tracker.Register(page);
        tracker.RegisterOnce(page);

        tracker.AliveCounts().Should().BeEmpty();
        GC.KeepAlive(page);
    }

    [Fact]
    public void Shared_UntilDiagMemoryTurnsItOn_IsDisabled() =>
        LiveInstanceTracker.Shared.Enabled.Should().BeFalse();

    [Fact]
    public void RegisterOnce_CountsTheSameInstanceOnce()
    {
        var tracker = new LiveInstanceTracker();
        var page = new Alpha();

        tracker.RegisterOnce(page);
        tracker.RegisterOnce(page);

        tracker.AliveCounts()["Alpha"].Should().Be(1);
        GC.KeepAlive(page);
    }
}

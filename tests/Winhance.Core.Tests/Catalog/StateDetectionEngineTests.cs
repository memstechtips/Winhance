using Winhance.Core.Features.Common.Catalog;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

public class StateDetectionEngineTests
{
    // Simple in-memory readings: key -> (value, present). Missing key => absent.
    private sealed class FakeReadings : IStateReadings
    {
        private readonly Dictionary<string, object?> _present;
        public FakeReadings(Dictionary<string, object?> present) => _present = present;
        public bool TryGet(string key, out object? value, out bool present)
        {
            present = _present.TryGetValue(key, out value);
            return true;
        }
    }

    private static SettingState State(string label, Dictionary<string, StateValue> set,
        params StateRole[] roles) =>
        new() { Label = label, Set = set, Roles = roles };

    private static SettingState Fallback(string label, Dictionary<string, StateValue>? set = null) =>
        new() { Label = label, Set = set ?? new Dictionary<string, StateValue>(), IsFallback = true };

    [Fact]
    public void Detect_returns_the_single_matching_state()
    {
        var states = new[]
        {
            State("Hide",  new() { ["Mode"] = StateValue.Of(0) }, new StateRole(RoleKind.Recommended)),
            State("Icon",  new() { ["Mode"] = StateValue.Of(1) }),
            State("Box",   new() { ["Mode"] = StateValue.Of(2) }, new StateRole(RoleKind.WindowsDefault)),
        };
        var readings = new FakeReadings(new() { ["Mode"] = 1 });
        Assert.Equal("Icon", StateDetectionEngine.Detect(states, readings));
    }

    [Fact]
    public void Detect_requires_ALL_targets_in_a_state_to_match()
    {
        var states = new[]
        {
            State("Manual", new()
            {
                ["Start"]   = StateValue.Of(3),
                ["Preload"] = StateValue.Of(1),
            }),
        };
        var readings = new FakeReadings(new() { ["Start"] = 3, ["Preload"] = 0 });
        Assert.Null(StateDetectionEngine.Detect(states, readings));
    }

    [Fact]
    public void Detect_treats_absent_key_via_OrAbsent_as_a_match()
    {
        var states = new[]
        {
            State("Manual", new()
            {
                ["Start"]   = StateValue.Of(3).OrAbsent(),
                ["Preload"] = StateValue.Of(1).OrAbsent(),
            }, new StateRole(RoleKind.WindowsDefault)),
        };
        var readings = new FakeReadings(new());
        Assert.Equal("Manual", StateDetectionEngine.Detect(states, readings));
    }

    [Fact]
    public void Detect_returns_null_Custom_when_no_state_matches()
    {
        var states = new[]
        {
            State("On",  new() { ["K"] = StateValue.Of(1) }),
            State("Off", new() { ["K"] = StateValue.Of(0) }),
        };
        var readings = new FakeReadings(new() { ["K"] = 99 });
        Assert.Null(StateDetectionEngine.Detect(states, readings));
    }

    [Fact]
    public void Detect_resolves_to_fallback_state_when_nothing_else_matches()
    {
        var states = new[]
        {
            State("Programs disabled", new() { ["Prio"] = StateValue.Of(0x26) }),
            Fallback("Default", new() { ["Prio"] = StateValue.Of(2) }),
        };
        var unrecognised = new FakeReadings(new() { ["Prio"] = 0x18 });
        Assert.Equal("Default", StateDetectionEngine.Detect(states, unrecognised));
    }

    [Fact]
    public void Detect_returns_first_state_when_multiple_match()
    {
        var states = new[]
        {
            State("A", new() { ["K"] = StateValue.Exists }),
            State("B", new() { ["K"] = StateValue.Of(5) }),
        };
        var readings = new FakeReadings(new() { ["K"] = 5 });
        Assert.Equal("A", StateDetectionEngine.Detect(states, readings));
    }
}

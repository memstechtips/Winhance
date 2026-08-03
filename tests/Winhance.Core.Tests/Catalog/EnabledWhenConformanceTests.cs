using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Winhance.Core.Features.Common.Catalog;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

/// <summary>
/// The presentation gate is DECLARED, not inferred. Nesting a setting under a UiParentId says where its
/// card is drawn; whether it stops meaning anything in some of the parent's states is a fact about
/// Windows that only the setting's author knows, so it is written down in
/// <see cref="Setting.EnabledWhen"/> and keyed on the target's state LABEL.
///
/// What this replaced compared the parent's selected INDEX against zero, in two view-model methods. It
/// was right for gaming-sysmain-service by coincidence (its "off" state happens to be index 0) and wrong
/// for theme-mode-windows, whose index 0 is "Light Mode" - so every stock Windows 11 install opened the
/// Windows Theme page with both sub-toggles greyed out.
///
/// These tests pin the AUTHORING - which nested settings claim a gate and which deliberately do not -
/// because that is the part a future edit can silently get wrong. Machine-independent: pure catalog.
/// </summary>
public class EnabledWhenConformanceTests
{
    private static Setting S(string id) => SettingCatalog.All.First(s => s.Id == id);

    /// <summary>Every gate in the shipped catalog, child -> (target, the states it is usable in).</summary>
    public static readonly Dictionary<string, (string Target, string[] States)> Expected = new()
    {
        // Hibernation genuinely owns these: with hiberfil.sys gone there is no hibernate timeout to set,
        // no hybrid sleep, no fast startup, and nothing for the Start-menu entry to do.
        ["power-hibernate-timeout"] = ("power-hibernation-enable", new[] { "Enabled" }),
        ["power-hybrid-sleep"] = ("power-hibernation-enable", new[] { "Enabled" }),
        ["power-fast-startup"] = ("power-hibernation-enable", new[] { "Enabled" }),
        ["start-power-hibernate-option"] = ("power-hibernation-enable", new[] { "Enabled" }),

        // HVCI runs ON the hypervisor VBS starts.
        ["gaming-memory-integrity"] = ("gaming-virtualization-based-security", new[] { "Enabled" }),

        // The one non-toggle gate, and the reason the gate is keyed on labels: SysMain has THREE states
        // and prefetching is its job, so the child is usable in the two where the service can run.
        ["gaming-performance-prefetch"] =
            ("gaming-sysmain-service", new[] { "Manual", "Automatic (Recommended for HDD)" }),

        // Both children configure the SECONDARY taskbars, which do not exist when it is off.
        ["taskbar-multi-display-apps"] = ("taskbar-multi-display", new[] { "Enabled" }),
        ["taskbar-combine-buttons-other"] = ("taskbar-multi-display", new[] { "Enabled" }),

        // Toast sounds and lock-screen toasts are properties OF a toast; no toasts, nothing to shape.
        ["notifications-sound"] = ("windows-pushnotifications", new[] { "Enabled" }),
        ["notifications-toast-above-lock"] = ("windows-pushnotifications", new[] { "Enabled" }),
        ["notifications-critical-toast-above-lock"] = ("windows-pushnotifications", new[] { "Enabled" }),
    };

    /// <summary>Nested settings that deliberately declare NO gate - the other half of the authoring, and
    /// the half a "children of an off parent are dead" instinct keeps re-adding.</summary>
    public static readonly string[] DeliberatelyUngated =
    {
        // THE REPORTED BUG. The master is a preset over two independently meaningful facets; that they
        // can disagree is exactly why it needed a "Mixed" state.
        "theme-mode-apps",
        "theme-mode-system",

        // Six per-folder toggles under "show all folders": each writes its own NonEnum policy GUID,
        // which Windows honours independently of NavPaneShowAllFolders.
        "explorer-customization-nav-saf-desktop",
        "explorer-customization-nav-saf-documents",
        "explorer-customization-nav-saf-downloads",
        "explorer-customization-nav-saf-music",
        "explorer-customization-nav-saf-pictures",
        "explorer-customization-nav-saf-videos",

        // Spotlight images and the lock-screen overlay still render with lock-workstation policy set.
        "privacy-rotating-lock-screen",
        "privacy-lock-screen-overlay",

        // A tray-icon preference Explorer honours whether or not toasts are on.
        "notifications-show-bell-icon",
    };

    [Fact]
    public void Exactly_the_verified_gates_are_declared()
    {
        var declared = SettingCatalog.All
            .Where(s => s.EnabledWhen is not null)
            .Select(s => s.Id)
            .OrderBy(id => id, System.StringComparer.Ordinal);

        declared.Should().Equal(Expected.Keys.OrderBy(id => id, System.StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(GateCases))]
    public void Each_declared_gate_names_its_target_and_the_states_it_is_usable_in(
        string childId, string targetId, string[] states)
    {
        var gate = S(childId).EnabledWhen;

        gate.Should().NotBeNull();
        gate!.OtherId.Should().Be(targetId);
        gate.States.Should().Equal(states);
    }

    [Theory]
    [MemberData(nameof(GateCases))]
    public void Each_declared_gate_names_states_the_target_really_has(
        string childId, string targetId, string[] states)
    {
        // The whole point of keying on the label: a label that does not exist is a gate that can never
        // open. CatalogValidator enforces this catalog-wide; this says it for the gates by name.
        var labels = S(targetId).States.Select(st => st.Label).ToList();

        states.Should().BeSubsetOf(labels, $"'{childId}' is gated on them");
    }

    public static IEnumerable<object[]> GateCases() =>
        Expected.Select(kv => new object[] { kv.Key, kv.Value.Target, kv.Value.States });

    [Theory]
    [MemberData(nameof(UngatedCases))]
    public void A_nested_setting_that_declares_no_gate_stays_ungated(string childId)
    {
        var setting = S(childId);

        setting.UiParentId.Should().NotBeNullOrEmpty("this list is about settings that ARE nested");
        setting.EnabledWhen.Should().BeNull();
    }

    public static IEnumerable<object[]> UngatedCases() =>
        DeliberatelyUngated.Select(id => new object[] { id });

    [Fact]
    public void Every_nested_setting_is_accounted_for_as_gated_or_deliberately_ungated()
    {
        // Non-vacuity for the two lists above: a NEW child of an existing parent has to be a decision,
        // not a default. Whichever way it is authored, it belongs in one of the lists.
        var nested = SettingCatalog.All
            .Where(s => !string.IsNullOrEmpty(s.UiParentId))
            .Select(s => s.Id)
            .OrderBy(id => id, System.StringComparer.Ordinal);

        nested.Should().Equal(Expected.Keys.Concat(DeliberatelyUngated)
            .OrderBy(id => id, System.StringComparer.Ordinal));
    }

    [Fact]
    public void No_gate_names_a_setting_that_is_not_the_one_it_is_nested_under()
    {
        // Not a rule of the model - EnabledWhen may name any setting, and Link deliberately does point
        // at non-parents. It is a statement about TODAY'S catalog: every gate we could verify happens to
        // be a claim about the card's own UI parent. If that ever stops being true it should be a
        // conscious edit here, because a gate whose target lives on another PAGE cannot be read: the
        // feature view-model only sees its own settings, and an unreadable gate stays open.
        foreach (var s in SettingCatalog.All.Where(s => s.EnabledWhen is not null))
            s.EnabledWhen!.OtherId.Should().Be(s.UiParentId!, $"'{s.Id}' gates on its own parent");
    }

    [Fact]
    public void No_gate_names_a_separate_AC_DC_selection()
    {
        // A gate reads its target's CurrentStateLabel, and a Separate PowerCfg selection does not have
        // one: its live readings sit in the AC and DC indices, and its SelectedValue never resolves to a
        // state. Such a gate would silently never close. No shipped gate targets one; this keeps it that
        // way, because the failure is invisible - the card simply stays enabled forever.
        foreach (var s in SettingCatalog.All.Where(s => s.EnabledWhen is not null))
        {
            var target = S(s.EnabledWhen!.OtherId);

            target.Targets.OfType<PowerCfgTarget>()
                .Any(t => t.Mode == Winhance.Core.Features.Common.Models.PowerModeSupport.Separate)
                .Should().BeFalse($"'{s.Id}' gates on '{target.Id}'");
        }
    }
}

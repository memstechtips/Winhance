using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Tests.Services;
using Winhance.TestSupport;
using Xunit;
using Xunit.Abstractions;

namespace Winhance.Infrastructure.Tests.Catalog;

// For EVERY catalog setting, every reachable apply-request shape must resolve to a NON-null plan;
// special-handled settings are excluded (they never reach Resolve). Run: --filter ResolveTotalityAudit
public class ResolveTotalityAuditTests
{
    private readonly ITestOutputHelper _output;
    public ResolveTotalityAuditTests(ITestOutputHelper output) => _output = output;

    private const string Zones = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Time Zones";
    private const string Layouts = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Keyboard Layouts";

    // The reads the five time, region and language lists make. On an empty machine they offer and select nothing,
    // and the keyed arm would audit no shape at all.
    private static readonly FakeDetectionContext KeyedMachine = new FakeDetectionContext()
        .Sub(Zones, "UTC")
        .Set($@"{Zones}\UTC", "Display", "(UTC) Coordinated Universal Time")
        .Set(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\TimeZoneInformation", "TimeZoneKeyName", "UTC")
        .Set(@"HKEY_CURRENT_USER\Control Panel\International", "LocaleName", "en-US")
        .Set(@"HKEY_CURRENT_USER\Control Panel\International\Geo", "Nation", "244")
        .Sub(Layouts, "00000409")
        .Set($@"{Layouts}\00000409", "Layout Text", "US")
        .Set(@"HKEY_CURRENT_USER\Keyboard Layout\Preload", "1", "00000409")
        .Set(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Nls\Language", "Default", "0409");

    [Fact]
    public void ResolveTotalityAudit_NoReachableShapeReturnsNull()
    {
        var build = new WinBuild(22631); // representative live build (Win11); build-gated targets still emit non-null
        var nulls = new List<string>();
        var unaudited = new List<string>();

        // updates-policy-mode is fully served by the UpdateService special handler: its live (int-value) applies are
        // intercepted before ApplyOperationsAsync, and Resolve returns null for it anyway (a bare-state-effects
        // Detector setting). Its custom-state dict WOULD reach Resolve (the handler declines non-int values), but
        // UpdatePolicyDetector always yields one of its authored labels, so it never exports a Custom state -> that
        // shape is unreachable. (FRAGILITY: if UpdatePolicyDetector ever gains a Custom/unmatched branch, this
        // setting would export custom-state, reach Resolve, and return null - revisit this exclusion then.)
        // theme-mode-windows is NOT excluded: it is also special-handled (the Mixed state), but Resolve handles all its
        // shapes non-null (plain registry selection), so the audit legitimately covers it.
        var specialHandled = new HashSet<string> { "updates-policy-mode" };
        var providers = OptionProviderFixtures.Registry();

        foreach (var s in SettingCatalog.All)
        {
            if (specialHandled.Contains(s.Id) || s.IsAnswerFileOnly) continue;

            void Check(bool enable, object? value, bool reset, string shape)
            {
                var plan = ApplyRequestResolver.Resolve(s.Id, enable, value, reset, SettingCatalog.All, build, providers);
                if (plan is null)
                    nulls.Add($"{s.Id} [{s.Control}] {shape}");
            }

            switch (s.Control)
            {
                case ControlKind.Toggle:
                    Check(true, null, false, "enable");
                    Check(false, null, false, "disable");
                    Check(true, null, true, "reset-enable");
                    Check(false, null, true, "reset-disable");
                    break;

                case ControlKind.CheckBox:
                    Check(true, null, false, "check");
                    Check(false, null, false, "uncheck");
                    Check(true, null, true, "reset-check");
                    Check(false, null, true, "reset-uncheck");
                    break;

                case ControlKind.Selection:
                    for (int i = 0; i < s.States.Count; i++)
                    {
                        // A DETECT-ONLY state is not a reachable apply target and so is out of this audit's
                        // scope. It is not in the option list, so no producer can dispatch its index: the card
                        // cannot select it, config export writes the index only for a state the user chose, and
                        // the relationship reverse-sync's snap to it is intercepted by the setting's special
                        // handler before Resolve. Resolve also refuses one outright with an empty plan, because a
                        // detect-only state can carry a Set: theme-wallpaper's Spotlight state does, to be detected.
                        if (s.States[i].IsDetectOnly)
                            continue;
                        Check(true, i, false, $"idx{i}");
                        Check(true, i, true, $"reset-idx{i}");
                    }
                    bool pcfgSep = s.Targets.Count > 0 && s.Targets.All(t => t is PowerCfgTarget { Mode: PowerModeSupport.Separate });
                    if (pcfgSep)
                    {
                        var d = new Dictionary<string, object?> { ["ACValue"] = 0, ["DCValue"] = 0 };
                        Check(true, d, false, "acdc");
                        Check(true, d, true, "reset-acdc");
                        Check(true, (0, 0), false, "acdc-tuple");
                    }
                    var valueNames = s.Targets.OfType<RegTarget>().Where(r => r.ValueName != null).Select(r => r.ValueName!).ToList();
                    if (valueNames.Count > 0)
                    {
                        var cs = new Dictionary<string, object>();
                        foreach (var vn in valueNames) cs[vn] = 0;
                        Check(true, cs, false, "customstate");
                    }

                    // gaming-dns-server / taskbar-system-tray-icons-11: nothing registry-side to write, so the
                    // Custom state exports the reconstructed bag and the plan comes from CustomStateScripts.
                    if (s.CustomStateScripts.Count > 0 && !s.Targets.OfType<RegTarget>().Any())
                    {
                        Check(true, new Dictionary<string, object> { ["DetectedIndex"] = -1 }, false, "script-customstate");
                    }
                    break;

                case ControlKind.Slider:
                    var nd = new Dictionary<string, object?> { ["ACValue"] = 0, ["DCValue"] = 0 };
                    Check(true, nd, false, "acdc-num");
                    Check(true, nd, true, "reset-acdc-num");
                    break;

                case ControlKind.Action:
                    Check(true, null, false, "action");
                    break;

                case ControlKind.TextBox:
                    // An empty box is deliberately not a plan: there is no folder to hand the shell.
                    Check(true, @"D:\Albums\Trip", false, "folder");
                    break;

                case ControlKind.KeyedSelection:
                    // Predefined plans, shipped pictures and swatches are the same on every PC, so those lists are
                    // audited through them: the fixture machine has no power schemes, background or colour to read.
                    var list = s.Options!;
                    var reference = list.Source switch
                    {
                        OptionSource.PowerPlans => PowerPlanCatalog.BuiltInPowerPlans.Select(p => p.Guid).ToList(),
                        OptionSource.Pictures => s.States.Select(state => KeyedOptions.KeyOf(s, state)!).ToList(),
                        OptionSource.Colors => list.Keys.ToList(),
                        _ => [],
                    };
                    if (reference.Count > 0)
                    {
                        foreach (var key in reference)
                            Check(true, key, false, $"ref:{key}");
                        break;
                    }

                    // Two keys, not every option: a culture-backed list offers hundreds and they all take the same shape.
                    var provider = providers.For(list.Source);
                    var current = provider.CurrentKey(s, KeyedMachine);
                    var options = provider.Options(s, KeyedMachine);
                    var offered = options.Count > 0 ? options[0].Value : null;

                    // A null means the fixture lacks a read this list makes, and the audit would pass unchecked.
                    if (current is null) unaudited.Add($"{s.Id} reads back no key");
                    else Check(true, current, false, $"key:{current}");

                    if (offered is null) unaudited.Add($"{s.Id} offers no key");
                    else if (offered != current) Check(true, offered, false, $"key:{offered}");
                    break;
            }
        }

        foreach (var n in nulls)
            _output.WriteLine($"[NULL] {n}");
        _output.WriteLine($"{nulls.Count} reachable shape(s) return null across {SettingCatalog.All.Count} settings");

        Assert.True(unaudited.Count == 0,
            "a keyed list resolved no key against the fixture machine, so its apply shape went unaudited:\n  "
            + string.Join("\n  ", unaudited));
        Assert.True(nulls.Count == 0, $"{nulls.Count} reachable apply-request shapes return null - see [NULL] rows");
    }
}

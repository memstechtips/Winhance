using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Xunit;
using Xunit.Abstractions;

namespace Winhance.Infrastructure.Tests.Catalog;

/// <summary>Deletion precondition gate: for EVERY catalog setting, exercise the reachable apply-request shapes its
/// producers (per-card VM handlers, bulk apply/reset, config-import bridge, relationship cascade) can dispatch, and
/// assert ApplyRequestResolver.Resolve returns a NON-null plan for each. Green => every reachable input is handled by
/// the new engine, so the old SettingOperationExecutor fallback is dead and safe to delete. Special-handled settings
/// (intercepted before ApplyOperationsAsync) are excluded - they never reach Resolve. Run: --filter ResolveTotalityAudit</summary>
public class ResolveTotalityAuditTests
{
    private readonly ITestOutputHelper _output;
    public ResolveTotalityAuditTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void ResolveTotalityAudit_NoReachableShapeReturnsNull()
    {
        var build = new WinBuild(22631); // representative live build (Win11); build-gated targets still emit non-null
        var nulls = new List<string>();

        // updates-policy-mode is fully served by the UpdateService special handler: its live (int-value) applies are
        // intercepted before ApplyOperationsAsync, and Resolve returns null for it anyway (a bare-state-effects
        // Detector setting). Its custom-state dict WOULD reach Resolve (the handler declines non-int values), but
        // UpdatePolicyDetector always yields one of its authored labels, so it never exports a Custom state -> that
        // shape is unreachable. (FRAGILITY: if UpdatePolicyDetector ever gains a Custom/unmatched branch, this
        // setting would export custom-state, reach Resolve, and return null - revisit this exclusion then.)
        // theme-mode-windows is NOT excluded: it is also special-handled (wallpaper), but Resolve handles all its
        // shapes non-null (plain registry selection), so the audit legitimately covers it.
        var specialHandled = new HashSet<string> { "updates-policy-mode" };

        foreach (var s in SettingCatalog.All)
        {
            if (specialHandled.Contains(s.Id)) continue;

            void Check(bool enable, object? value, bool reset, string shape)
            {
                var plan = ApplyRequestResolver.Resolve(s.Id, enable, value, reset, SettingCatalog.All, build);
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

                case ControlKind.Selection:
                    for (int i = 0; i < s.States.Count; i++)
                    {
                        // A DETECT-ONLY state is not a reachable apply target and so is out of this audit's
                        // scope. It is not in the option list, so no producer can dispatch its index: the card
                        // cannot select it, config export writes the index only for a state the user chose, and
                        // the relationship reverse-sync's snap to it is intercepted by the setting's special
                        // handler before Resolve. Auditing it would prove nothing either - the state carries no
                        // Set, so Resolve returns an EMPTY (non-null) plan and the assertion passes vacuously.
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
                    break;

                case ControlKind.Slider:
                    var nd = new Dictionary<string, object?> { ["ACValue"] = 0, ["DCValue"] = 0 };
                    Check(true, nd, false, "acdc-num");
                    Check(true, nd, true, "reset-acdc-num");
                    break;

                case ControlKind.Action:
                    Check(true, null, false, "action");
                    break;

                case ControlKind.PowerPlan:
                    Check(true, "381b4222-f694-41f0-9685-ff5bb260df2e", false, "guid");
                    Check(true, new Dictionary<string, object> { ["Guid"] = "381b4222-f694-41f0-9685-ff5bb260df2e", ["Name"] = "Balanced" }, false, "guid-name-dict");
                    break;
            }
        }

        foreach (var n in nulls)
            _output.WriteLine($"[NULL] {n}");
        _output.WriteLine($"{nulls.Count} reachable shape(s) return null across {SettingCatalog.All.Count} settings");

        Assert.True(nulls.Count == 0, $"{nulls.Count} reachable apply-request shapes return null - see [NULL] rows");
    }
}

using System.Text;
using System.Text.RegularExpressions;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Selections;
using Winhance.Infrastructure.Features.Common.Helpers;
using static Winhance.Infrastructure.Features.AdvancedTools.Helpers.PowerShellScriptUtilities;

namespace Winhance.Infrastructure.Features.AdvancedTools.Helpers;

internal sealed record PowerCfgRow(string SubgroupGuid, string SettingGuid, int Ac, int Dc, string Description);

internal sealed record EmitResult(
    IReadOnlyDictionary<string, string> SystemPassByFeature,
    IReadOnlyDictionary<string, string> UserPassByFeature,
    IReadOnlyList<PowerCfgRow> PowerRows,
    ChoiceValue.PowerPlan? PowerPlan,
    IReadOnlyList<string> Warnings);

// Runs every chosen setting through the SAME resolver + plan builder the live app applies with, then writes one
// PowerShell statement per ApplyOp. The autounattend therefore cannot drift from the apply engine: a new op kind
// shows up here as a warning instead of silently emitting nothing.
internal sealed class ApplyOpScriptEmitter
{
    // The one setting whose apply is a powercfg verb rather than anything the plan builder can express, so the
    // autounattend has always emitted it out of band.
    private const string HibernationSettingId = "power-hibernation-enable";

    // Matches .reg section headers like `[HKEY_CURRENT_USER\Software\...]` at the start of a line.
    // Headers are the only syntactic indicator of target hive in a .reg file - comments and REG_SZ
    // values can contain "HKCU" as plain text without affecting import behavior.
    private static readonly Regex s_hkcuHeaderRegex = new(
        @"^\s*\[HKEY_CURRENT_USER\\",
        RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

    private static readonly Regex s_systemHiveHeaderRegex = new(
        @"^\s*\[(HKEY_LOCAL_MACHINE|HKEY_CLASSES_ROOT|HKEY_USERS|HKEY_CURRENT_CONFIG)\\",
        RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

    private readonly ILogService _log;

    public ApplyOpScriptEmitter(ILogService log)
    {
        _log = log;
    }

    public EmitResult Emit(
        SelectionSet set,
        IReadOnlyDictionary<string, IReadOnlyList<Setting>> byFeature,
        WinBuild build,
        string systemIndent,
        string userIndent)
    {
        var catalog = byFeature.Values.SelectMany(x => x).ToList();
        var byId = new Dictionary<string, Setting>();
        var featureOf = new Dictionary<string, string>();
        foreach (var (featureId, settings) in byFeature)
        {
            foreach (var setting in settings)
            {
                byId[setting.Id] = setting;
                featureOf[setting.Id] = featureId;
            }
        }

        var features = new Dictionary<string, FeatureText>();
        var featureOrder = new List<string>();
        var rows = new List<PowerCfgRow>();
        var warnings = new List<string>();
        ChoiceValue.PowerPlan? powerPlan = null;

        FeatureText TextFor(string featureId)
        {
            if (!features.TryGetValue(featureId, out var text))
            {
                text = new FeatureText();
                features[featureId] = text;
                featureOrder.Add(featureId);
            }
            return text;
        }

        foreach (var choice in set.Settings)
        {
            if (!byId.TryGetValue(SettingIdAliases.Normalize(choice.SettingId), out var setting))
            {
                Warn(warnings, $"Could not find catalog Setting for: {choice.SettingId}");
                continue;
            }

            // The power plan is created and activated by the power section, not by an apply plan - the resolver
            // would hand back a PowerPlanActivateOp that has no place in either registry pass.
            if (choice.Value is ChoiceValue.PowerPlan plan)
            {
                powerPlan = plan;
                continue;
            }

            // An Action expresses no "disabled" semantic, so an unselected one emits nothing at all.
            if (choice.Value is ChoiceValue.Toggle { On: false } && setting.Control == ControlKind.Action)
                continue;

            var text = TextFor(featureOf[setting.Id]);

            if (setting.Id == HibernationSettingId && choice.Value is ChoiceValue.Toggle hibernation)
                text.HibernationStates.Add(hibernation.On ? "on" : "off");

            var (enable, value) = ResolverArguments(setting, choice.Value);
            var ops = ApplyRequestResolver.Resolve(setting.Id, enable, value, resetToDefault: false, catalog, build)
                ?? DetectorStateOps(setting, choice.Value, build);
            if (ops is null)
            {
                Warn(warnings, $"Setting '{setting.Id}' with value {choice.Value} did not resolve to an apply plan");
                continue;
            }

            EmitOps(ops, setting, choice.Value, text, systemIndent, userIndent, rows, warnings);
        }

        var systemPass = new Dictionary<string, string>();
        var userPass = new Dictionary<string, string>();
        foreach (var featureId in featureOrder)
        {
            var text = features[featureId];

            // The order the old emitter produced: every item's own lines, then hibernation, then one batch
            // covering the whole feature's scheduled tasks.
            foreach (var state in text.HibernationStates)
                AppendHibernation(text.System, systemIndent, state);

            if (text.Tasks.Count > 0)
                AppendScheduledTaskBatch(text.System, text.Tasks, systemIndent);

            if (text.System.Length > 0)
                systemPass[featureId] = text.System.ToString();
            if (text.User.Length > 0)
                userPass[featureId] = text.User.ToString();
        }

        return new EmitResult(systemPass, userPass, rows, powerPlan, warnings);
    }

    // Ops arrive in plan order - the registry/task/powercfg ops per target in Targets order, then the state's
    // Effects in authored order - and the script keeps that order, so a script effect still runs after the
    // registry writes it depends on. ApplyPlan.From only partitions async effects out for the live runner, which
    // is why the raw op list is what gets walked here.
    private void EmitOps(
        IReadOnlyList<ApplyOp> ops,
        Setting setting,
        ChoiceValue choice,
        FeatureText text,
        string systemIndent,
        string userIndent,
        List<PowerCfgRow> rows,
        List<string> warnings)
    {
        var desc = EscapePowerShellString(setting.Display.Description)!;
        var logDesc = EscapeForDoubleQuotedString(setting.Display.Description);
        var acValues = new Dictionary<PowerCfgTarget, int>();
        var dcValues = new Dictionary<PowerCfgTarget, int>();
        var powerTargets = new List<PowerCfgTarget>();

        foreach (var op in ops)
        {
            switch (op)
            {
                case PowerCfgSetOp pc:
                    if (!powerTargets.Contains(pc.Target))
                        powerTargets.Add(pc.Target);
                    if (pc.Context == PowerContext.DC)
                        dcValues[pc.Target] = pc.Value;
                    else
                        acValues[pc.Target] = pc.Value;
                    break;

                case TaskSetOp t:
                    text.Tasks.Add((t.Target.TaskPath, t.Enabled ? "/Enable" : "/Disable", setting.Display.Description));
                    break;

                case EffectOp { Effect: ScriptEffect s }:
                    // The state's script is already baked with its option's variables; the live engine runs it
                    // verbatim, so no placeholder pass happens here either.
                    if (s.Run == RunContext.User)
                        AppendScript(text.User, userIndent, setting, s.Script, logDesc);
                    else
                        AppendScript(text.System, systemIndent, setting, s.Script, logDesc);
                    break;

                case EffectOp { Effect: RegContentEffect r }:
                    if (string.IsNullOrEmpty(r.Content))
                        break;
                    // Reject mixed-hive blocks: the emitter routes to a single pass per block, so a block
                    // containing both HKCU and HKLM/HKCR/HKU/HKCC headers would silently lose half its
                    // content under the hive filter below. Authors must split such content into separate
                    // RegContentEffect entries.
                    if (s_hkcuHeaderRegex.IsMatch(r.Content) && s_systemHiveHeaderRegex.IsMatch(r.Content))
                    {
                        throw new InvalidOperationException(
                            $"RegContentEffect for '{setting.Id}' mixes HKEY_CURRENT_USER and system-hive " +
                            $"section headers in a single block. Split it into one RegContentEffect per hive " +
                            $"so each can be routed to the correct autounattend pass.");
                    }
                    if (s_hkcuHeaderRegex.IsMatch(r.Content))
                        AppendRegContent(text.User, userIndent, setting, r.Content, logDesc);
                    else
                        AppendRegContent(text.System, systemIndent, setting, r.Content, logDesc);
                    break;

                // The script builder already warns once per setting for a native power effect, and emits the
                // wallpaper block unconditionally for the theme feature.
                case EffectOp { Effect: NativePowerEffect }:
                case EffectOp { Effect: WallpaperEffect }:
                    break;

                // ApplyPlanBuilder.BuildAction turns an Action's RegistryWriteEffects into RegistryWriteOps, so
                // this arm only fires for one sitting in a STATE's Effects.
                case EffectOp { Effect: RegistryWriteEffect rw }:
                    AppendRegistry(
                        text,
                        systemIndent,
                        userIndent,
                        rw.Path,
                        new RegistryWriteOp(new RegTarget(rw.ValueName, new[] { rw.Path }, rw.ValueName, rw.Kind), rw.Path, rw.Value),
                        desc);
                    break;

                case EffectOp fx:
                    Warn(warnings, $"Effect {fx.Effect.GetType().Name} on '{setting.Id}' has no PowerShell emission");
                    break;

                default:
                    if (RegistryPathOf(op) is { } path)
                        AppendRegistry(text, systemIndent, userIndent, path, op, desc);
                    else if (op is not PowerPlanActivateOp)
                        Warn(warnings, $"ApplyOp {op.GetType().Name} on '{setting.Id}' has no PowerShell emission");
                    break;
            }
        }

        foreach (var target in powerTargets)
        {
            if (!acValues.TryGetValue(target, out var ac))
                continue;
            int dc = dcValues.TryGetValue(target, out var dcValue) ? dcValue : ac;

            // The resolver takes display units, so a slider value went system -> display (integer division) -> system
            // on the way here; a machine-read 90 s would come back as 60. The choice still holds the exact system
            // value, and powercfg wants system units, so the row takes it from the choice.
            switch (choice)
            {
                case ChoiceValue.Number n: ac = n.Value; dc = n.Value; break;
                case ChoiceValue.AcDcNumber acdc: ac = acdc.Ac; dc = acdc.Dc; break;
            }

            rows.Add(new PowerCfgRow(target.SubgroupGuid, target.SettingGuid, ac, dc, setting.Display.Description));
        }
    }

    private static void AppendRegistry(FeatureText text, string systemIndent, string userIndent, string path, ApplyOp op, string desc)
    {
        if (path.StartsWith("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase))
            AppendRegistryOp(text.User, userIndent, op, desc);
        else
            AppendRegistryOp(text.System, systemIndent, op, desc);
    }

    private static string? RegistryPathOf(ApplyOp op) => op switch
    {
        RegistryWriteOp w => w.Path,
        RegistryDeleteOp d => d.Path,
        RegistryEnsureKeyOp e => e.Path,
        RegistryUnlockKeyOp u => u.Path,
        RegistryLockKeyOp l => l.Path,
        RegistryBitSetOp b => b.Path,
        RegistryByteSetOp y => y.Path,
        RegistryStringFlagSetOp f => f.Path,
        RegistryCompositeSetOp c => c.Path,
        RegistryPerSubkeyWriteOp p => p.ParentPath,
        RegistryPerSubkeyDeleteOp p => p.ParentPath,
        _ => null,
    };

    private static void AppendRegistryOp(StringBuilder sb, string indent, ApplyOp op, string desc)
    {
        switch (op)
        {
            // A value-less RegTarget controls KEY existence. An empty-string payload additionally writes the
            // key's (Default) value, which is how a shell-extension CLSID block is spelled.
            case RegistryWriteOp keyOp when string.IsNullOrEmpty(keyOp.Target.ValueName):
                sb.AppendLine($"{indent}New-RegistryKey -Path '{EscapedPath(keyOp.Path)}' -Description '{desc}'");
                if (keyOp.Value is string keyDefault && keyDefault.Length == 0)
                    sb.AppendLine($"{indent}Set-RegistryValue -Path '{EscapedPath(keyOp.Path)}' -Name '(Default)' -Type 'String' -Value '' -Description '{desc}'");
                break;

            case RegistryWriteOp emptyOp when emptyOp.Value is string empty && empty.Length == 0:
                sb.AppendLine($"{indent}Set-RegistryValue -Path '{EscapedPath(emptyOp.Path)}' -Name '{EscapedName(emptyOp.Target)}' -Type 'String' -Value '' -Description '{desc}'");
                break;

            case RegistryWriteOp w:
                sb.AppendLine($"{indent}Set-RegistryValue -Path '{EscapedPath(w.Path)}' -Name '{EscapedName(w.Target)}' -Type '{ConvertToRegistryType(w.Target.Type)}' -Value {FormatValueForPowerShell(w.Value, w.Target.Type)} -Description '{desc}'");
                break;

            case RegistryDeleteOp deleteKey when string.IsNullOrEmpty(deleteKey.Target.ValueName):
                sb.AppendLine($"{indent}Remove-RegistryKey -Path '{EscapedPath(deleteKey.Path)}' -Description '{desc}'");
                break;

            case RegistryDeleteOp d:
                sb.AppendLine($"{indent}Remove-RegistryValue -Path '{EscapedPath(d.Path)}' -Name '{EscapedName(d.Target)}' -Description '{desc}'");
                break;

            case RegistryEnsureKeyOp e:
                sb.AppendLine($"{indent}New-RegistryKey -Path '{EscapedPath(e.Path)}' -Description '{desc}'");
                break;

            case RegistryUnlockKeyOp u:
                sb.AppendLine($"{indent}Unlock-RegistryKey -Path '{EscapedPath(u.Path)}' -Description '{desc}'");
                break;

            case RegistryLockKeyOp l:
                sb.AppendLine($"{indent}Lock-RegistryKey -Path '{EscapedPath(l.Path)}' -Description '{desc}'");
                break;

            case RegistryBitSetOp b:
                sb.AppendLine($"{indent}Set-BinaryBit -Path '{EscapedPath(b.Path)}' -Name '{EscapedName(b.Target)}' -ByteIndex {b.ByteIndex} -BitMask 0x{b.BitMask:X2} -SetBit ${b.Set} -Description '{desc}'");
                break;

            case RegistryByteSetOp y:
                sb.AppendLine($"{indent}Set-BinaryByte -Path '{EscapedPath(y.Path)}' -Name '{EscapedName(y.Target)}' -ByteIndex {y.ByteIndex} -ByteValue 0x{y.Value:X2} -Description '{desc}'");
                break;

            case RegistryStringFlagSetOp f:
                sb.AppendLine($"{indent}Set-RegistryStringFlag -Path '{EscapedPath(f.Path)}' -Name '{EscapedName(f.Target)}' -FlagMask {f.FlagMask} -AbsentBase {f.AbsentBase} -Set ${f.Set} -Description '{desc}'");
                break;

            case RegistryCompositeSetOp c:
                sb.AppendLine(c.SubValue is null
                    ? $"{indent}Set-RegistryCompositeValue -Path '{EscapedPath(c.Path)}' -Name '{EscapedName(c.Target)}' -Key '{EscapePowerShellString(c.CompositeKey)}' -Remove -Description '{desc}'"
                    : $"{indent}Set-RegistryCompositeValue -Path '{EscapedPath(c.Path)}' -Name '{EscapedName(c.Target)}' -Key '{EscapePowerShellString(c.CompositeKey)}' -SubValue '{EscapePowerShellString(c.SubValue)}' -Description '{desc}'");
                break;

            // Enumeration is deferred to install time. ApplyPlanBuilder never routes a bit/byte/composite target
            // through the per-subkey ops, so the plain value write is the only shape reachable inside the loop.
            case RegistryPerSubkeyWriteOp subkeyWrite:
                sb.AppendLine($"{indent}Get-ChildItem -Path '{EscapedPath(subkeyWrite.ParentPath)}' -ErrorAction SilentlyContinue | ForEach-Object {{");
                sb.AppendLine($"{indent}    Set-RegistryValue -Path $_.PSPath -Name '{EscapedName(subkeyWrite.Target)}' -Type '{ConvertToRegistryType(subkeyWrite.Target.Type)}' -Value {FormatValueForPowerShell(subkeyWrite.Value, subkeyWrite.Target.Type)} -Description '{desc}'");
                sb.AppendLine($"{indent}}}");
                break;

            case RegistryPerSubkeyDeleteOp subkeyDelete:
                sb.AppendLine($"{indent}Get-ChildItem -Path '{EscapedPath(subkeyDelete.ParentPath)}' -ErrorAction SilentlyContinue | ForEach-Object {{");
                sb.AppendLine($"{indent}    Remove-RegistryValue -Path $_.PSPath -Name '{EscapedName(subkeyDelete.Target)}' -Description '{desc}'");
                sb.AppendLine($"{indent}}}");
                break;
        }
    }

    private static void AppendScript(StringBuilder sb, string indent, Setting setting, string script, string desc)
    {
        if (string.IsNullOrEmpty(script))
            return;

        sb.AppendLine();
        sb.AppendLine($"{indent}# PowerShell script for: {setting.Display.Name}");
        sb.AppendLine($"{indent}try {{");
        AppendScriptBody(sb, indent, script);
        sb.AppendLine($"{indent}    Write-Log \"{desc}\" \"SUCCESS\"");
        sb.AppendLine($"{indent}}} catch {{");
        sb.AppendLine($"{indent}    Write-Log \"Failed: {desc} - $($_.Exception.Message)\" \"ERROR\"");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine();
    }

    // A here-string is literal: PowerShell only ends one on a line whose FIRST character is the terminator,
    // so neither its body nor its '@ may be re-indented. Indenting the terminator swallows the rest of the file.
    private static void AppendScriptBody(StringBuilder sb, string indent, string script)
    {
        string? terminator = null;
        foreach (var line in script.Split('\n'))
        {
            var trimmedLine = line.Trim();
            if (terminator is not null)
            {
                if (trimmedLine == terminator)
                {
                    sb.AppendLine(terminator);
                    terminator = null;
                }
                else
                {
                    sb.AppendLine(line.TrimEnd('\r'));
                }

                continue;
            }

            if (string.IsNullOrEmpty(trimmedLine))
                continue;

            sb.AppendLine($"{indent}    {trimmedLine}");
            if (trimmedLine.EndsWith("@'", StringComparison.Ordinal))
                terminator = "'@";
            else if (trimmedLine.EndsWith("@\"", StringComparison.Ordinal))
                terminator = "\"@";
        }
    }

    private static void AppendRegContent(StringBuilder sb, string indent, Setting setting, string content, string desc)
    {
        var varName = SanitizeVariableName(setting.Id);

        sb.AppendLine($"{indent}try {{");
        sb.AppendLine($"{indent}    $regContent_{varName} = @'");
        sb.AppendLine(content);
        sb.AppendLine("'@");
        sb.AppendLine($"{indent}    $tempRegFile = Join-Path $env:TEMP \"winhance_{setting.Id}_$((Get-Date).Ticks).reg\"");
        sb.AppendLine($"{indent}    $regContent_{varName} | Out-File -FilePath $tempRegFile -Encoding Unicode -Force");
        sb.AppendLine($"{indent}    reg import \"$tempRegFile\" 2>&1 | Out-Null");
        sb.AppendLine($"{indent}    if ($LASTEXITCODE -eq 0) {{");
        sb.AppendLine($"{indent}        Write-Log \"{desc}\" \"SUCCESS\"");
        sb.AppendLine($"{indent}    }} else {{");
        sb.AppendLine($"{indent}        Write-Log \"Failed to import registry content for {desc}\" \"ERROR\"");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine($"{indent}    Remove-Item $tempRegFile -Force -ErrorAction SilentlyContinue");
        sb.AppendLine($"{indent}}} catch {{");
        sb.AppendLine($"{indent}    Write-Log \"Error processing registry content for {desc}: $($_.Exception.Message)\" \"ERROR\"");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine();
    }

    private static void AppendScheduledTaskBatch(StringBuilder sb, List<(string TaskName, string Action, string Description)> tasks, string indent)
    {
        sb.AppendLine();
        sb.AppendLine($"{indent}$scheduledTasks = @(");

        for (int i = 0; i < tasks.Count; i++)
        {
            var (taskName, action, description) = tasks[i];
            var escapedTaskName = EscapeForDoubleQuotedString(taskName);
            var escapedDescription = EscapeForDoubleQuotedString(description);
            var comma = i < tasks.Count - 1 ? "," : "";

            sb.AppendLine($"{indent}    @{{ TN=\"{escapedTaskName}\"; Action=\"{action}\"; Desc=\"{escapedDescription}\" }}{comma}");
        }

        sb.AppendLine($"{indent})");
        sb.AppendLine();
        sb.AppendLine($"{indent}Write-Log \"Applying scheduled task settings...\" \"INFO\"");
        sb.AppendLine($"{indent}$processedCount = 0");
        sb.AppendLine($"{indent}foreach ($task in $scheduledTasks) {{");
        sb.AppendLine($"{indent}    try {{");
        sb.AppendLine($"{indent}        $result = & cmd.exe /c \"schtasks /Change /TN `\"$($task.TN)`\" $($task.Action)\" 2>&1");
        sb.AppendLine($"{indent}        if ($LASTEXITCODE -eq 0) {{");
        sb.AppendLine($"{indent}            Write-Log \"$($task.Desc)\" \"SUCCESS\"");
        sb.AppendLine($"{indent}            $processedCount++");
        sb.AppendLine($"{indent}        }} else {{");
        sb.AppendLine($"{indent}            Write-Log \"Task command failed for: $($task.Desc)\" \"WARNING\"");
        sb.AppendLine($"{indent}        }}");
        sb.AppendLine($"{indent}    }} catch {{");
        sb.AppendLine($"{indent}        Write-Log \"Failed to process task: $($task.Desc) - $($_.Exception.Message)\" \"ERROR\"");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine($"{indent}Write-Log \"Processed $processedCount scheduled task settings\" \"SUCCESS\"");
        sb.AppendLine();
    }

    private static void AppendHibernation(StringBuilder sb, string indent, string state)
    {
        sb.AppendLine();
        sb.AppendLine($"{indent}Write-Log \"Setting hibernation to {state}...\" \"INFO\"");
        sb.AppendLine($"{indent}powercfg /hibernate {state} 2>$null");
        sb.AppendLine($"{indent}Write-Log \"Hibernation set to {state}\" \"SUCCESS\"");
    }

    // A ChoiceValue holds SYSTEM units; the resolver's Slider branch converts display units back to system units
    // through Numeric.Units, so a numeric choice has to be handed over in DISPLAY units to survive the round trip.
    private static (bool Enable, object? Value) ResolverArguments(Setting setting, ChoiceValue value)
    {
        switch (value)
        {
            case ChoiceValue.Toggle t:
                return (t.On, null);
            case ChoiceValue.Option o:
                return (true, o.Index);
            case ChoiceValue.AcDcOption a:
                return (true, (a.AcIndex, a.DcIndex));
            case ChoiceValue.CustomValues c:
                return (true, new Dictionary<string, object>(c.Values));
            case ChoiceValue.Number n:
                return (true, NumericArgument(setting, n.Value, n.Value));
            case ChoiceValue.AcDcNumber an:
                return (true, NumericArgument(setting, an.Ac, an.Dc));
            default:
                return (true, null);
        }
    }

    // A custom-detector setting with bare states (updates-policy-mode) is special-handled in the live app, so the
    // resolver declines it; that handler's registry half is ApplyPlanBuilder.Build on the chosen state, which is
    // what the autounattend writes too. Its service/task/DLL half is the script builder's Update hardening block.
    private static IReadOnlyList<ApplyOp>? DetectorStateOps(Setting setting, ChoiceValue value, WinBuild build)
    {
        if (setting.Detector is null) return null;
        string? label = value switch
        {
            ChoiceValue.Option o when o.Index >= 0 && o.Index < setting.States.Count => setting.States[o.Index].Label,
            ChoiceValue.Toggle t => t.On ? "Enabled" : "Disabled",
            _ => null,
        };
        return label is not null && setting.States.Any(st => st.Label == label)
            ? ApplyPlanBuilder.Build(setting, label, build)
            : null;
    }

    private static Dictionary<string, object?> NumericArgument(Setting setting, int ac, int dc)
    {
        if (setting.Numeric is null)
            return new Dictionary<string, object?> { ["ACValue"] = ac, ["DCValue"] = dc };

        var units = RecommendedSettingsResolver.GetPowerCfgDisplayUnits(setting);
        return new Dictionary<string, object?>
        {
            ["ACValue"] = RecommendedSettingsResolver.ConvertSystemToDisplayUnits(ac, units),
            ["DCValue"] = RecommendedSettingsResolver.ConvertSystemToDisplayUnits(dc, units),
        };
    }

    private static string EscapedPath(string path) => EscapePowerShellString(ConvertRegistryPath(path))!;

    private static string EscapedName(RegTarget target) => EscapePowerShellString(target.ValueName) ?? string.Empty;

    private void Warn(List<string> warnings, string message)
    {
        warnings.Add(message);
        _log.Log(LogLevel.Warning, message);
    }

    private sealed class FeatureText
    {
        public StringBuilder System { get; } = new();

        public StringBuilder User { get; } = new();

        public List<(string TaskName, string Action, string Description)> Tasks { get; } = new();

        public List<string> HibernationStates { get; } = new();
    }
}

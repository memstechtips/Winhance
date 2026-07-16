using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using static Winhance.Infrastructure.Features.AdvancedTools.Helpers.PowerShellScriptUtilities;

namespace Winhance.Infrastructure.Features.AdvancedTools.Helpers;

/// <summary>
/// Emits PowerShell registry commands for toggle and selection settings.
/// Eliminates duplication between toggle command emission and selection value resolution.
/// </summary>
internal class RegistryCommandEmitter
{
    private readonly ILogService _logService;

    private static object? GetWriteValue(object?[]? values) => values?.FirstOrDefault(v => v != null);

    public RegistryCommandEmitter(ILogService logService)
    {
        _logService = logService;
    }

    /// <summary>Emits the RegTarget's registry writes (Set-BinaryBit / Set-BinaryByte / Set-RegistryValue),
    /// reading its Type / ByteIndex / BitMask / ByteOnly.</summary>
    public void EmitRegistryValueFromTarget(
        StringBuilder sb,
        RegTarget rt,
        object value,
        string escapedDescription,
        string pathExpr,
        string escapedValueName,
        string indent)
    {
        var valueType = ConvertToRegistryType(rt.Type);

        if (rt.Type == RegistryValueKind.Binary && rt.ByteIndex.HasValue)
        {
            if (rt.BitMask.HasValue)
            {
                var setBit = Convert.ToBoolean(value);
                sb.AppendLine($"{indent}Set-BinaryBit -Path {pathExpr} -Name '{escapedValueName}' -ByteIndex {rt.ByteIndex.Value} -BitMask 0x{rt.BitMask.Value:X2} -SetBit ${setBit} -Description '{escapedDescription}'");
            }
            else if (rt.ByteOnly)
            {
                var byteValue = value switch
                {
                    byte b => $"0x{b:X2}",
                    int i => $"0x{(byte)i:X2}",
                    _ => "0x00"
                };
                sb.AppendLine($"{indent}Set-BinaryByte -Path {pathExpr} -Name '{escapedValueName}' -ByteIndex {rt.ByteIndex.Value} -ByteValue {byteValue} -Description '{escapedDescription}'");
            }
            else
            {
                var formattedValue = FormatValueForPowerShell(value, rt.Type);
                sb.AppendLine($"{indent}Set-RegistryValue -Path {pathExpr} -Name '{escapedValueName}' -Type '{valueType}' -Value {formattedValue} -Description '{escapedDescription}'");
            }
        }
        else
        {
            var formattedValue = FormatValueForPowerShell(value, rt.Type);
            sb.AppendLine($"{indent}Set-RegistryValue -Path {pathExpr} -Name '{escapedValueName}' -Type '{valueType}' -Value {formattedValue} -Description '{escapedDescription}'");
        }
    }

    /// <summary>Phase 6.8 F2b: byte-equivalent new-catalog mirror of AppendToggleCommandsFiltered's REGISTRY
    /// emission. Sources the write decision from the catalog Setting's active SettingState (the "Enabled" state
    /// when configItem.IsSelected is true, else the "Disabled" state) and its RegTargets instead of the old
    /// SettingDefinition.RegistrySettings' EnabledValue/DisabledValue. A mirror RegTarget with N Paths reproduces
    /// N old single-KeyPath RegistrySettings, so each path emits one command in order. The per-target write value
    /// is the active state's StateValue.WritePayload, or null when that StateValue deletes (Absent/DeleteOnWrite)
    /// or the state carries no entry for the target - matching the old GetWriteValue(EnabledValue/DisabledValue)
    /// returning the first non-null entry or null. This method emits ONLY registry targets; the RegContents tail
    /// is left to the call site. Proven at migration by the now-retired ScriptGenToggleEquivalenceTests.</summary>
    public void AppendToggleCommandsFromCatalog(StringBuilder sb, Winhance.Core.Features.Common.Catalog.Setting catalogSetting, ConfigurationItem configItem, bool isHkcu, string indent = "", Winhance.Core.Features.Common.Catalog.WinBuild? build = null)
    {
        var escapedDescription = EscapePowerShellString(catalogSetting.Display.Description);
        var isEnabled = configItem.IsSelected;

        // A toggle Setting has exactly two states, Label "Enabled" and "Disabled" (catalog authoring convention,
        // asserted at migration across the toggle population by the now-retired ScriptGenToggleEquivalenceTests).
        var state = catalogSetting.States.FirstOrDefault(s => s.Label == (isEnabled == true ? "Enabled" : "Disabled"));
        if (state == null)
            return;

        foreach (var rt in catalogSetting.Targets.OfType<RegTarget>())
        {
            // Phase 6.8 script-gen tail: when a live build is threaded, drop targets not active on it (the OS-merged
            // "This PC" toggles carry per-target AppliesTo Win10/Win11 ranges - emitting both would write both OS
            // variants). Mirrors ApplyPlanBuilder's per-target gate. When build is null (e.g. a non-build-gated
            // caller / a unit test feeding no build), no target is dropped - the prior emit-all behaviour.
            if (build is { } b && rt.AppliesTo.Count > 0 && !rt.AppliesTo.Any(r => r.Contains(b)))
                continue;

            foreach (var path in rt.Paths)
            {
                // Filter by hive
                bool isHkcuEntry = path.StartsWith("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase);
                if (isHkcuEntry != isHkcu)
                    continue;

                var regPath = EscapePowerShellString(ConvertRegistryPath(path));
                var escapedValueName = EscapePowerShellString(rt.ValueName);

                // Per-subkey enumeration: wrap commands in a ForEach-Object loop
                // so the script enumerates subkeys at install time, not build time
                bool isPerSubkey = rt.PerNetworkInterface || rt.PerMonitor;
                var effectivePath = isPerSubkey ? "$_.PSPath" : $"'{regPath}'";
                var innerIndent = isPerSubkey ? indent + "    " : indent;

                if (isPerSubkey)
                {
                    sb.AppendLine($"{indent}Get-ChildItem -Path '{regPath}' -ErrorAction SilentlyContinue | ForEach-Object {{");
                }

                // The write value for this target from the active state: WritePayload unless the state deletes
                // (Absent/DeleteOnWrite) or carries no entry for this target - both map to null, matching the old
                // GetWriteValue(EnabledValue/DisabledValue) returning null.
                state.Set.TryGetValue(rt.Key, out var sv);
                object? writeValue = (sv != null && !sv.DeleteOnWrite) ? sv.WritePayload : null;

                // Check if we have a raw value from the registry to use instead of definitions
                var key = rt.ValueName ?? "KeyExists";
                object? customValue = null;
                bool hasCustomValue = configItem.CustomStateValues?.TryGetValue(key, out customValue) == true;

                // Pattern 1: Key-Based Settings (CLSID folders, etc.)
                // Detection: ValueName is null or empty - these control registry KEY existence, not values
                if (string.IsNullOrEmpty(rt.ValueName))
                {
                    var keyValue = writeValue;

                    if (keyValue == null)
                    {
                        sb.AppendLine($"{innerIndent}Remove-RegistryKey -Path {effectivePath} -Description '{escapedDescription}'");
                    }
                    else if (keyValue is string keyStrValue && keyStrValue == "")
                    {
                        sb.AppendLine($"{innerIndent}New-RegistryKey -Path {effectivePath} -Description '{escapedDescription}'");
                        sb.AppendLine($"{innerIndent}Set-RegistryValue -Path {effectivePath} -Name '(Default)' -Type 'String' -Value '' -Description '{escapedDescription}'");
                    }
                    else
                    {
                        sb.AppendLine($"{innerIndent}New-RegistryKey -Path {effectivePath} -Description '{escapedDescription}'");
                    }

                    if (isPerSubkey) sb.AppendLine($"{indent}}}");
                    continue;
                }

                if (hasCustomValue)
                {
                    if (customValue == null)
                    {
                        if (isPerSubkey) sb.AppendLine($"{indent}}}");
                        continue;
                    }

                    EmitRegistryValueFromTarget(sb, rt, customValue, escapedDescription!, effectivePath!, escapedValueName!, innerIndent);
                    if (isPerSubkey) sb.AppendLine($"{indent}}}");
                    continue;
                }

                // Fallback for when custom value is not available (should happen rarely if discovery worked)
                var value = writeValue;

                if (value is string strValue && strValue == "")
                {
                    sb.AppendLine($"{innerIndent}Set-RegistryValue -Path {effectivePath} -Name '{escapedValueName}' -Type 'String' -Value '' -Description '{escapedDescription}'");
                    if (isPerSubkey) sb.AppendLine($"{indent}}}");
                    continue;
                }

                // Pattern 3: Null Value Deletion
                if (value == null)
                {
                    sb.AppendLine($"{innerIndent}Remove-RegistryValue -Path {effectivePath} -Name '{escapedValueName}' -Description '{escapedDescription}'");
                    if (isPerSubkey) sb.AppendLine($"{indent}}}");
                    continue;
                }

                // Pattern 4: Regular Value Setting
                EmitRegistryValueFromTarget(sb, rt, value, escapedDescription!, effectivePath!, escapedValueName!, innerIndent);
                if (isPerSubkey) sb.AppendLine($"{indent}}}");
            }
        }
    }

    // Matches .reg section headers like `[HKEY_CURRENT_USER\Software\...]` at the start of a line.
    // Headers are the only syntactic indicator of target hive in a .reg file — comments and REG_SZ
    // values can contain "HKCU" as plain text without affecting import behavior.
    private static readonly Regex s_hkcuHeaderRegex = new(
        @"^\s*\[HKEY_CURRENT_USER\\",
        RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

    private static readonly Regex s_systemHiveHeaderRegex = new(
        @"^\s*\[(HKEY_LOCAL_MACHINE|HKEY_CLASSES_ROOT|HKEY_USERS|HKEY_CURRENT_CONFIG)\\",
        RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

    internal static bool RegContentTargetsHkcu(string content)
        => !string.IsNullOrEmpty(content) && s_hkcuHeaderRegex.IsMatch(content);

    internal static bool RegContentMixesHives(string content)
        => !string.IsNullOrEmpty(content)
           && s_hkcuHeaderRegex.IsMatch(content)
           && s_systemHiveHeaderRegex.IsMatch(content);

    /// <summary>Phase 6.8 F2c: byte-equivalent new-catalog mirror of AppendRegContentCommands. Sources the .reg
    /// content from the active SettingState's RegContentEffects (the "Enabled" state when isEnabled is true, else the
    /// "Disabled" state) instead of the old SettingDefinition.RegContents' EnabledContent/DisabledContent. The converter's
    /// BuildToggleEffects maps each non-empty RegContents[i].EnabledContent to the Enabled state's RegContentEffect
    /// (DisabledContent to the Disabled state's) in order, so the Enabled state's RegContentEffects are exactly the old
    /// method's non-empty EnabledContents in order (likewise Disabled). Each content is hive-routed, mixed-hive-rejected,
    /// and emitted identically to the old block. Proven at migration by the now-retired ScriptGenRegContentEquivalenceTests.</summary>
    public void AppendRegContentCommandsFromCatalog(StringBuilder sb, Winhance.Core.Features.Common.Catalog.Setting catalogSetting, bool? isEnabled, bool isHkcuPass, string indent = "")
    {
        var escapedDescription = EscapePowerShellString(catalogSetting.Display.Description);
        var varName = SanitizeVariableName(catalogSetting.Id);

        // A toggle Setting has exactly two states, Label "Enabled" and "Disabled". Pick the one this pass applies.
        var state = catalogSetting.States.FirstOrDefault(s => s.Label == (isEnabled == true ? "Enabled" : "Disabled"));
        if (state == null)
            return;

        foreach (var regContentEffect in state.Effects.OfType<RegContentEffect>())
        {
            var content = regContentEffect.Content;

            if (string.IsNullOrEmpty(content)) continue;

            // Reject mixed-hive blocks: the emitter routes to a single pass per block, so a block
            // containing both HKCU and HKLM/HKCR/HKU/HKCC headers would silently lose half its
            // content under the hive filter below. Authors must split such content into separate
            // RegContentSetting entries.
            if (RegContentMixesHives(content))
            {
                throw new InvalidOperationException(
                    $"RegContentSetting for '{catalogSetting.Id}' mixes HKEY_CURRENT_USER and system-hive " +
                    $"section headers in a single block. Split it into one RegContentSetting per hive " +
                    $"so each can be routed to the correct autounattend pass.");
            }

            // Determine pass by inspecting .reg section headers only (lines like
            // `[HKEY_CURRENT_USER\...]`). Scanning raw text caught false positives when
            // "HKCU" appeared in a comment or REG_SZ value.
            if (RegContentTargetsHkcu(content) != isHkcuPass)
                continue;

            sb.AppendLine($"{indent}try {{");
            sb.AppendLine($"{indent}    $regContent_{varName} = @'");
            sb.AppendLine(content);
            sb.AppendLine("'@");
            sb.AppendLine($"{indent}    $tempRegFile = Join-Path $env:TEMP \"winhance_{catalogSetting.Id}_$((Get-Date).Ticks).reg\"");
            sb.AppendLine($"{indent}    $regContent_{varName} | Out-File -FilePath $tempRegFile -Encoding Unicode -Force");
            sb.AppendLine($"{indent}    reg import \"$tempRegFile\" 2>&1 | Out-Null");
            sb.AppendLine($"{indent}    if ($LASTEXITCODE -eq 0) {{");
            sb.AppendLine($"{indent}        Write-Log \"{escapedDescription}\" \"SUCCESS\"");
            sb.AppendLine($"{indent}    }} else {{");
            sb.AppendLine($"{indent}        Write-Log \"Failed to import registry content for {escapedDescription}\" \"ERROR\"");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine($"{indent}    Remove-Item $tempRegFile -Force -ErrorAction SilentlyContinue");
            sb.AppendLine($"{indent}}} catch {{");
            sb.AppendLine($"{indent}    Write-Log \"Error processing registry content for {escapedDescription}: $($_.Exception.Message)\" \"ERROR\"");
            sb.AppendLine($"{indent}}}");
            sb.AppendLine();
        }
    }

    /// <summary>Phase 6.8 script-gen tail: byte-equivalent new-catalog mirror of the OLD Action REGISTRY emission
    /// (AppendToggleCommandsFiltered's Pattern-4 plain Set-RegistryValue path, run when an Action is IsSelected).
    /// Action settings carry SETTING-level Effects (no States/Targets); the retired SettingDefinitionConverter.ConvertAction mapped
    /// each old RegistrySetting's single EnabledValue to a plain RegistryWriteEffect and REJECTS any non-plain write
    /// (bit/byte/composite/per-subkey/key-existence) at convert time, so only the plain Set-RegistryValue path is
    /// reachable here - matching the old emitter's Pattern-4 branch for these settings (the per-subkey/key-existence/
    /// binary-byte branches never apply). Each write is hive-filtered by its Path. The caller guards IsSelected; this
    /// method assumes the Action is selected. The Action population is RegistryWriteEffect/ScriptEffect-only (asserted
    /// by the now-retired ScriptGenActionEquivalenceTests), so RegContent/NativePower effects are not emitted here. Proven by
    /// the now-retired ScriptGenActionEquivalenceTests.</summary>
    public void AppendActionRegistryCommandsFromCatalog(StringBuilder sb, Winhance.Core.Features.Common.Catalog.Setting catalogSetting, bool isHkcu, string indent = "")
    {
        var escapedDescription = EscapePowerShellString(catalogSetting.Display.Description);

        foreach (var rw in catalogSetting.Effects.OfType<RegistryWriteEffect>())
        {
            bool isHkcuEntry = rw.Path.StartsWith("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase);
            if (isHkcuEntry != isHkcu)
                continue;

            var regPath = EscapePowerShellString(ConvertRegistryPath(rw.Path));
            var escapedValueName = EscapePowerShellString(rw.ValueName);
            var valueType = ConvertToRegistryType(rw.Kind);
            var formattedValue = FormatValueForPowerShell(rw.Value, rw.Kind);
            sb.AppendLine($"{indent}Set-RegistryValue -Path '{regPath}' -Name '{escapedValueName}' -Type '{valueType}' -Value {formattedValue} -Description '{escapedDescription}'");
        }
    }

    /// <summary>Emits a Selection setting's resolved value writes. Slice 7e-6: takes the PAIRED catalog Setting
    /// (the section's dict now carries catalog Settings, so the internal alias-normalizing SettingCatalog.Find
    /// re-pairing is gone) and is renamed from AppendSelectionCommandsFiltered - it no longer filters/reads defs.
    /// The PowerPlanSelection skip stays (that id is emitted by the Power Settings section). Value resolution:
    /// runtime CustomStateValues win; otherwise a SelectedIndex resolves through the catalog States' Set
    /// (ResolveSelectionValuesFromCatalog - the now-retired ScriptGenSelectionResolveEquivalenceTests; the "any state carries
    /// write-values" gate was the now-retired ScriptGenSelectionGateEquivalenceTests' gate A); a selection with neither logs a
    /// warning and emits nothing. Emission routes through ApplyResolvedValuesFromCatalog
    /// (the now-retired ScriptGenApplyResolvedEquivalenceTests).</summary>
    public void AppendSelectionCommands(StringBuilder sb, Winhance.Core.Features.Common.Catalog.Setting setting, ConfigurationItem configItem, bool isHkcu, string indent = "")
    {
        if (setting.Id == SettingIds.PowerPlanSelection)
            return;

        Dictionary<string, object> valuesToApply;

        if (configItem.CustomStateValues != null && configItem.CustomStateValues.Any())
        {
            valuesToApply = configItem.CustomStateValues;
        }
        else if (configItem.SelectedIndex.HasValue &&
                 setting.States.Any(st => st.Set.Count > 0))
        {
            valuesToApply = ResolveSelectionValuesFromCatalog(setting, configItem.SelectedIndex.Value);
        }
        else
        {
            _logService.Log(LogLevel.Warning, $"Selection setting {setting.Id} has no ValueMappings or CustomStateValues");
            return;
        }

        ApplyResolvedValuesFromCatalog(sb, setting, valuesToApply, isHkcu, indent);
    }

    /// <summary>Phase 6.8 F1: the new-catalog replacement for IComboBoxResolver.ResolveIndexToRawValues. Builds the
    /// selected option's raw write-values dict from the catalog setting's States[index].Set - each Set entry keyed by
    /// the matching Target's registry ValueName (or "KeyExists" when the RegTarget has no value name), or "PowerCfgValue"
    /// for a PowerCfgTarget, valued by StateValue.WritePayload. The caller pairs the setting (SettingCatalog.Find)
    /// and passes the catalog Setting in. Returns empty when the selected STATE carries no write-values (an empty
    /// Set - the old "selected option has no ValueMappings" case, proven equivalent per index by
    /// the now-retired ScriptGenSelectionGateEquivalenceTests gate B) or the index is out of range. Byte-equivalence with the old
    /// IComboBoxResolver.ResolveIndexToRawValues for all paired selections is proven by
    /// the now-retired ScriptGenSelectionResolveEquivalenceTests (103 settings, 0 mismatches).</summary>
    private static Dictionary<string, object> ResolveSelectionValuesFromCatalog(Winhance.Core.Features.Common.Catalog.Setting catalogSetting, int index)
    {
        var result = new Dictionary<string, object>();

        // Faithful to ResolveIndexToRawValues: empty unless the SELECTED state carries write-values. A state's Set
        // is non-empty exactly when its option carried ValueMappings (the converter builds Set from ValueMappings),
        // so the old "options[index].ValueMappings == null" gate is the catalog "States[index].Set is empty" check.
        if (index < 0 || index >= catalogSetting.States.Count
            || catalogSetting.States[index].Set.Count == 0)
            return result;

        foreach (var entry in catalogSetting.States[index].Set)
        {
            var target = catalogSetting.Targets.FirstOrDefault(t => t.Key == entry.Key);
            string? key = target switch
            {
                RegTarget rt => rt.ValueName ?? "KeyExists",
                PowerCfgTarget => "PowerCfgValue",
                _ => null,
            };
            if (key != null)
                result[key] = entry.Value.WritePayload!;
        }

        return result;
    }

    /// <summary>Phase 6.8 F2a: byte-equivalent new-catalog mirror of ApplyResolvedValues, reading the catalog Setting's
    /// Display.Description + Targets (PowerCfgTarget/RegTarget) instead of the old setting's Description +
    /// PowerCfgSettings/RegistrySettings. A mirror RegTarget with N Paths reproduces N old single-KeyPath
    /// RegistrySettings, so each path emits one command in order. Proven at migration by the now-retired ScriptGenApplyResolvedEquivalenceTests.</summary>
    public void ApplyResolvedValuesFromCatalog(StringBuilder sb, Winhance.Core.Features.Common.Catalog.Setting catalogSetting, Dictionary<string, object> valuesToApply, bool isHkcu, string indent)
    {
        var escapedDescription = EscapePowerShellString(catalogSetting.Display.Description);

        foreach (var kvp in valuesToApply)
        {
            var powerCfgTargets = catalogSetting.Targets.OfType<PowerCfgTarget>().ToList();
            if (kvp.Key == "PowerCfgValue" && powerCfgTargets.Any())
            {
                foreach (var powerCfgTarget in powerCfgTargets)
                {
                    var value = Convert.ToInt32(kvp.Value);

                    if (powerCfgTarget.Mode == PowerModeSupport.Separate)
                    {
                        sb.AppendLine($"{indent}powercfg /setacvalueindex SCHEME_CURRENT {powerCfgTarget.SubgroupGuid} {powerCfgTarget.SettingGuid} {value}");
                        sb.AppendLine($"{indent}powercfg /setdcvalueindex SCHEME_CURRENT {powerCfgTarget.SubgroupGuid} {powerCfgTarget.SettingGuid} {value}");
                    }
                    else
                    {
                        sb.AppendLine($"{indent}powercfg /setacvalueindex SCHEME_CURRENT {powerCfgTarget.SubgroupGuid} {powerCfgTarget.SettingGuid} {value}");
                    }
                }
                sb.AppendLine($"{indent}Write-Log 'Applied: {escapedDescription}' 'SUCCESS'");
                continue;
            }

            var matchingRegTargets = catalogSetting.Targets.OfType<RegTarget>()
                .Where(rt => rt.ValueName == kvp.Key || kvp.Key == "KeyExists")
                .ToList();

            foreach (var rt in matchingRegTargets)
            {
                foreach (var keyPath in rt.Paths)
                {
                    bool isHkcuEntry = keyPath.StartsWith("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase);
                    if (isHkcuEntry != isHkcu)
                        continue;

                    var regPath = EscapePowerShellString(ConvertRegistryPath(keyPath));
                    var escapedValueName = EscapePowerShellString(rt.ValueName);

                    bool isPerSubkey = rt.PerNetworkInterface || rt.PerMonitor;
                    var effectivePath = isPerSubkey ? "$_.PSPath" : $"'{regPath}'";
                    var innerIndent = isPerSubkey ? indent + "    " : indent;

                    if (isPerSubkey)
                    {
                        sb.AppendLine($"{indent}Get-ChildItem -Path '{regPath}' -ErrorAction SilentlyContinue | ForEach-Object {{");
                    }

                    if (kvp.Value == null)
                    {
                        if (isPerSubkey) sb.AppendLine($"{indent}}}");
                        continue;
                    }
                    else
                    {
                        EmitRegistryValueFromTarget(sb, rt, kvp.Value, escapedDescription!, effectivePath!, escapedValueName!, innerIndent);
                    }

                    if (isPerSubkey) sb.AppendLine($"{indent}}}");
                }
            }
        }
    }

}

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

internal class RegistryCommandEmitter
{
    private readonly ILogService _logService;

    public RegistryCommandEmitter(ILogService logService)
    {
        _logService = logService;
    }

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

    // One command per path for a mirror target. Write value = the active state's WritePayload, or null when that
    // StateValue deletes or the state has no entry for the target. Registry targets only; the RegContents tail is the call site's.
    public void AppendToggleCommandsFromCatalog(StringBuilder sb, Winhance.Core.Features.Common.Catalog.Setting catalogSetting, ConfigurationItem configItem, bool isHkcu, string indent = "", Winhance.Core.Features.Common.Catalog.WinBuild? build = null)
    {
        var escapedDescription = EscapePowerShellString(catalogSetting.Display.Description);
        var isEnabled = configItem.IsSelected;

        // A toggle Setting has exactly two states, Label "Enabled" and "Disabled" (catalog authoring convention).
        var state = catalogSetting.States.FirstOrDefault(s => s.Label == (isEnabled == true ? "Enabled" : "Disabled"));
        if (state == null)
            return;

        foreach (var rt in catalogSetting.Targets.OfType<RegTarget>())
        {
            // When a live build is threaded, drop targets not active on it (the OS-merged "This PC" toggles
            // carry per-target AppliesTo Win10/Win11 ranges - emitting both would write both OS variants).
            // Mirrors ApplyPlanBuilder's per-target gate. When build is null (e.g. a non-build-gated caller /
            // a unit test feeding no build), no target is dropped.
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
                // (Absent/DeleteOnWrite) or carries no entry for this target - both map to null.
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

    // Each content is hive-routed and mixed-hive-rejected.
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
            // RegContentEffect entries.
            if (RegContentMixesHives(content))
            {
                throw new InvalidOperationException(
                    $"RegContentEffect for '{catalogSetting.Id}' mixes HKEY_CURRENT_USER and system-hive " +
                    $"section headers in a single block. Split it into one RegContentEffect per hive " +
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

    // An Action's effects are plain RegistryWriteEffects only, so only the plain Set-RegistryValue path is reachable.
    // The caller guards IsSelected.
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

    // Runtime CustomStateValues win; otherwise a SelectedIndex resolves through the catalog States' Set; neither ->
    // a warning and nothing emitted. PowerPlanSelection is skipped here (the Power Settings section emits it).
    public void AppendSelectionCommands(StringBuilder sb, Winhance.Core.Features.Common.Catalog.Setting setting, ConfigurationItem configItem, bool isHkcu, string indent = "")
    {
        if (setting.Id == SettingIds.PowerPlanSelection)
            return;

        Dictionary<string, object> valuesToApply;

        if (configItem.CustomStateValues != null && configItem.CustomStateValues.Count > 0)
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

    // Keyed by the matching Target's ValueName ("KeyExists" for a value-less RegTarget) or "PowerCfgValue";
    // empty when the state's Set is empty or the index is out of range.
    private static Dictionary<string, object> ResolveSelectionValuesFromCatalog(Winhance.Core.Features.Common.Catalog.Setting catalogSetting, int index)
    {
        var result = new Dictionary<string, object>();

        // Empty unless the SELECTED state carries write-values (a non-empty Set).
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

    public void ApplyResolvedValuesFromCatalog(StringBuilder sb, Winhance.Core.Features.Common.Catalog.Setting catalogSetting, Dictionary<string, object> valuesToApply, bool isHkcu, string indent)
    {
        var escapedDescription = EscapePowerShellString(catalogSetting.Display.Description);

        foreach (var kvp in valuesToApply)
        {
            var powerCfgTargets = catalogSetting.Targets.OfType<PowerCfgTarget>().ToList();
            if (kvp.Key == "PowerCfgValue" && powerCfgTargets.Count > 0)
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

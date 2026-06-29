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

    /// <summary>
    /// Emits a single registry value set command, handling Binary/BitMask/ModifyByteOnly patterns.
    /// This is the core method that eliminates duplication between toggle and selection code paths.
    /// </summary>
    public void EmitRegistryValue(
        StringBuilder sb,
        RegistrySetting regSetting,
        object value,
        string escapedDescription,
        string pathExpr,
        string escapedValueName,
        string indent)
    {
        var valueType = ConvertToRegistryType(regSetting.ValueType);

        if (regSetting.ValueType == RegistryValueKind.Binary && regSetting.BinaryByteIndex.HasValue)
        {
            if (regSetting.BitMask.HasValue)
            {
                var setBit = Convert.ToBoolean(value);
                sb.AppendLine($"{indent}Set-BinaryBit -Path {pathExpr} -Name '{escapedValueName}' -ByteIndex {regSetting.BinaryByteIndex.Value} -BitMask 0x{regSetting.BitMask.Value:X2} -SetBit ${setBit} -Description '{escapedDescription}'");
            }
            else if (regSetting.ModifyByteOnly)
            {
                var byteValue = value switch
                {
                    byte b => $"0x{b:X2}",
                    int i => $"0x{(byte)i:X2}",
                    _ => "0x00"
                };
                sb.AppendLine($"{indent}Set-BinaryByte -Path {pathExpr} -Name '{escapedValueName}' -ByteIndex {regSetting.BinaryByteIndex.Value} -ByteValue {byteValue} -Description '{escapedDescription}'");
            }
            else
            {
                var formattedValue = FormatValueForPowerShell(value, regSetting.ValueType);
                sb.AppendLine($"{indent}Set-RegistryValue -Path {pathExpr} -Name '{escapedValueName}' -Type '{valueType}' -Value {formattedValue} -Description '{escapedDescription}'");
            }
        }
        else
        {
            var formattedValue = FormatValueForPowerShell(value, regSetting.ValueType);
            sb.AppendLine($"{indent}Set-RegistryValue -Path {pathExpr} -Name '{escapedValueName}' -Type '{valueType}' -Value {formattedValue} -Description '{escapedDescription}'");
        }
    }

    /// <summary>
    /// Emits a single registry value set command using the fallback definition values path.
    /// Handles the special case where BinaryByteIndex requires different byte formatting
    /// when we have a definition value (not a raw registry value).
    /// </summary>
    public void EmitRegistryValueFromDefinition(
        StringBuilder sb,
        RegistrySetting regSetting,
        object value,
        bool? isEnabled,
        string escapedDescription,
        string pathExpr,
        string escapedValueName,
        string indent)
    {
        var valueType = ConvertToRegistryType(regSetting.ValueType);

        if (regSetting.ValueType == RegistryValueKind.Binary && regSetting.BinaryByteIndex.HasValue)
        {
            if (regSetting.BitMask.HasValue)
            {
                var setBit = isEnabled == true;
                sb.AppendLine($"{indent}Set-BinaryBit -Path {pathExpr} -Name '{escapedValueName}' -ByteIndex {regSetting.BinaryByteIndex.Value} -BitMask 0x{regSetting.BitMask.Value:X2} -SetBit ${setBit} -Description '{escapedDescription}'");
            }
            else if (regSetting.ModifyByteOnly)
            {
                var byteValue = value switch
                {
                    byte b => $"0x{b:X2}",
                    int i => $"0x{(byte)i:X2}",
                    _ => "0x00"
                };
                sb.AppendLine($"{indent}Set-BinaryByte -Path {pathExpr} -Name '{escapedValueName}' -ByteIndex {regSetting.BinaryByteIndex.Value} -ByteValue {byteValue} -Description '{escapedDescription}'");
            }
            else
            {
                var formattedValue = FormatValueForPowerShell(value, regSetting.ValueType);
                sb.AppendLine($"{indent}Set-RegistryValue -Path {pathExpr} -Name '{escapedValueName}' -Type '{valueType}' -Value {formattedValue} -Description '{escapedDescription}'");
            }
        }
        else
        {
            var formattedValue = FormatValueForPowerShell(value, regSetting.ValueType);
            sb.AppendLine($"{indent}Set-RegistryValue -Path {pathExpr} -Name '{escapedValueName}' -Type '{valueType}' -Value {formattedValue} -Description '{escapedDescription}'");
        }
    }

    public void AppendToggleCommandsFiltered(StringBuilder sb, SettingDefinition setting, ConfigurationItem configItem, bool isHkcu, string indent = "")
    {
        var escapedDescription = EscapePowerShellString(setting.Description);
        var isEnabled = configItem.IsSelected;

        foreach (var regSetting in setting.RegistrySettings)
        {
            // Filter by hive
            bool isHkcuEntry = regSetting.KeyPath.StartsWith("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase);
            if (isHkcuEntry != isHkcu)
                continue;

            var regPath = EscapePowerShellString(ConvertRegistryPath(regSetting.KeyPath));
            var escapedValueName = EscapePowerShellString(regSetting.ValueName);

            // Per-subkey enumeration: wrap commands in a ForEach-Object loop
            // so the script enumerates subkeys at install time, not build time
            bool isPerSubkey = regSetting.ApplyPerNetworkInterface || regSetting.ApplyPerMonitor;
            var effectivePath = isPerSubkey ? "$_.PSPath" : $"'{regPath}'";
            var innerIndent = isPerSubkey ? indent + "    " : indent;

            if (isPerSubkey)
            {
                sb.AppendLine($"{indent}Get-ChildItem -Path '{regPath}' -ErrorAction SilentlyContinue | ForEach-Object {{");
            }

            // Check if we have a raw value from the registry to use instead of definitions
            var key = regSetting.ValueName ?? "KeyExists";
            object? customValue = null;
            bool hasCustomValue = configItem.CustomStateValues?.TryGetValue(key, out customValue) == true;

            // Pattern 1: Key-Based Settings (CLSID folders, etc.)
            // Detection: ValueName is null or empty - these control registry KEY existence, not values
            if (string.IsNullOrEmpty(regSetting.ValueName))
            {
                var keyValue = GetWriteValue(isEnabled == true ? regSetting.EnabledValue : regSetting.DisabledValue);

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

                EmitRegistryValue(sb, regSetting, customValue, escapedDescription!, effectivePath!, escapedValueName!, innerIndent);
                if (isPerSubkey) sb.AppendLine($"{indent}}}");
                continue;
            }

            // Fallback for when custom value is not available (should happen rarely if discovery worked)
            var value = GetWriteValue(isEnabled == true ? regSetting.EnabledValue : regSetting.DisabledValue);

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
            EmitRegistryValueFromDefinition(sb, regSetting, value, isEnabled, escapedDescription!, effectivePath!, escapedValueName!, innerIndent);
            if (isPerSubkey) sb.AppendLine($"{indent}}}");
        }

        if (setting.RegContents?.Count > 0)
        {
            AppendRegContentCommands(sb, setting, isEnabled, isHkcu, indent);
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

    public void AppendRegContentCommands(StringBuilder sb, SettingDefinition setting, bool? isEnabled, bool isHkcuPass, string indent = "")
    {
        if (setting.RegContents?.Count == 0) return;

        var escapedDescription = EscapePowerShellString(setting.Description);
        var varName = SanitizeVariableName(setting.Id);

        foreach (var regContent in setting.RegContents!)
        {
            var content = isEnabled == true ? regContent.EnabledContent : regContent.DisabledContent;

            if (string.IsNullOrEmpty(content)) continue;

            // Reject mixed-hive blocks: the emitter routes to a single pass per block, so a block
            // containing both HKCU and HKLM/HKCR/HKU/HKCC headers would silently lose half its
            // content under the hive filter below. Authors must split such content into separate
            // RegContentSetting entries.
            if (RegContentMixesHives(content))
            {
                throw new InvalidOperationException(
                    $"RegContentSetting for '{setting.Id}' mixes HKEY_CURRENT_USER and system-hive " +
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
            sb.AppendLine($"{indent}    $tempRegFile = Join-Path $env:TEMP \"winhance_{setting.Id}_$((Get-Date).Ticks).reg\"");
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

    public void AppendSelectionCommandsFiltered(StringBuilder sb, SettingDefinition setting, ConfigurationItem configItem, bool isHkcu, string indent = "")
    {
        if (setting.Id == SettingIds.PowerPlanSelection)
            return;

        Dictionary<string, object> valuesToApply;

        if (configItem.CustomStateValues != null && configItem.CustomStateValues.Any())
        {
            valuesToApply = configItem.CustomStateValues;
        }
        else if (configItem.SelectedIndex.HasValue &&
                 setting.ComboBox?.Options?.Any(o => o.ValueMappings != null) == true)
        {
            valuesToApply = ResolveSelectionValuesFromCatalog(setting, configItem.SelectedIndex.Value);
        }
        else
        {
            _logService.Log(LogLevel.Warning, $"Selection setting {setting.Id} has no ValueMappings or CustomStateValues");
            return;
        }

        ApplyResolvedValues(sb, setting, valuesToApply, isHkcu, indent);
    }

    /// <summary>Phase 6.8 F1: the new-catalog replacement for IComboBoxResolver.ResolveIndexToRawValues. Builds the
    /// selected option's raw write-values dict from the catalog setting's States[index].Set - each Set entry keyed by
    /// the matching Target's registry ValueName (or "KeyExists" when the RegTarget has no value name), or "PowerCfgValue"
    /// for a PowerCfgTarget, valued by StateValue.WritePayload. Returns empty when the selected option carries no
    /// value-mappings (matching the old resolver) or - UNLIKE the old resolver, which had no catalog concept - when the
    /// setting is unpaired or the index is out of range. That unpaired divergence (old would still emit; this emits
    /// nothing) is safe ONLY because every selection setting carrying value-mappings is catalog-paired;
    /// ScriptGenSelectionResolveEquivalenceTests enforces that invariant and proves byte-equivalence with the old
    /// resolver for all paired selections (103 settings, 0 mismatches).</summary>
    private static Dictionary<string, object> ResolveSelectionValuesFromCatalog(SettingDefinition setting, int index)
    {
        var result = new Dictionary<string, object>();

        // Faithful to ResolveIndexToRawValues: empty unless the SELECTED option carries value-mappings.
        if (setting.ComboBox?.Options is not { } options
            || index < 0 || index >= options.Count
            || options[index].ValueMappings == null)
            return result;

        var catalogSetting = SettingCatalog.All.FirstOrDefault(s => s.Id == setting.Id);
        if (catalogSetting == null || index >= catalogSetting.States.Count)
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

    public void ApplyResolvedValues(StringBuilder sb, SettingDefinition setting, Dictionary<string, object> valuesToApply, bool isHkcu, string indent)
    {
        var escapedDescription = EscapePowerShellString(setting.Description);

        foreach (var kvp in valuesToApply)
        {
            if (kvp.Key == "PowerCfgValue" && setting.PowerCfgSettings?.Any() == true)
            {
                foreach (var powerCfgSetting in setting.PowerCfgSettings)
                {
                    var value = Convert.ToInt32(kvp.Value);

                    if (powerCfgSetting.PowerModeSupport == PowerModeSupport.Separate)
                    {
                        sb.AppendLine($"{indent}powercfg /setacvalueindex SCHEME_CURRENT {powerCfgSetting.SubgroupGuid} {powerCfgSetting.SettingGuid} {value}");
                        sb.AppendLine($"{indent}powercfg /setdcvalueindex SCHEME_CURRENT {powerCfgSetting.SubgroupGuid} {powerCfgSetting.SettingGuid} {value}");
                    }
                    else
                    {
                        sb.AppendLine($"{indent}powercfg /setacvalueindex SCHEME_CURRENT {powerCfgSetting.SubgroupGuid} {powerCfgSetting.SettingGuid} {value}");
                    }
                }
                sb.AppendLine($"{indent}Write-Log 'Applied: {escapedDescription}' 'SUCCESS'");
                continue;
            }

            var matchingRegSettings = setting.RegistrySettings
                .Where(r => r.ValueName == kvp.Key || kvp.Key == "KeyExists")
                .ToList();

            foreach (var regSetting in matchingRegSettings)
            {
                bool isHkcuEntry = regSetting.KeyPath.StartsWith("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase);
                if (isHkcuEntry != isHkcu)
                    continue;

                var regPath = EscapePowerShellString(ConvertRegistryPath(regSetting.KeyPath));
                var escapedValueName = EscapePowerShellString(regSetting.ValueName);

                bool isPerSubkey = regSetting.ApplyPerNetworkInterface || regSetting.ApplyPerMonitor;
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
                    EmitRegistryValue(sb, regSetting, kvp.Value, escapedDescription!, effectivePath!, escapedValueName!, innerIndent);
                }

                if (isPerSubkey) sb.AppendLine($"{indent}}}");
            }
        }
    }

}

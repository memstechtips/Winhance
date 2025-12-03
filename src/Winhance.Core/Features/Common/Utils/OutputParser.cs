using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text;
using Winhance.Core.Features.Optimize.Models;

namespace Winhance.Core.Features.Common.Utils
{
    public static class OutputParser
    {
        private static readonly Regex GuidRegex = new(
            @"([a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12})",
            RegexOptions.Compiled);

        private static readonly Regex ParenthesesContentRegex = new(
            @"\((.+?)\)",
            RegexOptions.Compiled);

        public static class PowerCfg
        {
            public static string? ExtractGuid(string output)
                => GuidRegex.Match(output).Success ? GuidRegex.Match(output).Value : null;

            public static string? ExtractNameFromParentheses(string output)
                => ParenthesesContentRegex.Match(output).Success
                    ? ParenthesesContentRegex.Match(output).Groups[1].Value.Trim()
                    : null;

            public static int? ParsePowerSettingValue(string output, string searchPattern)
            {
                if (string.IsNullOrEmpty(output) || string.IsNullOrEmpty(searchPattern))
                    return null;

                try
                {
                    var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith(searchPattern))
                        {
                            var valueStart = trimmed.IndexOf(searchPattern) + searchPattern.Length;
                            var valueString = trimmed.Substring(valueStart).Trim();
                            return ParseIndexValue(valueString);
                        }
                    }
                    return null;
                }
                catch
                {
                    return null;
                }
            }

            public static string? ExtractPowerSchemeGuid(string powercfgOutput)
            {
                try
                {
                    var lines = powercfgOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("Power Scheme GUID:"))
                        {
                            var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 4)
                            {
                                var guid = parts[3];
                                if (System.Guid.TryParse(guid, out _))
                                    return guid;
                            }
                        }
                    }
                    return null;
                }
                catch
                {
                    return null;
                }
            }

            public static Dictionary<string, Dictionary<string, int?>> ParseBulkPowerSettingsOutput(string output)
            {
                var results = new Dictionary<string, Dictionary<string, int?>>();
                if (string.IsNullOrEmpty(output)) return results;

                try
                {
                    var subgroupSections = ParseSubgroupSections(output);

                    foreach (var (subgroupGuid, subgroupContent) in subgroupSections)
                    {
                        var settingValues = ParseSettingsInSubgroup(subgroupContent);
                        foreach (var (settingGuid, values) in settingValues)
                        {
                            var key = settingGuid;
                            results[key] = values;
                        }
                    }
                }
                catch
                {
                    // Return partial results if parsing fails
                }

                return results;
            }

            public static (List<PowerPlan> powerPlans, Dictionary<string, int?> powerSettings) ParseDelimitedPowerOutput(string output)
            {
                var powerPlans = new List<PowerPlan>();
                var powerSettings = new Dictionary<string, int?>();

                try
                {
                    var planStartIndex = output.IndexOf("=== POWER_PLANS_START ===");
                    var planEndIndex = output.IndexOf("=== POWER_PLANS_END ===");

                    if (planStartIndex != -1 && planEndIndex != -1)
                    {
                        var planSection = output.Substring(planStartIndex + "=== POWER_PLANS_START ===".Length,
                                                         planEndIndex - planStartIndex - "=== POWER_PLANS_START ===".Length);
                        powerPlans = ParsePowerPlansFromListOutput(planSection.Trim());
                    }

                    var settingsStartIndex = output.IndexOf("=== POWER_SETTINGS_START ===");
                    var settingsEndIndex = output.IndexOf("=== POWER_SETTINGS_END ===");

                    if (settingsStartIndex != -1 && settingsEndIndex != -1)
                    {
                        var settingsSection = output.Substring(settingsStartIndex + "=== POWER_SETTINGS_START ===".Length,
                                                             settingsEndIndex - settingsStartIndex - "=== POWER_SETTINGS_START ===".Length);
                        var bulkResults = ParseBulkPowerSettingsOutput(settingsSection.Trim());

                        powerSettings = FlattenPowerSettings(bulkResults);
                    }
                }
                catch
                {
                    // Return partial results if parsing fails
                }

                return (powerPlans, powerSettings);
            }

            public static List<PowerPlan> ParsePowerPlansFromListOutput(string planOutput)
            {
                var powerPlans = new List<PowerPlan>();
                if (string.IsNullOrEmpty(planOutput)) return powerPlans;

                try
                {
                    var lines = planOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        if (line.Contains("Power Scheme GUID:"))
                        {
                            var guid = ExtractGuid(line);
                            var name = ExtractNameFromParentheses(line);
                            bool isActive = line.Trim().EndsWith("*");

                            if (!string.IsNullOrEmpty(guid) && !string.IsNullOrEmpty(name))
                            {
                                powerPlans.Add(new PowerPlan
                                {
                                    Guid = guid,
                                    Name = name,
                                    IsActive = isActive
                                });
                            }
                        }
                    }
                }
                catch
                {
                    // Return empty list if parsing fails
                }

                return powerPlans.OrderByDescending(p => p.IsActive).ThenBy(p => p.Name).ToList();
            }

            public static Dictionary<string, int?> FlattenPowerSettings(Dictionary<string, Dictionary<string, int?>> bulkResults)
            {
                var flatResults = new Dictionary<string, int?>();
                foreach (var settingData in bulkResults)
                {
                    var key = settingData.Key;
                    var acDcValues = settingData.Value;
                    var value = acDcValues.TryGetValue("AC", out var acValue) ? acValue :
                               acDcValues.TryGetValue("DC", out var dcValue) ? dcValue : null;
                    flatResults[key] = value;
                }
                return flatResults;
            }

            private static Dictionary<string, string> ParseSubgroupSections(string output)
            {
                var sections = new Dictionary<string, string>();
                var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                string? currentSubgroupGuid = null;
                var currentContent = new StringBuilder();

                foreach (var line in lines)
                {
                    var trimmed = line.Trim();

                    if (trimmed.StartsWith("Subgroup GUID:"))
                    {
                        if (currentSubgroupGuid != null)
                        {
                            sections[currentSubgroupGuid] = currentContent.ToString();
                        }

                        currentSubgroupGuid = ExtractGuid(trimmed);
                        currentContent.Clear();
                    }
                    else if (currentSubgroupGuid != null)
                    {
                        currentContent.AppendLine(trimmed);
                    }
                }

                if (currentSubgroupGuid != null)
                {
                    sections[currentSubgroupGuid] = currentContent.ToString();
                }

                return sections;
            }

            public static Dictionary<string, int?> ParseFilteredPowerSettingsOutput(string output)
            {
                var results = new Dictionary<string, int?>();
                if (string.IsNullOrEmpty(output)) return results;

                try
                {
                    var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    string? currentSettingGuid = null;
                    int? currentACValue = null;

                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();

                        if (trimmed.StartsWith("Power Setting GUID:"))
                        {
                            // Save previous setting if we have complete data
                            if (currentSettingGuid != null && currentACValue.HasValue)
                            {
                                results[currentSettingGuid] = currentACValue;
                            }

                            // Start new setting
                            currentSettingGuid = ExtractGuid(trimmed);
                            currentACValue = null;
                        }
                        else if (trimmed.StartsWith("Current AC Power Setting Index:"))
                        {
                            var colonIndex = trimmed.IndexOf(':');
                            if (colonIndex != -1)
                            {
                                var valueStr = trimmed.Substring(colonIndex + 1).Trim();
                                currentACValue = ParseIndexValue(valueStr);
                            }
                        }
                        // We can ignore DC values for now since AC is typically used
                    }

                    // Don't forget the last setting
                    if (currentSettingGuid != null && currentACValue.HasValue)
                    {
                        results[currentSettingGuid] = currentACValue;
                    }
                }
                catch (Exception)
                {
                    // Return partial results if parsing fails
                }

                return results;
            }

            private static Dictionary<string, Dictionary<string, int?>> ParseSettingsInSubgroup(string subgroupContent)
            {
                var settings = new Dictionary<string, Dictionary<string, int?>>();
                var lines = subgroupContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                string? currentSettingGuid = null;
                var currentValues = new Dictionary<string, int?>();

                foreach (var line in lines)
                {
                    var trimmed = line.Trim();

                    if (trimmed.StartsWith("Power Setting GUID:"))
                    {
                        if (currentSettingGuid != null)
                        {
                            settings[currentSettingGuid] = new Dictionary<string, int?>(currentValues);
                        }

                        currentSettingGuid = ExtractGuid(trimmed);
                        currentValues.Clear();
                    }
                    else if (trimmed.StartsWith("Current AC Power Setting Index:"))
                    {
                        var colonIndex = trimmed.IndexOf(':');
                        if (colonIndex != -1)
                        {
                            var valueStr = trimmed.Substring(colonIndex + 1).Trim();
                            currentValues["AC"] = ParseIndexValue(valueStr);
                        }
                    }
                    else if (trimmed.StartsWith("Current DC Power Setting Index:"))
                    {
                        var colonIndex = trimmed.IndexOf(':');
                        if (colonIndex != -1)
                        {
                            var valueStr = trimmed.Substring(colonIndex + 1).Trim();
                            currentValues["DC"] = ParseIndexValue(valueStr);
                        }
                    }
                }

                if (currentSettingGuid != null)
                {
                    settings[currentSettingGuid] = currentValues;
                }

                return settings;
            }

            public static (int? minValue, int? maxValue) ParsePowerSettingMinMax(string output)
            {
                if (string.IsNullOrEmpty(output)) return (null, null);

                try
                {
                    var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    int? minValue = null;
                    int? maxValue = null;

                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();

                        if (trimmed.StartsWith("Minimum Possible Setting:"))
                        {
                            var valueStart = trimmed.IndexOf(':') + 1;
                            var valueString = trimmed.Substring(valueStart).Trim();
                            minValue = ParseIndexValue(valueString);
                        }
                        else if (trimmed.StartsWith("Maximum Possible Setting:"))
                        {
                            var valueStart = trimmed.IndexOf(':') + 1;
                            var valueString = trimmed.Substring(valueStart).Trim();
                            maxValue = ParseIndexValue(valueString);
                        }
                    }

                    return (minValue, maxValue);
                }
                catch
                {
                    return (null, null);
                }
            }

            private static int? ParseIndexValue(string valueString)
            {
                if (string.IsNullOrEmpty(valueString)) return null;

                var parts = valueString.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                foreach (var part in parts)
                {
                    if (part.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    {
                        if (int.TryParse(part.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out int hexValue))
                            return hexValue;
                    }
                    else if (int.TryParse(part, out int decValue))
                    {
                        return decValue;
                    }
                }

                return null;
            }
        }
    }
}
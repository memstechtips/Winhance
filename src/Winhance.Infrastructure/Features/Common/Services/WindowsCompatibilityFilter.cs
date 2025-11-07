using System;
using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;

namespace Winhance.Infrastructure.Features.Common.Services
{
    public class WindowsCompatibilityFilter : IWindowsCompatibilityFilter
    {
        private readonly IWindowsVersionService _versionService;
        private readonly ILogService _logService;
        private readonly HashSet<string> _loggedCompatibilityMessages = new();

        public WindowsCompatibilityFilter(
            IWindowsVersionService versionService,
            ILogService logService)
        {
            _versionService = versionService ?? throw new ArgumentNullException(nameof(versionService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        public virtual IEnumerable<SettingDefinition> FilterSettingsByWindowsVersion(
            IEnumerable<SettingDefinition> settings)
        {
            return FilterSettingsByWindowsVersion(settings, applyFilter: true);
        }

        public virtual IEnumerable<SettingDefinition> FilterSettingsByWindowsVersion(
            IEnumerable<SettingDefinition> settings,
            bool applyFilter)
        {
            if (!applyFilter)
            {
                return DecorateSettingsWithCompatibilityMessages(settings);
            }

            try
            {
                var isWindows11 = _versionService.IsWindows11();
                var buildNumber = _versionService.GetWindowsBuildNumber();

                _logService.Log(LogLevel.Debug,
                    $"Filtering settings for Windows {(isWindows11 ? "11" : "10")} build {buildNumber}");

                var compatibleSettings = new List<SettingDefinition>();
                var filteredCount = 0;

                foreach (var setting in settings)
                {
                    bool isCompatible = true;
                    string incompatibilityReason = "";

                    // Check Windows version compatibility using polymorphism
                    bool isWindows10Only = false;
                    bool isWindows11Only = false;
                    int? minimumBuild = null;
                    int? maximumBuild = null;
                    List<(int MinBuild, int MaxBuild)>? supportedRanges = null;

                    // Extract version info from SettingDefinition
                    if (setting is SettingDefinition appSetting)
                    {
                        isWindows10Only = appSetting.IsWindows10Only;
                        isWindows11Only = appSetting.IsWindows11Only;
                        minimumBuild = appSetting.MinimumBuildNumber;
                        maximumBuild = appSetting.MaximumBuildNumber;
                        supportedRanges = appSetting.SupportedBuildRanges;
                    }

                    // Check Windows 10 only restriction
                    if (isWindows10Only && isWindows11)
                    {
                        isCompatible = false;
                        incompatibilityReason = "Windows 10 only";
                    }
                    // Check Windows 11 only restriction
                    else if (isWindows11Only && !isWindows11)
                    {
                        isCompatible = false;
                        incompatibilityReason = "Windows 11 only";
                    }
                    // Check build ranges (takes precedence over min/max if specified)
                    else if (supportedRanges?.Count > 0)
                    {
                        bool inSupportedRange = supportedRanges.Any(range =>
                            buildNumber >= range.MinBuild && buildNumber <= range.MaxBuild);

                        if (!inSupportedRange)
                        {
                            isCompatible = false;
                            var rangesStr = string.Join(", ", supportedRanges.Select(r => $"{r.MinBuild}-{r.MaxBuild}"));
                            incompatibilityReason = $"build not in supported ranges: {rangesStr}";
                        }
                    }
                    // Check minimum build number
                    else if (minimumBuild.HasValue && buildNumber < minimumBuild.Value)
                    {
                        isCompatible = false;
                        incompatibilityReason = $"requires build >= {minimumBuild.Value}";
                    }
                    // Check maximum build number
                    else if (maximumBuild.HasValue && buildNumber > maximumBuild.Value)
                    {
                        isCompatible = false;
                        incompatibilityReason = $"requires build <= {maximumBuild.Value}";
                    }

                    if (isCompatible)
                    {
                        compatibleSettings.Add(setting);
                    }
                    else
                    {
                        filteredCount++;
                        _logService.Log(LogLevel.Debug,
                            $"Filtered out setting '{setting.Id}': {incompatibilityReason}");
                    }
                }

                if (filteredCount > 0)
                {
                    _logService.Log(LogLevel.Debug,
                        $"Filtered out {filteredCount} incompatible settings. {compatibleSettings.Count} settings remain.");
                }

                return compatibleSettings;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error,
                    $"Error filtering settings by Windows version: {ex.Message}. Returning all settings.");
                return settings;
            }
        }

        private IEnumerable<SettingDefinition> DecorateSettingsWithCompatibilityMessages(
            IEnumerable<SettingDefinition> settings)
        {
            var isWindows11 = _versionService.IsWindows11();
            var buildNumber = _versionService.GetWindowsBuildNumber();

            foreach (var setting in settings)
            {
                string? compatibilityMessage = null;

                if (setting.IsWindows10Only && isWindows11)
                {
                    compatibilityMessage = "ℹ️ This setting applies to Windows 10 only";
                }
                else if (setting.IsWindows11Only && !isWindows11)
                {
                    compatibilityMessage = "ℹ️ This setting applies to Windows 11 only";
                }
                else if (setting.MinimumBuildNumber.HasValue &&
                         buildNumber < setting.MinimumBuildNumber.Value)
                {
                    compatibilityMessage = $"ℹ️ Requires Windows build {setting.MinimumBuildNumber.Value} or higher";
                }
                else if (setting.MaximumBuildNumber.HasValue &&
                         buildNumber > setting.MaximumBuildNumber.Value)
                {
                    compatibilityMessage = $"ℹ️ Only compatible with Windows build {setting.MaximumBuildNumber.Value} or lower";
                }
                else if (setting.SupportedBuildRanges?.Count > 0)
                {
                    bool inRange = setting.SupportedBuildRanges.Any(range =>
                        buildNumber >= range.MinBuild && buildNumber <= range.MaxBuild);

                    if (!inRange)
                    {
                        var rangeText = string.Join(" or ",
                            setting.SupportedBuildRanges.Select(r => $"{r.MinBuild}-{r.MaxBuild}"));
                        compatibilityMessage = $"ℹ️ Compatible with builds: {rangeText}";
                    }
                }

                if (compatibilityMessage != null)
                {
                    var logKey = $"{setting.Name}:{compatibilityMessage}";
                    if (!_loggedCompatibilityMessages.Contains(logKey))
                    {
                        _logService.Log(LogLevel.Info, $"Adding compatibility message to {setting.Name}: {compatibilityMessage}");
                        _loggedCompatibilityMessages.Add(logKey);
                    }

                    var updatedProperties = new Dictionary<string, object>(setting.CustomProperties)
                    {
                        [Winhance.Core.Features.Common.Constants.CustomPropertyKeys.VersionCompatibilityMessage] = compatibilityMessage
                    };

                    yield return setting with { CustomProperties = updatedProperties };
                }
                else
                {
                    yield return setting;
                }
            }
        }
    }
}

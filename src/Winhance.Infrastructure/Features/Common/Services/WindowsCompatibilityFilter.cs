using System;
using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;

namespace Winhance.Infrastructure.Features.Common.Services
{
    /// <summary>
    /// Implementation for filtering settings based on Windows version compatibility.
    /// </summary>
    public class WindowsCompatibilityFilter : IWindowsCompatibilityFilter
    {
        private readonly ISystemServices _systemServices;
        private readonly ILogService _logService;

        public WindowsCompatibilityFilter(
            ISystemServices systemServices,
            ILogService logService)
        {
            _systemServices = systemServices ?? throw new ArgumentNullException(nameof(systemServices));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        /// <summary>
        /// Filters settings based on Windows version and build number compatibility.
        /// Supports both OptimizationSetting and CustomizationSetting polymorphically.
        /// </summary>
        public virtual IEnumerable<ApplicationSetting> FilterSettingsByWindowsVersion(
            IEnumerable<ApplicationSetting> settings)
        {
            try
            {
                var isWindows11 = _systemServices.IsWindows11();
                var buildNumber = _systemServices.GetWindowsBuildNumber();
                
                _logService.Log(LogLevel.Info, 
                    $"Filtering settings for Windows {(isWindows11 ? "11" : "10")} build {buildNumber}");

                var compatibleSettings = new List<ApplicationSetting>();
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

                    // Extract version info polymorphically
                    switch (setting)
                    {
                        case OptimizationSetting optSetting:
                            isWindows10Only = optSetting.IsWindows10Only;
                            isWindows11Only = optSetting.IsWindows11Only;
                            minimumBuild = optSetting.MinimumBuildNumber;
                            maximumBuild = optSetting.MaximumBuildNumber;
                            supportedRanges = optSetting.SupportedBuildRanges;
                            break;
                        case CustomizationSetting custSetting:
                            isWindows10Only = custSetting.IsWindows10Only;
                            isWindows11Only = custSetting.IsWindows11Only;
                            minimumBuild = custSetting.MinimumBuildNumber;
                            maximumBuild = custSetting.MaximumBuildNumber;
                            supportedRanges = custSetting.SupportedBuildRanges;
                            break;
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
                    _logService.Log(LogLevel.Info, 
                        $"Filtered out {filteredCount} incompatible settings. {compatibleSettings.Count} settings remain.");
                }
                else
                {
                    _logService.Log(LogLevel.Info, 
                        $"All {compatibleSettings.Count} settings are compatible with current Windows version.");
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
    }
}

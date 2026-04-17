using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;

namespace Winhance.Infrastructure.Features.Common.Services;

public class WindowsCompatibilityFilter : IWindowsCompatibilityFilter
{
    private readonly IWindowsVersionService _versionService;
    private readonly ILogService _logService;
    private readonly ConcurrentDictionary<string, byte> _loggedCompatibilityMessages = new();

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
            var buildRevision = _versionService.GetWindowsBuildRevision();
            var isServer = _versionService.IsWindowsServer();

            if (isServer)
            {
                _logService.Log(LogLevel.Info,
                    $"Windows Server detected (build {buildNumber}). Treating as Windows {(isWindows11 ? "11" : "10")} for compatibility filtering.");
            }

            _logService.Log(LogLevel.Debug,
                $"Filtering settings for Windows {(isWindows11 ? "11" : "10")}{(isServer ? " Server" : "")} build {buildNumber}");

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
                int? minimumRevision = null;
                int? maximumBuild = null;
                int? maximumRevision = null;
                IReadOnlyList<(int MinBuild, int MaxBuild)>? supportedRanges = null;

                // Extract version info from SettingDefinition
                if (setting is SettingDefinition appSetting)
                {
                    isWindows10Only = appSetting.IsWindows10Only;
                    isWindows11Only = appSetting.IsWindows11Only;
                    minimumBuild = appSetting.MinimumBuildNumber;
                    minimumRevision = appSetting.MinimumBuildRevision;
                    maximumBuild = appSetting.MaximumBuildNumber;
                    maximumRevision = appSetting.MaximumBuildRevision;
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
                else
                {
                    // Check minimum and maximum build numbers independently so settings
                    // bounded on both sides (e.g. feature present from build N, removed at build M)
                    // correctly filter out builds above the maximum.
                    if (minimumBuild.HasValue)
                    {
                        if (buildNumber < minimumBuild.Value)
                        {
                            isCompatible = false;
                            incompatibilityReason = $"requires build >= {minimumBuild.Value}";
                        }
                        else if (buildNumber == minimumBuild.Value && minimumRevision.HasValue && buildRevision < minimumRevision.Value)
                        {
                            isCompatible = false;
                            incompatibilityReason = $"requires build >= {minimumBuild.Value}.{minimumRevision.Value}";
                        }
                    }

                    if (isCompatible && maximumBuild.HasValue)
                    {
                        if (buildNumber > maximumBuild.Value)
                        {
                            isCompatible = false;
                            incompatibilityReason = $"requires build <= {maximumBuild.Value}";
                        }
                        else if (buildNumber == maximumBuild.Value && maximumRevision.HasValue && buildRevision > maximumRevision.Value)
                        {
                            isCompatible = false;
                            incompatibilityReason = $"requires build <= {maximumBuild.Value}.{maximumRevision.Value}";
                        }
                    }
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
        var buildRevision = _versionService.GetWindowsBuildRevision();

        foreach (var setting in settings)
        {
            string? compatibilityMessage = null;

            if (setting.IsWindows10Only && isWindows11)
            {
                compatibilityMessage = "Compatibility_Windows10Only";
            }
            else if (setting.IsWindows11Only && !isWindows11)
            {
                compatibilityMessage = "Compatibility_Windows11Only";
            }
            else if (setting.MinimumBuildNumber.HasValue &&
                     buildNumber < setting.MinimumBuildNumber.Value)
            {
                compatibilityMessage = $"Compatibility_MinBuild|{setting.MinimumBuildNumber.Value}";
            }
            else if (setting.MinimumBuildNumber.HasValue &&
                     buildNumber == setting.MinimumBuildNumber.Value &&
                     setting.MinimumBuildRevision.HasValue &&
                     buildRevision < setting.MinimumBuildRevision.Value)
            {
                compatibilityMessage = $"Compatibility_MinBuild|{setting.MinimumBuildNumber.Value}.{setting.MinimumBuildRevision.Value}";
            }
            else if (setting.MaximumBuildNumber.HasValue &&
                     buildNumber > setting.MaximumBuildNumber.Value)
            {
                compatibilityMessage = $"Compatibility_MaxBuild|{setting.MaximumBuildNumber.Value}";
            }
            else if (setting.MaximumBuildNumber.HasValue &&
                     buildNumber == setting.MaximumBuildNumber.Value &&
                     setting.MaximumBuildRevision.HasValue &&
                     buildRevision > setting.MaximumBuildRevision.Value)
            {
                compatibilityMessage = $"Compatibility_MaxBuild|{setting.MaximumBuildNumber.Value}.{setting.MaximumBuildRevision.Value}";
            }
            else if (setting.SupportedBuildRanges?.Count > 0)
            {
                bool inRange = setting.SupportedBuildRanges.Any(range =>
                    buildNumber >= range.MinBuild && buildNumber <= range.MaxBuild);

                if (!inRange)
                {
                    var rangeText = string.Join(" or ",
                        setting.SupportedBuildRanges.Select(r => $"{r.MinBuild}-{r.MaxBuild}"));
                    compatibilityMessage = $"Compatibility_BuildRange|{rangeText}";
                }
            }

            if (compatibilityMessage != null)
            {
                var logKey = $"{setting.Name}:{compatibilityMessage}";
                if (_loggedCompatibilityMessages.TryAdd(logKey, 0))
                {
                    _logService.Log(LogLevel.Info, $"Adding compatibility message to {setting.Name}: {compatibilityMessage}");
                }

                yield return setting with { VersionCompatibilityMessage = compatibilityMessage };
            }
            else
            {
                yield return setting;
            }
        }
    }
}

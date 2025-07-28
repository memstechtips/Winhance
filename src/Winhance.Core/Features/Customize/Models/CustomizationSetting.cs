using System.Collections.Generic;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Enums;

namespace Winhance.Core.Features.Customize.Models
{
    /// <summary>
    /// Represents a setting that customizes the system by modifying registry values.
    /// </summary>
    public record CustomizationSetting : ApplicationSetting
    {
        /// <summary>
        /// Gets or sets the customization category for this setting.
        /// </summary>
        public required CustomizationCategory Category { get; init; }

        /// <summary>
        /// Gets or sets the linked settings for this setting.
        /// This allows grouping multiple settings together under a parent setting.
        /// </summary>
        public List<CustomizationSetting> LinkedSettings { get; init; } = new List<CustomizationSetting>();

        /// <summary>
        /// Gets or sets a value indicating whether this setting is only applicable to Windows 11.
        /// </summary>
        public bool IsWindows11Only { get; init; }

        /// <summary>
        /// Gets or sets a value indicating whether this setting is only applicable to Windows 10.
        /// </summary>
        public bool IsWindows10Only { get; init; }

        /// <summary>
        /// Gets or sets the minimum Windows build number required for this setting.
        /// If specified, the setting will only be shown if the current build number is >= this value.
        /// </summary>
        public int? MinimumBuildNumber { get; init; }

        /// <summary>
        /// Gets or sets the maximum Windows build number supported by this setting.
        /// If specified, the setting will only be shown if the current build number is <= this value.
        /// </summary>
        public int? MaximumBuildNumber { get; init; }

        /// <summary>
        /// Gets or sets a list of supported build number ranges for this setting.
        /// Each range is represented as a tuple of (MinBuild, MaxBuild).
        /// If specified, the setting will only be shown if the current build falls within any of these ranges.
        /// This takes precedence over MinimumBuildNumber and MaximumBuildNumber if specified.
        /// </summary>
        public List<(int MinBuild, int MaxBuild)> SupportedBuildRanges { get; init; } = new List<(int, int)>();
    }
}

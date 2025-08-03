using System.Collections.Generic;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Optimize.Models;

namespace Winhance.Core.Features.Common.Models;

/// <summary>
/// Represents a group of optimization settings.
/// </summary>
public record OptimizationGroup
{
    /// <summary>
    /// Gets or sets the name of the optimization group.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the category of the optimization group.
    /// </summary>
    public required OptimizationCategory Category { get; init; }

    /// <summary>
    /// Gets or sets the settings in the optimization group.
    /// </summary>
    public required IReadOnlyList<OptimizationSetting> Settings { get; init; }
}

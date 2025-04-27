using System.Collections.Generic;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.Common.Models;

public class OptimizationConfig
{
    public required IDictionary<OptimizationCategory, IReadOnlyList<RegistrySetting>> RegistrySettings { get; init; }
    public required IReadOnlyList<WindowsPackage> WindowsPackages { get; init; }
    public required IReadOnlyList<LegacyCapability> LegacyCapabilities { get; init; }
    public required IReadOnlyList<WindowsService> Services { get; init; }
}
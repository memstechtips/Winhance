using Microsoft.Win32;
using System.Collections.Generic;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.Optimize.Models;

public static class DefaultOptimizationConfig
{
    public static OptimizationConfig CreateDefaultConfig()
    {
        return new OptimizationConfig
        {
            RegistrySettings = CreateDefaultRegistrySettings(),
            WindowsPackages = CreateDefaultWindowsPackages(),
            LegacyCapabilities = CreateDefaultLegacyCapabilities(),
            Services = CreateDefaultServices()
        };
    }

    private static IDictionary<OptimizationCategory, IReadOnlyList<RegistrySetting>> CreateDefaultRegistrySettings()
    {
        // We'll implement this next - it will convert the PowerShell $SCRIPT:RegConfig
        return new Dictionary<OptimizationCategory, IReadOnlyList<RegistrySetting>>();
    }

    private static IReadOnlyList<WindowsPackage> CreateDefaultWindowsPackages()
    {
        // We'll implement this next - it will convert the PowerShell $WindowsPackages
        return new List<WindowsPackage>();
    }

    private static IReadOnlyList<LegacyCapability> CreateDefaultLegacyCapabilities()
    {
        // We'll implement this next - it will convert the PowerShell $LegacyCapabilities
        return new List<LegacyCapability>();
    }

    private static IReadOnlyList<WindowsService> CreateDefaultServices()
    {
        // We'll implement this next - it will convert the PowerShell services configuration
        return new List<WindowsService>();
    }
}
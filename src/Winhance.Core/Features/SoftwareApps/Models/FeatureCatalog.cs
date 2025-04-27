using System.Collections.Generic;

namespace Winhance.Core.Features.SoftwareApps.Models;

/// <summary>
/// Represents a catalog of Windows optional features that can be enabled or disabled.
/// </summary>
public class FeatureCatalog
{
    /// <summary>
    /// Gets or sets the collection of Windows optional features.
    /// </summary>
    public IReadOnlyList<FeatureInfo> Features { get; init; } = new List<FeatureInfo>();

    /// <summary>
    /// Creates a default feature catalog with predefined Windows optional features.
    /// </summary>
    /// <returns>A new FeatureCatalog instance with default features.</returns>
    public static FeatureCatalog CreateDefault()
    {
        return new FeatureCatalog { Features = CreateDefaultFeatures() };
    }

    private static IReadOnlyList<FeatureInfo> CreateDefaultFeatures()
    {
        return new List<FeatureInfo>
        {
            // Windows Subsystems
            new FeatureInfo
            {
                Name = "Subsystem for Linux",
                Description = "Allows running Linux binary executables natively on Windows",
                PackageName = "Microsoft-Windows-Subsystem-Linux",
                Category = "Development",
                RequiresReboot = true,
                CanBeReenabled = true,
            },
            // Virtualization
            new FeatureInfo
            {
                Name = "Windows Hypervisor Platform",
                Description = "Core virtualization platform without Hyper-V management tools",
                PackageName = "Microsoft-Hyper-V-Hypervisor",
                Category = "Virtualization",
                RequiresReboot = true,
                CanBeReenabled = true,
            },
            // Hyper-V
            new FeatureInfo
            {
                Name = "Hyper-V",
                Description = "Virtualization platform for running multiple operating systems",
                PackageName = "Microsoft-Hyper-V-All",
                Category = "Virtualization",
                RequiresReboot = true,
                CanBeReenabled = true,
            },
            new FeatureInfo
            {
                Name = "Hyper-V Management Tools",
                Description = "Tools for managing Hyper-V virtual machines",
                PackageName = "Microsoft-Hyper-V-Tools-All",
                Category = "Virtualization",
                RequiresReboot = false,
                CanBeReenabled = true,
            },
            // .NET Framework
            new FeatureInfo
            {
                Name = ".NET Framework 3.5",
                Description = "Legacy .NET Framework for older applications",
                PackageName = "NetFx3",
                Category = "Development",
                RequiresReboot = true,
                CanBeReenabled = true,
            },
            // Windows Features
            new FeatureInfo
            {
                Name = "Windows Sandbox",
                Description = "Isolated desktop environment for running applications",
                PackageName = "Containers-DisposableClientVM",
                Category = "Security",
                RequiresReboot = true,
                CanBeReenabled = true,
            },
            new FeatureInfo
            {
                Name = "Recall",
                Description = "Windows 11 feature that records user activity",
                PackageName = "Recall",
                Category = "System",
                RequiresReboot = false,
                CanBeReenabled = true,
            },
        };
    }
}

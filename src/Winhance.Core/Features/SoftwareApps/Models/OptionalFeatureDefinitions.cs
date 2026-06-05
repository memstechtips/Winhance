using System.Collections.Generic;
using Winhance.Core.Features.Common.Constants;

namespace Winhance.Core.Features.SoftwareApps.Models;

public static class OptionalFeatureDefinitions
{
    public static ItemGroup GetWindowsOptionalFeatures()
    {
        return new ItemGroup
        {
            Name = "Windows Optional Features",
            FeatureId = FeatureIds.WindowsOptionalFeatures,
            Items = new List<ItemDefinition>
            {
                new ItemDefinition
                {
                    Id = "feature-wsl",
                    Name = "Subsystem for Linux",
                    Description = "Allows running Linux binary executables natively on Windows",
                    GroupName = "Development",
                    OptionalFeatureName = "Microsoft-Windows-Subsystem-Linux",
                    // No system binary carries the (Linux-branded) logo, so this is URL-only.
                    RequiresReboot = true,
                    CanBeReinstalled = true
                },
                new ItemDefinition
                {
                    Id = "feature-hyperv-platform",
                    Name = "Windows Hypervisor Platform",
                    Description = "Core virtualization platform without Hyper-V management tools",
                    GroupName = "Virtualization",
                    OptionalFeatureName = "Microsoft-Hyper-V-Hypervisor",
                    RequiresReboot = true,
                    CanBeReinstalled = true
                },
                new ItemDefinition
                {
                    Id = "feature-hyperv",
                    Name = "Hyper-V",
                    Description = "Virtualization platform for running multiple operating systems",
                    GroupName = "Virtualization",
                    OptionalFeatureName = "Microsoft-Hyper-V-All",
                    // Installed -> vmms.exe (carries the Hyper-V product icon).
                    RequiresReboot = true,
                    CanBeReinstalled = true
                },
                new ItemDefinition
                {
                    Id = "feature-hyperv-tools",
                    Name = "Hyper-V Management Tools",
                    Description = "Tools for managing Hyper-V virtual machines",
                    GroupName = "Virtualization",
                    OptionalFeatureName = "Microsoft-Hyper-V-Tools-All",
                    RequiresReboot = false,
                    CanBeReinstalled = true
                },
                new ItemDefinition
                {
                    Id = "feature-dotnet35",
                    Name = ".NET Framework 3.5",
                    Description = "Legacy .NET Framework for older applications",
                    GroupName = "Development",
                    OptionalFeatureName = "NetFx3",
                    RequiresReboot = true,
                    CanBeReinstalled = true
                },
                new ItemDefinition
                {
                    Id = "feature-windows-sandbox",
                    Name = "Windows Sandbox",
                    Description = "Throwaway isolated desktop for running suspicious or untrusted apps",
                    GroupName = "Security",
                    OptionalFeatureName = "Containers-DisposableClientVM",
                    // Installed -> WindowsSandbox.exe.
                    RequiresReboot = true,
                    CanBeReinstalled = true
                },
                new ItemDefinition
                {
                    Id = "feature-recall",
                    Name = "Recall",
                    Description = "Windows 11 AI feature that takes periodic screenshots of activity to make them searchable",
                    GroupName = "System",
                    OptionalFeatureName = "Recall",
                    // Microsoft's own host serving Microsoft's own asset (tested OK).
                    RequiresReboot = false,
                    CanBeReinstalled = true
                }
            }
        };
    }
}

using System.Collections.Generic;
using Winhance.Core.Features.Common.Constants;

namespace Winhance.Core.Features.SoftwareApps.Models
{
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
                        Category = "Development",
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
                        Category = "Virtualization",
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
                        Category = "Virtualization",
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
                        Category = "Virtualization",
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
                        Category = "Development",
                        RequiresReboot = true,
                        CanBeReinstalled = true
                    },
                    new ItemDefinition
                    {
                        Id = "feature-windows-sandbox",
                        Name = "Windows Sandbox",
                        Description = "Isolated desktop environment for running applications",
                        GroupName = "Security",
                        OptionalFeatureName = "Containers-DisposableClientVM",
                        Category = "Security",
                        RequiresReboot = true,
                        CanBeReinstalled = true
                    },
                    new ItemDefinition
                    {
                        Id = "feature-recall",
                        Name = "Recall",
                        Description = "Windows 11 feature that records user activity",
                        GroupName = "System",
                        OptionalFeatureName = "Recall",
                        Category = "System",
                        RequiresReboot = false,
                        CanBeReinstalled = true
                    }
                }
            };
        }
    }
}
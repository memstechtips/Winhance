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
                        Description = "Isolated desktop environment for running applications",
                        GroupName = "Security",
                        OptionalFeatureName = "Containers-DisposableClientVM",
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
                        RequiresReboot = false,
                        CanBeReinstalled = true
                    }
                }
            };
        }
    }
}
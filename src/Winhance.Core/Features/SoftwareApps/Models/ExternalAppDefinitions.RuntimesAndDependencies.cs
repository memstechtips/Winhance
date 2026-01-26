using System.Collections.Generic;
using Winhance.Core.Features.Common.Constants;

namespace Winhance.Core.Features.SoftwareApps.Models
{
    public static partial class ExternalAppDefinitions
    {
        public static class RuntimesAndDependencies
        {
            public static ItemGroup GetRuntimesAndDependencies()
            {
                return new ItemGroup
                {
                    Name = "Runtimes & Dependencies",
                    FeatureId = FeatureIds.ExternalApps,
                    Items = new List<ItemDefinition>
                    {
                        new ItemDefinition
                        {
                            Id = "external-app-dotnet-runtime-3-1",
                            Name = "Microsoft .NET Runtime 3.1",
                            Description = ".NET Runtime 3.1 for running applications",
                            GroupName = "Runtimes & Dependencies",
                            WinGetPackageId = ["Microsoft.DotNet.Runtime.3_1"],
                            WebsiteUrl = "https://dotnet.microsoft.com/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-dotnet-runtime-5",
                            Name = "Microsoft .NET Runtime 5.0",
                            Description = ".NET Runtime 5.0 for running applications",
                            GroupName = "Runtimes & Dependencies",
                            WinGetPackageId = ["Microsoft.DotNet.Runtime.5"],
                            WebsiteUrl = "https://dotnet.microsoft.com/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-dotnet-runtime-6",
                            Name = "Microsoft .NET Runtime 6.0",
                            Description = ".NET Runtime 6.0 LTS for running applications",
                            GroupName = "Runtimes & Dependencies",
                            WinGetPackageId = ["Microsoft.DotNet.Runtime.6"],
                            WebsiteUrl = "https://dotnet.microsoft.com/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-dotnet-runtime-7",
                            Name = "Microsoft .NET Runtime 7.0",
                            Description = ".NET Runtime 7.0 for running applications",
                            GroupName = "Runtimes & Dependencies",
                            WinGetPackageId = ["Microsoft.DotNet.Runtime.7"],
                            WebsiteUrl = "https://dotnet.microsoft.com/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-dotnet-runtime-8",
                            Name = "Microsoft .NET Runtime 8.0",
                            Description = ".NET Runtime 8.0 LTS for running applications",
                            GroupName = "Runtimes & Dependencies",
                            WinGetPackageId = ["Microsoft.DotNet.Runtime.8"],
                            WebsiteUrl = "https://dotnet.microsoft.com/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-dotnet-framework",
                            Name = ".NET Framework 4.8.1",
                            Description = ".NET Framework Developer Pack",
                            GroupName = "Runtimes & Dependencies",
                            WinGetPackageId = ["Microsoft.DotNet.Framework.DeveloperPack_4"],
                            WebsiteUrl = "https://dotnet.microsoft.com/download/dotnet-framework"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-directx",
                            Name = "DirectX End-User Runtime",
                            Description = "DirectX runtime components for running games and multimedia applications",
                            GroupName = "Runtimes & Dependencies",
                            WinGetPackageId = ["Microsoft.DirectX"],
                            WebsiteUrl = "https://www.microsoft.com/en-us/download/details.aspx?id=35"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-java-jre",
                            Name = "Java Runtime Environment",
                            Description = "Java runtime environment for running Java applications",
                            GroupName = "Runtimes & Dependencies",
                            WinGetPackageId = ["Oracle.JavaRuntimeEnvironment"],
                            WebsiteUrl = "https://www.oracle.com/java/technologies/downloads/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-vcredist-2005-x86",
                            Name = "Visual C++ 2005 (x86)",
                            Description = "Visual C++ 2005 runtime components",
                            GroupName = "Runtimes & Dependencies",
                            WinGetPackageId = ["Microsoft.VCRedist.2005.x86"],
                            WebsiteUrl = "https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-vcredist-2005-x64",
                            Name = "Visual C++ 2005 (x64)",
                            Description = "Visual C++ 2005 runtime components",
                            GroupName = "Runtimes & Dependencies",
                            WinGetPackageId = ["Microsoft.VCRedist.2005.x64"],
                            WebsiteUrl = "https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-vcredist-2008-x86",
                            Name = "Visual C++ 2008 (x86)",
                            Description = "Visual C++ 2008 runtime components",
                            GroupName = "Runtimes & Dependencies",
                            WinGetPackageId = ["Microsoft.VCRedist.2008.x86"],
                            WebsiteUrl = "https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-vcredist-2008-x64",
                            Name = "Visual C++ 2008 (x64)",
                            Description = "Visual C++ 2008 runtime components",
                            GroupName = "Runtimes & Dependencies",
                            WinGetPackageId = ["Microsoft.VCRedist.2008.x64"],
                            WebsiteUrl = "https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-vcredist-2010-x86",
                            Name = "Visual C++ 2010 (x86)",
                            Description = "Visual C++ 2010 runtime components",
                            GroupName = "Runtimes & Dependencies",
                            WinGetPackageId = ["Microsoft.VCRedist.2010.x86"],
                            WebsiteUrl = "https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-vcredist-2010-x64",
                            Name = "Visual C++ 2010 (x64)",
                            Description = "Visual C++ 2010 runtime components",
                            GroupName = "Runtimes & Dependencies",
                            WinGetPackageId = ["Microsoft.VCRedist.2010.x64"],
                            WebsiteUrl = "https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-vcredist-2012-x86",
                            Name = "Visual C++ 2012 (x86)",
                            Description = "Visual C++ 2012 runtime components",
                            GroupName = "Runtimes & Dependencies",
                            WinGetPackageId = ["Microsoft.VCRedist.2012.x86"],
                            WebsiteUrl = "https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-vcredist-2012-x64",
                            Name = "Visual C++ 2012 (x64)",
                            Description = "Visual C++ 2012 runtime components",
                            GroupName = "Runtimes & Dependencies",
                            WinGetPackageId = ["Microsoft.VCRedist.2012.x64"],
                            WebsiteUrl = "https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-vcredist-2013-x86",
                            Name = "Visual C++ 2013 (x86)",
                            Description = "Visual C++ 2013 runtime components",
                            GroupName = "Runtimes & Dependencies",
                            WinGetPackageId = ["Microsoft.VCRedist.2013.x86"],
                            WebsiteUrl = "https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-vcredist-2013-x64",
                            Name = "Visual C++ 2013 (x64)",
                            Description = "Visual C++ 2013 runtime components",
                            GroupName = "Runtimes & Dependencies",
                            WinGetPackageId = ["Microsoft.VCRedist.2013.x64"],
                            WebsiteUrl = "https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-vcredist-2015-2022-x86",
                            Name = "Visual C++ 2015-2022 (x86)",
                            Description = "Visual C++ 2015-2022 runtime components",
                            GroupName = "Runtimes & Dependencies",
                            WinGetPackageId = ["Microsoft.VCRedist.2015+.x86"],
                            WebsiteUrl = "https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-vcredist-2015-2022-x64",
                            Name = "Visual C++ 2015-2022 (x64)",
                            Description = "Visual C++ 2015-2022 runtime components",
                            GroupName = "Runtimes & Dependencies",
                            WinGetPackageId = ["Microsoft.VCRedist.2015+.x64"],
                            WebsiteUrl = "https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-vcredist-2022-arm64",
                            Name = "Visual C++ 2022 (ARM64)",
                            Description = "Visual C++ 2022 runtime components for ARM64",
                            GroupName = "Runtimes & Dependencies",
                            WinGetPackageId = ["Microsoft.VCRedist.2015+.arm64"],
                            WebsiteUrl = "https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist"
                        }
                    }
                };
            }
        }
    }
}

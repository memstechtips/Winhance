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
                            ChocoPackageId = "dotnetcore-3.1-runtime",
                            WebsiteUrl = "https://dotnet.microsoft.com/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-dotnet-runtime-5",
                            Name = "Microsoft .NET Runtime 5.0",
                            Description = ".NET Runtime 5.0 for running applications",
                            GroupName = "Runtimes & Dependencies",
                            WinGetPackageId = ["Microsoft.DotNet.Runtime.5"],
                            ChocoPackageId = "dotnet-5.0-runtime",
                            WebsiteUrl = "https://dotnet.microsoft.com/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-dotnet-runtime-6",
                            Name = "Microsoft .NET Runtime 6.0",
                            Description = ".NET Runtime 6.0 LTS for running applications",
                            GroupName = "Runtimes & Dependencies",
                            WinGetPackageId = ["Microsoft.DotNet.Runtime.6"],
                            ChocoPackageId = "dotnet-6.0-runtime",
                            WebsiteUrl = "https://dotnet.microsoft.com/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-dotnet-runtime-7",
                            Name = "Microsoft .NET Runtime 7.0",
                            Description = ".NET Runtime 7.0 for running applications",
                            GroupName = "Runtimes & Dependencies",
                            WinGetPackageId = ["Microsoft.DotNet.Runtime.7"],
                            ChocoPackageId = "dotnet-7.0-runtime",
                            WebsiteUrl = "https://dotnet.microsoft.com/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-dotnet-runtime-8",
                            Name = "Microsoft .NET Runtime 8.0",
                            Description = ".NET Runtime 8.0 LTS for running applications",
                            GroupName = "Runtimes & Dependencies",
                            WinGetPackageId = ["Microsoft.DotNet.Runtime.8"],
                            ChocoPackageId = "dotnet-8.0-runtime",
                            WebsiteUrl = "https://dotnet.microsoft.com/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-dotnet-framework",
                            Name = ".NET Framework 4.8.1",
                            Description = ".NET Framework Developer Pack",
                            GroupName = "Runtimes & Dependencies",
                            WinGetPackageId = ["Microsoft.DotNet.Framework.DeveloperPack_4"],
                            ChocoPackageId = "netfx-4.8.1-devpack",
                            WebsiteUrl = "https://dotnet.microsoft.com/download/dotnet-framework"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-directx",
                            Name = "DirectX End-User Runtime",
                            Description = "DirectX runtime components for running games and multimedia applications",
                            GroupName = "Runtimes & Dependencies",
                            WinGetPackageId = ["Microsoft.DirectX"],
                            ChocoPackageId = "directx",
                            WebsiteUrl = "https://www.microsoft.com/en-us/download/details.aspx?id=35"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-java-jre",
                            Name = "Java Runtime Environment",
                            Description = "Java runtime environment for running Java applications",
                            GroupName = "Runtimes & Dependencies",
                            WinGetPackageId = ["Oracle.JavaRuntimeEnvironment"],
                            ChocoPackageId = "jre8",
                            WebsiteUrl = "https://www.oracle.com/java/technologies/downloads/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-vcredist-2005-x86",
                            Name = "Visual C++ 2005 (x86)",
                            Description = "Visual C++ 2005 runtime components",
                            GroupName = "Runtimes & Dependencies",
                            WinGetPackageId = ["Microsoft.VCRedist.2005.x86"],
                            ChocoPackageId = "vcredist2005",
                            WebsiteUrl = "https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-vcredist-2005-x64",
                            Name = "Visual C++ 2005 (x64)",
                            Description = "Visual C++ 2005 runtime components",
                            GroupName = "Runtimes & Dependencies",
                            WinGetPackageId = ["Microsoft.VCRedist.2005.x64"],
                            ChocoPackageId = "vcredist2005",
                            WebsiteUrl = "https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-vcredist-2008-x86",
                            Name = "Visual C++ 2008 (x86)",
                            Description = "Visual C++ 2008 runtime components",
                            GroupName = "Runtimes & Dependencies",
                            WinGetPackageId = ["Microsoft.VCRedist.2008.x86"],
                            ChocoPackageId = "vcredist2008",
                            WebsiteUrl = "https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-vcredist-2008-x64",
                            Name = "Visual C++ 2008 (x64)",
                            Description = "Visual C++ 2008 runtime components",
                            GroupName = "Runtimes & Dependencies",
                            WinGetPackageId = ["Microsoft.VCRedist.2008.x64"],
                            ChocoPackageId = "vcredist2008",
                            WebsiteUrl = "https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-vcredist-2010-x86",
                            Name = "Visual C++ 2010 (x86)",
                            Description = "Visual C++ 2010 runtime components",
                            GroupName = "Runtimes & Dependencies",
                            WinGetPackageId = ["Microsoft.VCRedist.2010.x86"],
                            ChocoPackageId = "vcredist2010",
                            WebsiteUrl = "https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-vcredist-2010-x64",
                            Name = "Visual C++ 2010 (x64)",
                            Description = "Visual C++ 2010 runtime components",
                            GroupName = "Runtimes & Dependencies",
                            WinGetPackageId = ["Microsoft.VCRedist.2010.x64"],
                            ChocoPackageId = "vcredist2010",
                            WebsiteUrl = "https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-vcredist-2012-x86",
                            Name = "Visual C++ 2012 (x86)",
                            Description = "Visual C++ 2012 runtime components",
                            GroupName = "Runtimes & Dependencies",
                            WinGetPackageId = ["Microsoft.VCRedist.2012.x86"],
                            ChocoPackageId = "vcredist2012",
                            WebsiteUrl = "https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-vcredist-2012-x64",
                            Name = "Visual C++ 2012 (x64)",
                            Description = "Visual C++ 2012 runtime components",
                            GroupName = "Runtimes & Dependencies",
                            WinGetPackageId = ["Microsoft.VCRedist.2012.x64"],
                            ChocoPackageId = "vcredist2012",
                            WebsiteUrl = "https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-vcredist-2013-x86",
                            Name = "Visual C++ 2013 (x86)",
                            Description = "Visual C++ 2013 runtime components",
                            GroupName = "Runtimes & Dependencies",
                            WinGetPackageId = ["Microsoft.VCRedist.2013.x86"],
                            ChocoPackageId = "vcredist2013",
                            WebsiteUrl = "https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-vcredist-2013-x64",
                            Name = "Visual C++ 2013 (x64)",
                            Description = "Visual C++ 2013 runtime components",
                            GroupName = "Runtimes & Dependencies",
                            WinGetPackageId = ["Microsoft.VCRedist.2013.x64"],
                            ChocoPackageId = "vcredist2013",
                            WebsiteUrl = "https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-vcredist-2015-2022-x86",
                            Name = "Visual C++ 2015-2022 (x86)",
                            Description = "Visual C++ 2015-2022 runtime components",
                            GroupName = "Runtimes & Dependencies",
                            WinGetPackageId = ["Microsoft.VCRedist.2015+.x86"],
                            ChocoPackageId = "vcredist140",
                            WebsiteUrl = "https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-vcredist-2015-2022-x64",
                            Name = "Visual C++ 2015-2022 (x64)",
                            Description = "Visual C++ 2015-2022 runtime components",
                            GroupName = "Runtimes & Dependencies",
                            WinGetPackageId = ["Microsoft.VCRedist.2015+.x64"],
                            ChocoPackageId = "vcredist140",
                            WebsiteUrl = "https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-vcredist-2022-arm64",
                            Name = "Visual C++ 2022 (ARM64)",
                            Description = "Visual C++ 2022 runtime components for ARM64",
                            GroupName = "Runtimes & Dependencies",
                            WinGetPackageId = ["Microsoft.VCRedist.2015+.arm64"],
                            ChocoPackageId = "vcredist140",
                            WebsiteUrl = "https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist"
                        }
                    }
                };
            }
        }
    }
}

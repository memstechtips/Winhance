using System;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Native;

namespace Winhance.Infrastructure.Features.Common.Services;

public class SystemInfoProvider : ISystemInfoProvider
{
    private readonly IInteractiveUserService _interactiveUserService;

    public SystemInfoProvider(IInteractiveUserService interactiveUserService)
    {
        _interactiveUserService = interactiveUserService
            ?? throw new ArgumentNullException(nameof(interactiveUserService));
    }

    public SystemInfo Collect()
    {
        // Query Win32_ComputerSystem once for DeviceType, RAM, and DomainJoined
        string deviceType = "Unknown";
        string ram = "Unknown";
        string domainJoined = "Unknown";
        CollectComputerSystemInfo(ref deviceType, ref ram, ref domainJoined);

        return new SystemInfo
        {
            AppVersion = CollectAppVersion(),
            OperatingSystem = CollectOperatingSystem(),
            Architecture = CollectArchitecture(),
            DeviceType = deviceType,
            Cpu = CollectCpu(),
            Ram = ram,
            Gpu = CollectGpu(),
            DotNetRuntime = CollectDotNetRuntime(),
            Elevation = CollectElevation(),
            FirmwareType = CollectFirmwareType(),
            SecureBoot = CollectSecureBoot(),
            Tpm = CollectTpm(),
            DomainJoined = domainJoined
        };
    }

    private static string CollectAppVersion()
    {
        try
        {
            var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            string? location = assembly.Location;

            if (string.IsNullOrEmpty(location))
                return "Unknown";

            var versionInfo = FileVersionInfo.GetVersionInfo(location);
            string version = versionInfo.ProductVersion ?? versionInfo.FileVersion ?? "Unknown";

            // Trim build metadata
            int plusIndex = version.IndexOf('+');
            if (plusIndex > 0)
                version = version.Substring(0, plusIndex);

            return version;
        }
        catch
        {
            return "Unknown";
        }
    }

    private static string CollectOperatingSystem()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            if (key == null)
                return "Unknown";

            string productName = key.GetValue("ProductName") as string ?? "Windows";
            string displayVersion = key.GetValue("DisplayVersion") as string ?? "";
            string buildNumber = key.GetValue("CurrentBuildNumber") as string ?? "";
            var ubr = key.GetValue("UBR");
            string ubrStr = ubr != null ? $".{ubr}" : "";

            // Registry ProductName still says "Windows 10" on Win11 machines.
            // Build 22000+ is Windows 11.
            if (int.TryParse(buildNumber, out int build) && build >= 22000
                && productName.Contains("Windows 10"))
            {
                productName = productName.Replace("Windows 10", "Windows 11");
            }

            string result = productName;
            if (!string.IsNullOrEmpty(displayVersion))
                result += $" {displayVersion}";
            if (!string.IsNullOrEmpty(buildNumber))
                result += $" (Build {buildNumber}{ubrStr})";

            return result;
        }
        catch
        {
            return "Unknown";
        }
    }

    private static string CollectArchitecture()
    {
        try
        {
            return RuntimeInformation.OSArchitecture switch
            {
                Architecture.X64 => "x64",
                Architecture.X86 => "x86",
                Architecture.Arm64 => "arm64",
                Architecture.Arm => "arm",
                _ => RuntimeInformation.OSArchitecture.ToString()
            };
        }
        catch
        {
            return "Unknown";
        }
    }

    private static void CollectComputerSystemInfo(
        ref string deviceType, ref string ram, ref string domainJoined)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT PCSystemType, Model, Manufacturer, TotalPhysicalMemory, PartOfDomain, Domain FROM Win32_ComputerSystem");
            using var collection = searcher.Get();

            foreach (ManagementObject obj in collection)
            {
                using (obj)
                {
                    // DeviceType
                    try
                    {
                        if (obj["PCSystemType"] != null)
                        {
                            int pcSystemType = Convert.ToInt32(obj["PCSystemType"]);
                            deviceType = pcSystemType switch
                            {
                                1 => "Desktop",
                                2 => "Laptop",
                                3 => "Workstation",
                                4 => "Enterprise Server",
                                5 => "SOHO Server",
                                7 => "Performance Server",
                                8 => "Slate",
                                _ => $"Other ({pcSystemType})"
                            };
                        }

                        // Detect virtual machines from Model/Manufacturer
                        string model = (obj["Model"] as string ?? "").Trim();
                        string manufacturer = (obj["Manufacturer"] as string ?? "").Trim();
                        if (IsVirtualMachine(model, manufacturer))
                            deviceType += " (Virtual Machine)";
                    }
                    catch { /* field failure */ }

                    // RAM
                    try
                    {
                        if (obj["TotalPhysicalMemory"] != null)
                        {
                            long totalBytes = Convert.ToInt64(obj["TotalPhysicalMemory"]);
                            int totalGb = (int)Math.Round(totalBytes / (1024.0 * 1024 * 1024));
                            ram = $"{totalGb} GB";
                        }
                    }
                    catch { /* field failure */ }

                    // Domain
                    try
                    {
                        if (obj["PartOfDomain"] != null)
                        {
                            bool partOfDomain = Convert.ToBoolean(obj["PartOfDomain"]);
                            if (partOfDomain)
                            {
                                string domain = obj["Domain"] as string ?? "";
                                domainJoined = $"Yes ({domain})";
                            }
                            else
                            {
                                domainJoined = "No";
                            }
                        }
                    }
                    catch { /* field failure */ }
                }
            }

            // Fallback to ChassisTypes if PCSystemType wasn't conclusive
            if (deviceType == "Unknown" || deviceType.StartsWith("Other"))
            {
                try
                {
                    string chassisResult = CollectDeviceTypeFromChassis();
                    if (chassisResult != "Unknown")
                        deviceType = chassisResult;
                }
                catch { /* fallback failure */ }
            }
        }
        catch
        {
            // WMI query failed entirely — all three stay "Unknown"
        }
    }

    private static readonly int[] LaptopChassisTypes = { 3, 8, 9, 10, 11, 14, 30, 31, 32 };

    private static readonly string[] VmIndicators =
    {
        "virtual", "vmware", "virtualbox", "vbox", "hyper-v",
        "qemu", "kvm", "xen", "parallels", "bhyve"
    };

    private static bool IsVirtualMachine(string model, string manufacturer)
    {
        string combined = $"{model} {manufacturer}".ToLowerInvariant();
        foreach (var indicator in VmIndicators)
        {
            if (combined.Contains(indicator))
                return true;
        }
        return false;
    }

    private static string CollectDeviceTypeFromChassis()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT ChassisTypes FROM Win32_SystemEnclosure");
            using var collection = searcher.Get();

            foreach (ManagementObject enclosure in collection)
            {
                using (enclosure)
                {
                    if (enclosure["ChassisTypes"] is Array chassisTypes && chassisTypes.Length > 0)
                    {
                        foreach (var chassisType in chassisTypes)
                        {
                            int type = Convert.ToInt32(chassisType);
                            if (LaptopChassisTypes.Contains(type))
                                return "Laptop";
                        }
                        return "Desktop";
                    }
                }
            }
        }
        catch { /* fallback failure */ }

        return "Unknown";
    }

    private static string CollectCpu()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, NumberOfLogicalProcessors FROM Win32_Processor");
            using var collection = searcher.Get();

            foreach (ManagementObject obj in collection)
            {
                using (obj)
                {
                    string name = (obj["Name"] as string ?? "Unknown").Trim();
                    int cores = Convert.ToInt32(obj["NumberOfLogicalProcessors"] ?? 0);
                    return cores > 0 ? $"{name} ({cores} cores)" : name;
                }
            }
        }
        catch { /* query failure */ }

        return "Unknown";
    }

    private static string CollectGpu()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, AdapterDACType FROM Win32_VideoController");
            using var collection = searcher.Get();

            var gpus = new System.Collections.Generic.List<string>();

            foreach (ManagementObject obj in collection)
            {
                using (obj)
                {
                    string name = (obj["Name"] as string ?? "").Trim();
                    if (string.IsNullOrEmpty(name))
                        continue;

                    string dacType = (obj["AdapterDACType"] as string ?? "").Trim();
                    string gpuType = ClassifyGpu(name, dacType);
                    gpus.Add($"{name} ({gpuType})");
                }
            }

            return gpus.Count > 0 ? string.Join("; ", gpus) : "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }

    private static string ClassifyGpu(string name, string dacType)
    {
        string nameLower = name.ToLowerInvariant();

        // "Internal" DAC type is a strong signal for integrated
        if (dacType.Equals("Internal", StringComparison.OrdinalIgnoreCase))
            return "Integrated";

        // Name-based heuristics for integrated GPUs
        if (nameLower.Contains("intel") && (nameLower.Contains("uhd") || nameLower.Contains("hd graphics")
            || nameLower.Contains("iris")))
            return "Integrated";
        if (nameLower.Contains("radeon") && nameLower.Contains("graphics")
            && !nameLower.Contains("rx "))
            return "Integrated";

        return "Dedicated";
    }

    private static string CollectDotNetRuntime()
    {
        try
        {
            return RuntimeInformation.FrameworkDescription;
        }
        catch
        {
            return "Unknown";
        }
    }

    private string CollectElevation()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            bool isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);

            if (!isAdmin)
                return "Standard";

            return _interactiveUserService.IsOtsElevation ? "Admin (OTS)" : "Admin";
        }
        catch
        {
            return "Unknown";
        }
    }

    private static string CollectFirmwareType()
    {
        try
        {
            if (Kernel32Api.GetFirmwareType(out var firmwareType))
            {
                return firmwareType switch
                {
                    Kernel32Api.FirmwareType.Bios => "Legacy BIOS",
                    Kernel32Api.FirmwareType.Uefi => "UEFI",
                    _ => "Unknown"
                };
            }
        }
        catch { /* P/Invoke failure */ }

        return "Unknown";
    }

    private static string CollectSecureBoot()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\SecureBoot\State");
            if (key == null)
                return "Not Supported";

            var value = key.GetValue("UEFISecureBootEnabled");
            if (value != null)
            {
                int enabled = Convert.ToInt32(value);
                return enabled == 1 ? "Enabled" : "Disabled";
            }
        }
        catch { /* registry failure */ }

        return "Unknown";
    }

    private static string CollectTpm()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\cimv2\Security\MicrosoftTpm",
                "SELECT SpecVersion FROM Win32_Tpm");
            using var collection = searcher.Get();

            foreach (ManagementObject obj in collection)
            {
                using (obj)
                {
                    string specVersion = obj["SpecVersion"] as string ?? "";
                    if (!string.IsNullOrEmpty(specVersion))
                    {
                        // SpecVersion is like "2.0, 0, 1.59" — take the major part
                        string major = specVersion.Split(',')[0].Trim();
                        return major;
                    }
                }
            }
        }
        catch { /* WMI failure — TPM may not exist */ }

        return "Not Detected";
    }
}

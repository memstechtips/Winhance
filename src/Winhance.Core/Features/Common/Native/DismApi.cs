using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Winhance.Core.Features.Common.Native;

public static class DismApi
{
    // --- Constants ---

    public const string DISM_ONLINE_IMAGE_PATH = "DISM_{53BFAE52-B167-4E2F-A258-0A37B57FF845}";
    public const int DismLogErrors = 0;
    public const int DismStateInstalled = 4;
    public const int S_OK = 0;

    // --- Delegate ---

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate void DismProgressCallback(uint current, uint total, IntPtr userData);

    // No-op callback required by the DISM servicing engine to pump its internal state machine.
    // Passing null for the progress callback on write operations causes the engine to hang.
    public static readonly DismProgressCallback NoOpProgressCallback = (_, _, _) => { };

    // --- Structs ---

    // Pack = 4 is required: the native DISM structs are 12 bytes on x64
    // (IntPtr 8 + int 4), not 16. Default managed packing pads to 16,
    // causing every array element after the first to be read at the wrong offset.
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct DISM_CAPABILITY
    {
        public IntPtr Name;
        public int State;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct DISM_FEATURE
    {
        public IntPtr FeatureName;
        public int State;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DISM_IMAGE_INFO
    {
        public int ImageType;
        public uint ImageIndex;
        public IntPtr ImageName;
        public IntPtr ImageDescription;
        public long ImageSize;
        public uint Architecture;
        public IntPtr ProductName;
        public IntPtr EditionId;
        public IntPtr InstallationType;
        public IntPtr Hal;
        public IntPtr ProductType;
        public IntPtr ProductSuite;
        public uint MajorVersion;
        public uint MinorVersion;
        public uint Build;
        public uint SpBuild;
        public uint SpLevel;
        public int Bootable;
        public IntPtr SystemRoot;
        public IntPtr Language;
        public uint LanguageCount;
        public uint DefaultLanguageIndex;
        public IntPtr CustomizedInfo;
    }

    // --- P/Invoke Functions ---

    [DllImport("dismapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern int DismInitialize(
        int logLevel,
        [MarshalAs(UnmanagedType.LPWStr)] string? logFilePath,
        [MarshalAs(UnmanagedType.LPWStr)] string? scratchDirectory);

    [DllImport("dismapi.dll", SetLastError = true)]
    public static extern int DismShutdown();

    [DllImport("dismapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern int DismOpenSession(
        [MarshalAs(UnmanagedType.LPWStr)] string imagePath,
        [MarshalAs(UnmanagedType.LPWStr)] string? windowsDirectory,
        [MarshalAs(UnmanagedType.LPWStr)] string? systemDrive,
        out uint session);

    [DllImport("dismapi.dll", SetLastError = true)]
    public static extern int DismCloseSession(uint session);

    [DllImport("dismapi.dll", SetLastError = true)]
    public static extern int DismDelete(IntPtr dismStructure);

    [DllImport("dismapi.dll", SetLastError = true)]
    public static extern int DismGetCapabilities(
        uint session,
        out IntPtr capability,
        out uint count);

    [DllImport("dismapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern int DismGetFeatures(
        uint session,
        [MarshalAs(UnmanagedType.LPWStr)] string? identifier,
        int packageIdentifier,
        out IntPtr feature,
        out uint count);

    [DllImport("dismapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern int DismRemoveCapability(
        uint session,
        [MarshalAs(UnmanagedType.LPWStr)] string capabilityName,
        IntPtr cancelEvent,
        DismProgressCallback? progress,
        IntPtr userData);

    [DllImport("dismapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern int DismDisableFeature(
        uint session,
        [MarshalAs(UnmanagedType.LPWStr)] string featureName,
        [MarshalAs(UnmanagedType.LPWStr)] string? packageName,
        [MarshalAs(UnmanagedType.Bool)] bool removePayload,
        IntPtr cancelEvent,
        DismProgressCallback? progress,
        IntPtr userData);

    [DllImport("dismapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern int DismAddProvisionedAppxPackage(
        uint session,
        [MarshalAs(UnmanagedType.LPWStr)] string appPath,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr)] string[]? dependencyPackages,
        uint dependencyPackageCount,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr)] string[]? licensePaths,
        uint licensePathCount,
        [MarshalAs(UnmanagedType.Bool)] bool skipLicense,
        [MarshalAs(UnmanagedType.LPWStr)] string? customDataPath,
        [MarshalAs(UnmanagedType.LPWStr)] string? region,
        IntPtr cancelEvent,
        DismProgressCallback? progress,
        IntPtr userData);

    [DllImport("dismapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern int DismGetImageInfo(
        [MarshalAs(UnmanagedType.LPWStr)] string imageFilePath,
        out IntPtr imageInfo,
        out uint count);

    // --- Helpers ---

    public static T[] MarshalArray<T>(IntPtr ptr, uint count) where T : struct
    {
        var result = new T[count];
        var structSize = Marshal.SizeOf<T>();
        for (uint i = 0; i < count; i++)
        {
            result[i] = Marshal.PtrToStructure<T>(ptr + (int)(i * structSize));
        }
        return result;
    }

    public static void ThrowIfFailed(int hr, string operation)
    {
        if (hr < 0)
        {
            throw new Win32Exception(hr, $"DISM {operation} failed with HRESULT 0x{hr:X8}");
        }
    }
}

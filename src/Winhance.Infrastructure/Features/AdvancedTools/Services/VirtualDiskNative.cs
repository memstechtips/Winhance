using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Storage.FileSystem;
using Windows.Win32.Storage.Vhd;
using Windows.Win32.System.Ioctl;

namespace Winhance.Infrastructure.Features.AdvancedTools.Services;

internal sealed class VirtualDiskNative : IVirtualDiskNative
{
    // A volume GUID name is \\?\Volume{xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx}\ - 50 characters.
    // FindFirstVolume's page specifies this buffer size.
    private const int VolumeNameBufferLength = 50;

    // \\.\CDROM99 and the like; the physical-path buffer is measured in bytes, not characters.
    private const int PhysicalPathBufferChars = 260;

    public IVirtualDiskHandle Open(string isoPath)
    {
        var storageType = new VIRTUAL_STORAGE_TYPE
        {
            DeviceId = PInvoke.VIRTUAL_STORAGE_TYPE_DEVICE_ISO,
            VendorId = PInvoke.VIRTUAL_STORAGE_TYPE_VENDOR_MICROSOFT,
        };

        // VIRTUAL_DISK_ACCESS_READ is ATTACH_RO | DETACH | GET_INFO, which is every right the
        // attach, the physical-path lookup and the detach need, and no write right at all.
        var error = OpenIso(storageType, isoPath, out var handle);
        if (error != WIN32_ERROR.ERROR_SUCCESS)
        {
            handle?.Dispose();
            throw new Win32Exception((int)error, DescribeOpenFailure(error, isoPath));
        }

        return new VirtualDiskHandle(handle!);
    }

    public void Attach(IVirtualDiskHandle handle, AttachFlags flags)
    {
        var error = AttachIso(Unwrap(handle), Translate(flags));
        if (error != WIN32_ERROR.ERROR_SUCCESS)
        {
            throw new Win32Exception((int)error, $"Could not attach the ISO (error {(int)error}).");
        }
    }

    public string GetVolumeRootPath(IVirtualDiskHandle handle)
    {
        var physicalPath = ReadPhysicalPath(Unwrap(handle));
        var device = ReadDeviceNumber(physicalPath);

        foreach (var volume in EnumerateVolumes())
        {
            // The trailing backslash makes it a root path, which CreateFile rejects for a device
            // open; every other consumer wants it, so it is trimmed here and kept everywhere else.
            var volumeDevicePath = volume.TrimEnd('\\');
            if (TryReadDeviceNumber(volumeDevicePath, out var candidate)
                && candidate.DeviceType == device.DeviceType
                && candidate.DeviceNumber == device.DeviceNumber)
            {
                return volume;
            }
        }

        throw new InvalidOperationException(
            $"The ISO attached as {physicalPath} but no volume reported that device.");
    }

    private static unsafe WIN32_ERROR OpenIso(
        VIRTUAL_STORAGE_TYPE storageType,
        string isoPath,
        out SafeFileHandle? handle)
    {
        return PInvoke.OpenVirtualDisk(
            storageType,
            isoPath,
            VIRTUAL_DISK_ACCESS_MASK.VIRTUAL_DISK_ACCESS_READ,
            OPEN_VIRTUAL_DISK_FLAG.OPEN_VIRTUAL_DISK_FLAG_NONE,
            null,
            out handle);
    }

    private static unsafe WIN32_ERROR AttachIso(SafeFileHandle handle, ATTACH_VIRTUAL_DISK_FLAG flags)
    {
        return PInvoke.AttachVirtualDisk(handle, default, flags, 0, null, null);
    }

    private static unsafe string ReadPhysicalPath(SafeFileHandle handle)
    {
        Span<char> buffer = stackalloc char[PhysicalPathBufferChars];
        uint sizeInBytes = PhysicalPathBufferChars * sizeof(char);

        var error = PInvoke.GetVirtualDiskPhysicalPath(handle, ref sizeInBytes, buffer);
        if (error != WIN32_ERROR.ERROR_SUCCESS)
        {
            throw new Win32Exception((int)error, $"Could not read the attached ISO's device path (error {(int)error}).");
        }

        return ReadNullTerminated(buffer);
    }

    private static unsafe List<string> EnumerateVolumes()
    {
        var volumes = new List<string>();
        var buffer = new char[VolumeNameBufferLength];

        using var search = PInvoke.FindFirstVolume(buffer);
        if (search.IsInvalid)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not enumerate volumes.");
        }

        var handle = (HANDLE)search.DangerousGetHandle();
        do
        {
            volumes.Add(ReadNullTerminated(buffer));
        }
        while (PInvoke.FindNextVolume(handle, buffer));

        // Nothing in the loop references `search`, so without this the finalizer could close the
        // search handle out from under FindNextVolume.
        GC.KeepAlive(search);

        return volumes;
    }

    private static string ReadNullTerminated(ReadOnlySpan<char> buffer)
    {
        var end = buffer.IndexOf('\0');
        return new string(buffer[..(end < 0 ? buffer.Length : end)]);
    }

    private static STORAGE_DEVICE_NUMBER ReadDeviceNumber(string devicePath)
    {
        if (!TryReadDeviceNumber(devicePath, out var number))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not identify the device behind {devicePath}.");
        }

        return number;
    }

    private static unsafe bool TryReadDeviceNumber(string devicePath, out STORAGE_DEVICE_NUMBER number)
    {
        number = default;

        // No access rights are requested: IOCTL_STORAGE_GET_DEVICE_NUMBER is a read of the device's
        // identity, and asking for GENERIC_READ would fail on a volume the caller cannot read.
        using var device = CreateDeviceHandle(devicePath);
        if (device.IsInvalid)
        {
            return false;
        }

        STORAGE_DEVICE_NUMBER result = default;
        uint returned = 0;
        var ok = PInvoke.DeviceIoControl(
            (HANDLE)device.DangerousGetHandle(),
            PInvoke.IOCTL_STORAGE_GET_DEVICE_NUMBER,
            null,
            0,
            &result,
            (uint)sizeof(STORAGE_DEVICE_NUMBER),
            &returned,
            null);

        if (!ok)
        {
            return false;
        }

        number = result;
        return true;
    }

    private static unsafe SafeFileHandle CreateDeviceHandle(string devicePath)
    {
        return PInvoke.CreateFile(
            devicePath,
            0,
            FILE_SHARE_MODE.FILE_SHARE_READ | FILE_SHARE_MODE.FILE_SHARE_WRITE,
            null,
            FILE_CREATION_DISPOSITION.OPEN_EXISTING,
            FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_NORMAL,
            null);
    }

    private static ATTACH_VIRTUAL_DISK_FLAG Translate(AttachFlags flags)
    {
        var translated = ATTACH_VIRTUAL_DISK_FLAG.ATTACH_VIRTUAL_DISK_FLAG_NONE;

        if (flags.HasFlag(AttachFlags.ReadOnly))
            translated |= ATTACH_VIRTUAL_DISK_FLAG.ATTACH_VIRTUAL_DISK_FLAG_READ_ONLY;

        if (flags.HasFlag(AttachFlags.NoDriveLetter))
            translated |= ATTACH_VIRTUAL_DISK_FLAG.ATTACH_VIRTUAL_DISK_FLAG_NO_DRIVE_LETTER;

        if (flags.HasFlag(AttachFlags.PermanentLifetime))
            translated |= ATTACH_VIRTUAL_DISK_FLAG.ATTACH_VIRTUAL_DISK_FLAG_PERMANENT_LIFETIME;

        return translated;
    }

    private static SafeFileHandle Unwrap(IVirtualDiskHandle handle)
    {
        return handle is VirtualDiskHandle owned
            ? owned.Handle
            : throw new ArgumentException("Handle did not come from this native layer.", nameof(handle));
    }

    private static string DescribeOpenFailure(WIN32_ERROR error, string isoPath)
    {
        // AttachVirtualDisk's page: "The host volume that contains the virtual disk image file
        // cannot be compressed or EFS encrypted." Windows reports that as a plain access denial,
        // which sends users looking at permissions instead of at the file's attributes.
        return error == WIN32_ERROR.ERROR_ACCESS_DENIED
            ? $"Could not open '{isoPath}'. Windows cannot attach an ISO stored on a compressed or encrypted volume - copy it somewhere else and try again."
            : $"Could not open '{isoPath}' (error {(int)error}).";
    }

    private sealed class VirtualDiskHandle : IVirtualDiskHandle
    {
        internal VirtualDiskHandle(SafeFileHandle handle) => Handle = handle;

        internal SafeFileHandle Handle { get; }

        public bool IsClosed => Handle.IsClosed;

        public void Dispose() => Handle.Dispose();
    }
}

using System.Reflection;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Storage.Imapi;
using Windows.Win32.System.Com;

namespace Winhance.Infrastructure.Features.AdvancedTools.Services;

internal sealed class Imapi2FileSystemImage : IFileSystemImageWrapper
{
    private const string BootImageOptionsArrayProperty = "BootImageOptionsArray";

    // STGM_READ | STGM_SHARE_DENY_NONE. AssignBootImage takes an IStream and nothing else.
    private const uint BootImageOpenMode = 0x00000040;

    private readonly object _raw;
    private readonly IFileSystemImage2 _image;
    private readonly List<object> _bootObjects = [];
    private readonly List<IStream> _bootStreams = [];
    private bool _disposed;

    public Imapi2FileSystemImage()
    {
        // Created through the CLSID rather than `new MsftFileSystemImage()` so the RCW is a
        // late-bindable __ComObject: BootImageOptionsArray has to go through IDispatch (see the
        // setter), and only this form guarantees that.
        var type = Type.GetTypeFromCLSID(typeof(MsftFileSystemImage).GUID)
            ?? throw new InvalidOperationException("IMAPI2FS is not registered on this system.");

        _raw = Activator.CreateInstance(type)
            ?? throw new InvalidOperationException("Could not create the IMAPI2 file system image.");

        _image = (IFileSystemImage2)_raw;
    }

    public IsoFileSystems FileSystemsToCreate
    {
        get => (IsoFileSystems)_image.FileSystemsToCreate;
        set => _image.FileSystemsToCreate = (FsiFileSystems)value;
    }

    public int UdfRevision
    {
        get => _image.UDFRevision;
        set => _image.UDFRevision = value;
    }

    public int FreeMediaBlocks
    {
        get => _image.FreeMediaBlocks;
        set => _image.FreeMediaBlocks = value;
    }

    public bool StageFiles
    {
        get => _image.StageFiles;
        set => _image.StageFiles = value;
    }

    public string VolumeName
    {
        get => _image.VolumeName.ToString();
        set
        {
            var bstr = Marshal.StringToBSTR(value);
            try
            {
                _image.VolumeName = (BSTR)bstr;
            }
            finally
            {
                Marshal.FreeBSTR(bstr);
            }
        }
    }

    public int BootImageEntryCount
    {
        get
        {
            // The read-back exists to prove the assignment took; the entries themselves are
            // opaque COM objects, so only their count is recoverable, and count is what matters.
            var value = _raw.GetType().InvokeMember(
                BootImageOptionsArrayProperty,
                BindingFlags.GetProperty,
                null,
                _raw,
                null);

            return value is object[] entries ? entries.Length : 0;
        }
    }

    public void SetBootImageOptions(IReadOnlyList<BootEntry> entries)
    {
        var options = entries.Select(CreateBootOptions).ToArray();

        // A late-bound put, not the typed SAFEARRAY property: measured 2026-08-26, a C#
        // object[] through IDispatch was accepted and read back two entries, while a
        // loosely-typed array came back E_NOINTERFACE - which reads like "IMAPI2 refuses two
        // boot entries" and is not that at all.
        _raw.GetType().InvokeMember(
            BootImageOptionsArrayProperty,
            BindingFlags.SetProperty,
            null,
            _raw,
            [options]);
    }

    public void AddTree(string sourceDirectory, bool includeBaseDirectory)
    {
        var bstr = Marshal.StringToBSTR(sourceDirectory);
        try
        {
            _image.Root.AddTree((BSTR)bstr, includeBaseDirectory);
        }
        finally
        {
            Marshal.FreeBSTR(bstr);
        }
    }

    public IIsoResultImage CreateResultImage()
    {
        _image.CreateResultImage(out var result);
        return new Imapi2ResultImage(result);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var stream in _bootStreams)
        {
            Release(stream);
        }

        foreach (var boot in _bootObjects)
        {
            Release(boot);
        }

        _bootStreams.Clear();
        _bootObjects.Clear();
        Release(_raw);
    }

    // CsWin32 generates these as [ComImport] interfaces, so every one is a runtime RCW; the guard
    // is for the day that stops being true, when there would be nothing to release.
    private static void Release(object com)
    {
        if (Marshal.IsComObject(com))
        {
            _ = Marshal.ReleaseComObject(com);
        }
    }

    private object CreateBootOptions(BootEntry entry)
    {
        var type = Type.GetTypeFromCLSID(typeof(BootOptions).GUID)
            ?? throw new InvalidOperationException("IMAPI2FS boot options are not registered on this system.");

        var raw = Activator.CreateInstance(type)
            ?? throw new InvalidOperationException("Could not create the IMAPI2 boot options.");

        // Tracked before anything can throw, so Dispose releases it even if AssignBootImage fails.
        _bootObjects.Add(raw);

        var options = (IBootOptions)raw;
        options.PlatformId = (PlatformId)entry.Platform;

        // oscdimg's ",e," - the boot image is read directly, not through a floppy or hard-disk
        // emulation, which is what every Windows installation ISO uses.
        options.Emulation = EmulationType.EmulationNone;
        // IMAPI2 keeps its own reference for CreateResultImage; this one is released in Dispose,
        // which is what lets the working folder be deleted straight after a build.
        var stream = OpenBootImage(entry.BootImagePath);
        _bootStreams.Add(stream);
        options.AssignBootImage(stream);

        return raw;
    }

    private static IStream OpenBootImage(string path)
    {
        PInvoke.SHCreateStreamOnFileEx(path, BootImageOpenMode, 0, false, null, out var stream)
            .ThrowOnFailure();

        return stream;
    }

    private sealed class Imapi2ResultImage : IIsoResultImage
    {
        // IMAPI2 hands the image back 2 KB at a time; draining it a block at a time would be
        // millions of interop calls on an 8 GB image.
        private const int ReadChunkBytes = 4 * 1024 * 1024;

        private readonly IFileSystemImageResult _result;
        private bool _disposed;

        internal Imapi2ResultImage(IFileSystemImageResult result)
        {
            _result = result;
            TotalBytes = (long)result.TotalBlocks * result.BlockSize;
        }

        public long TotalBytes { get; }

        public unsafe void WriteTo(string outputPath, Action<long, long>? onProgress, CancellationToken cancellationToken)
        {
            var stream = _result.ImageStream;
            var buffer = new byte[ReadChunkBytes];
            var written = 0L;

            try
            {
                using var file = new FileStream(outputPath, FileMode.Create, FileAccess.Write);

                while (written < TotalBytes)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var read = ReadChunk(stream, buffer);
                    if (read == 0)
                    {
                        break;
                    }

                    file.Write(buffer, 0, (int)read);
                    written += read;
                    onProgress?.Invoke(written, TotalBytes);
                }

                file.Flush();
            }
            finally
            {
                _ = Marshal.ReleaseComObject(stream);
            }

            if (written < TotalBytes)
            {
                throw new IOException(
                    $"IMAPI2 announced {TotalBytes:N0} bytes but its stream ended after {written:N0}.");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _ = Marshal.ReleaseComObject(_result);
        }

        private static unsafe uint ReadChunk(IStream stream, byte[] buffer)
        {
            uint read;
            fixed (byte* pBuffer = buffer)
            {
                stream.Read(pBuffer, (uint)buffer.Length, &read).ThrowOnFailure();
            }

            return read;
        }
    }
}

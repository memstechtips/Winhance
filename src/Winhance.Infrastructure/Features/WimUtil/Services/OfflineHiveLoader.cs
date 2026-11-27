using System.ComponentModel;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.WimUtil.Services;

internal sealed class OfflineHiveLoader(IRegistryHiveNative hives, IFileSystemService files, ILogService log) : IOfflineHiveLoader
{
    private const int UnloadAttempts = 5;
    private const string MachineRoot = "HKEY_LOCAL_MACHINE";

    internal TimeSpan RetryDelay { get; init; } = TimeSpan.FromMilliseconds(500);

    public async Task<IOfflineHiveSession> LoadAsync(string mountDir, CancellationToken ct)
    {
        // A fixed name would collide with a hive a crashed run left loaded, so every session picks its own.
        var id = Guid.NewGuid().ToString("N")[..8];
        var session = new Session(this, $"WH_SOFTWARE_{id}", $"WH_SYSTEM_{id}", $"WH_NTUSER_{id}");
        try
        {
            LoadHive(session, session.SoftwareName, files.CombinePath(mountDir, "Windows", "System32", "config", "SOFTWARE"), ct);
            LoadHive(session, session.SystemName, files.CombinePath(mountDir, "Windows", "System32", "config", "SYSTEM"), ct);
            LoadHive(session, session.UserName, files.CombinePath(mountDir, "Users", "Default", "NTUSER.DAT"), ct);
            return session;
        }
        catch
        {
            await session.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private void LoadHive(Session session, string name, string file, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!files.FileExists(file))
        {
            throw new FileNotFoundException($"The image has no registry hive at {file}", file);
        }

        var error = hives.Load(name, file);
        if (error != 0)
        {
            throw new Win32Exception(error, $"Could not load the registry hive {file}: {new Win32Exception(error).Message}");
        }
        session.Loaded.Add(name);
    }

    // One hive that will not unload must not leave the rest loaded, or the image cannot be unmounted.
    private async Task UnloadAsync(string name)
    {
        try
        {
            await RetryUnloadAsync(name).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            log.LogWarning($"Unloading the registry hive {name} failed: {ex.Message}");
        }
    }

    // An open key handle that has not been finalized yet keeps a hive busy, so a failed unload forces a collection first.
    private async Task RetryUnloadAsync(string name)
    {
        var error = 0;
        for (var attempt = 1; attempt <= UnloadAttempts; attempt++)
        {
            error = hives.Unload(name);
            if (error == 0)
            {
                return;
            }

            if (attempt < UnloadAttempts)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                await Task.Delay(RetryDelay).ConfigureAwait(false);
            }
        }

        log.LogWarning($"Could not unload the registry hive {name} ({new Win32Exception(error).Message}); the image cannot be unmounted cleanly until it is");
    }

    private sealed class Session(OfflineHiveLoader loader, string softwareName, string systemName, string userName) : IOfflineHiveSession
    {
        private bool _disposed;

        public string SoftwareName { get; } = softwareName;

        public string SystemName { get; } = systemName;

        public string UserName { get; } = userName;

        public List<string> Loaded { get; } = [];

        public string SoftwareRoot => $@"{MachineRoot}\{SoftwareName}";

        public string SystemRoot => $@"{MachineRoot}\{SystemName}";

        public string DefaultUserRoot => $@"{MachineRoot}\{UserName}";

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;

            for (var i = Loaded.Count - 1; i >= 0; i--)
            {
                await loader.UnloadAsync(Loaded[i]).ConfigureAwait(false);
            }
        }
    }
}

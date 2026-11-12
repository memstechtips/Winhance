namespace Winhance.Infrastructure.Features.WimUtil.Services;

internal interface IOfflineHiveLoader
{
    Task<IOfflineHiveSession> LoadAsync(string mountDir, CancellationToken ct);
}

// Roots are full key paths under HKEY_LOCAL_MACHINE; disposing unloads every hive the session loaded.
internal interface IOfflineHiveSession : IAsyncDisposable
{
    string SoftwareRoot { get; }

    string SystemRoot { get; }

    string DefaultUserRoot { get; }
}

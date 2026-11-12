using Winhance.Infrastructure.Features.WimUtil.Services;

namespace Winhance.Infrastructure.Tests.WimUtil.WimServices;

internal sealed class FakeHiveSession(string id = "t") : IOfflineHiveSession
{
    public string SoftwareRoot { get; } = $@"HKEY_LOCAL_MACHINE\WH_SOFTWARE_{id}";

    public string SystemRoot { get; } = $@"HKEY_LOCAL_MACHINE\WH_SYSTEM_{id}";

    public string DefaultUserRoot { get; } = $@"HKEY_LOCAL_MACHINE\WH_NTUSER_{id}";

    public bool Disposed { get; private set; }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }
}

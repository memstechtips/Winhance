using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

internal sealed class InstallOptionsService(IUserPreferencesService preferences) : IInstallOptionsService
{
    private readonly object _lock = new();
    private InstallOptionsStore? _store;
    private InstallOptionsStore? _session;

    public InstallOptions Defaults => Store.Defaults;

    public InstallOptions? ForApp(string appId) => Store.Apps.GetValueOrDefault(appId);

    public InstallOptions EffectiveFor(string appId) => ForApp(appId) ?? Defaults;

    public Task SetDefaultsAsync(InstallOptions options) => UpdateAsync(store => store with { Defaults = options });

    public Task SetForAppAsync(string appId, InstallOptions? options) => UpdateAsync(store =>
    {
        var apps = new Dictionary<string, InstallOptions>(store.Apps, StringComparer.Ordinal);
        if (options is null)
            apps.Remove(appId);
        else
            apps[appId] = options;
        return store with { Apps = apps };
    });

    public InstallOptionsStore Current => Store;

    // One slot and an EndSession with no owner: a nested session would be ended by the inner owner and leave the outer
    // one on this PC's saved options, so nesting throws instead.
    public void BeginSession(InstallOptionsStore options)
    {
        lock (_lock)
        {
            if (_session is not null)
                throw new InvalidOperationException("An install-options session is already open; end it before starting another.");
            _session = options;
        }
    }

    public void EndSession()
    {
        lock (_lock)
            _session = null;
    }

    private InstallOptionsStore Store
    {
        get
        {
            lock (_lock)
                return _session ?? (_store ??= Load());
        }
    }

    private Task UpdateAsync(Func<InstallOptionsStore, InstallOptionsStore> change)
    {
        InstallOptionsStore updated;
        lock (_lock)
        {
            if (_session is not null)
            {
                _session = change(_session);
                return Task.CompletedTask;
            }
            updated = _store = change(_store ?? Load());
        }
        return preferences.SetPreferenceAsync(UserPreferenceKeys.AppInstallOptions, updated);
    }

    // Until the options are first saved, someone who had "install for all users" switched on keeps machine-wide installs.
    private InstallOptionsStore Load()
    {
        var saved = preferences.GetPreference<InstallOptionsStore?>(UserPreferenceKeys.AppInstallOptions, null);
        if (saved is not null)
            return saved;

        return preferences.GetPreference(UserPreferenceKeys.InstallAppsForAllUsers, false)
            ? new InstallOptionsStore { Defaults = InstallOptions.Default with { Scope = InstallScope.Machine } }
            : new InstallOptionsStore();
    }
}

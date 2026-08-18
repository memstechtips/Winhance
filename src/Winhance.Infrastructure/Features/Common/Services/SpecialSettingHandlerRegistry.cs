using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services;

// The handler set is resolved LAZILY (on first lookup) rather than eagerly at construction. A handler's graph can
// transitively depend back on a service that depends on this registry (e.g. UpdateService -> IStateWriter ->
// PowerCfgApplier -> ComboBoxResolver -> ISystemSettingsDiscoveryService -> the discovery registry); resolving the
// handlers inside the registry's DI factory would re-enter singleton construction and DEADLOCK. Deferring to first
// use breaks that cycle - by lookup time every singleton is already built, so the factory just returns cached ones.
public sealed class SpecialSettingHandlerRegistry : ISpecialSettingHandlerRegistry
{
    private readonly Lazy<IReadOnlyDictionary<string, ISpecialSettingHandler>> _handlers;

    public SpecialSettingHandlerRegistry(Func<IReadOnlyDictionary<string, ISpecialSettingHandler>> handlersFactory)
        => _handlers = new Lazy<IReadOnlyDictionary<string, ISpecialSettingHandler>>(handlersFactory);

    public ISpecialSettingHandler? TryGet(string settingId)
        => _handlers.Value.TryGetValue(settingId, out var h) ? h : null;
}

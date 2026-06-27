// File: src/Winhance.Infrastructure/Features/Common/Services/SpecialDiscoveryRegistry.cs
using System;
using System.Collections.Generic;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services;

// Resolved LAZILY (on first enumeration), not eagerly at construction. SystemSettingsDiscoveryService depends on this
// registry, and a handler's graph (UpdateService -> IStateWriter -> PowerCfgApplier -> ComboBoxResolver) cycles back
// to ISystemSettingsDiscoveryService; resolving the handlers inside the registry's DI factory re-enters singleton
// construction and DEADLOCKS. Deferring to first All-access breaks the cycle - by then discovery is fully built.
public sealed class SpecialDiscoveryRegistry : ISpecialDiscoveryRegistry
{
    private readonly Lazy<IReadOnlyList<ISpecialSettingHandler>> _handlers;

    public SpecialDiscoveryRegistry(Func<IReadOnlyList<ISpecialSettingHandler>> handlersFactory)
        => _handlers = new Lazy<IReadOnlyList<ISpecialSettingHandler>>(handlersFactory);

    public IEnumerable<ISpecialSettingHandler> All => _handlers.Value;
}

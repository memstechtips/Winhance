using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services;

// The handler set is resolved LAZILY (on first lookup) rather than in the registry's DI factory. The registry is a
// dependency of SettingApplicationService, so building the handlers' graphs inside that factory would let any
// handler that reaches the apply service re-enter singleton construction. By first lookup every singleton exists.
internal sealed class SpecialSettingHandlerRegistry : ISpecialSettingHandlerRegistry
{
    private readonly Lazy<IReadOnlyDictionary<string, ISpecialSettingHandler>> _handlers;

    public SpecialSettingHandlerRegistry(Func<IReadOnlyDictionary<string, ISpecialSettingHandler>> handlersFactory)
        => _handlers = new Lazy<IReadOnlyDictionary<string, ISpecialSettingHandler>>(handlersFactory);

    public ISpecialSettingHandler? TryGet(string settingId)
        => _handlers.Value.TryGetValue(settingId, out var h) ? h : null;
}

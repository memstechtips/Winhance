using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services;

// Resolved lazily, on first lookup: resolving the providers inside the registry's DI factory re-enters singleton
// construction for any provider whose graph reaches back to a service holding this registry, and deadlocks.
internal sealed class OptionProviderRegistry : IOptionProviderRegistry
{
    private readonly Lazy<IReadOnlyDictionary<OptionSource, IOptionProvider>> _providers;

    public OptionProviderRegistry(Func<IEnumerable<IOptionProvider>> providersFactory)
        => _providers = new Lazy<IReadOnlyDictionary<OptionSource, IOptionProvider>>(
            () => providersFactory()
                .SelectMany(provider => provider.Sources, (provider, source) => (Source: source, Provider: provider))
                .ToDictionary(pair => pair.Source, pair => pair.Provider));

    public IOptionProvider For(OptionSource source)
        => _providers.Value.TryGetValue(source, out var provider)
            ? provider
            : throw new InvalidOperationException($"No option provider handles the {source} option list.");
}

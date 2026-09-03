using Winhance.Infrastructure.Features.Common.Services;

namespace Winhance.Infrastructure.Tests.Services;

// Shared IWmiApi fake for every Services test in this folder. Keyed by class name; ignores the
// WHERE condition text production code passes, since every current test only needs "is a matching
// instance registered or not", not condition parsing.
internal sealed class FakeWmiApi : IWmiApi
{
    private readonly Dictionary<string, List<FakeWmiInstance>> _instances = new(StringComparer.Ordinal);

    public List<(string Scope, string ClassName, string Method, IReadOnlyDictionary<string, object> Parameters)>
        ClassInvocations
    { get; } = [];

    public List<FakeWmiInstance> For(string className) =>
        _instances.TryGetValue(className, out var list) ? list : _instances[className] = [];

    public IReadOnlyList<IWmiInstance> Query(string scope, string className, string? condition) =>
        _instances.TryGetValue(className, out var list) ? list : [];

    public WmiMethodResult InvokeClassMethod(
        string scope, string className, string method, IReadOnlyDictionary<string, object>? parameters)
    {
        ClassInvocations.Add((scope, className, method, parameters ?? new Dictionary<string, object>()));
        return new WmiMethodResult(0, new FakeWmiInstance());
    }
}

internal sealed class FakeWmiInstance : IWmiInstance
{
    private readonly Dictionary<string, object?> _properties = new(StringComparer.Ordinal);

    public object? this[string property]
    {
        get => Get(property);
        set => _properties[property] = value;
    }

    public object? Get(string property) => _properties.GetValueOrDefault(property);

    public IReadOnlyList<IWmiInstance> GetRelated(string className) => [];

    public WmiMethodResult Invoke(string method, IReadOnlyDictionary<string, object>? parameters) =>
        new(0, new FakeWmiInstance());

    public void Dispose()
    {
    }
}

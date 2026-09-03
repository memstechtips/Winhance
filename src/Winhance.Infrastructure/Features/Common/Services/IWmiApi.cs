namespace Winhance.Infrastructure.Features.Common.Services;

// The WMI surface every WMI-backed decision in Infrastructure needs: query a class in a given
// namespace, read a property, follow an association, invoke a method on an instance a query
// already found, or invoke a method directly on a class (SystemRestore.Enable has no instance to
// query first). It decides nothing; the decisions stay in the caller, where a fake behind this
// seam can reach them without a disk, a battery or a live restore point.
internal interface IWmiApi
{
    IReadOnlyList<IWmiInstance> Query(string scope, string className, string? condition);

    WmiMethodResult InvokeClassMethod(
        string scope, string className, string method, IReadOnlyDictionary<string, object>? parameters);
}

internal interface IWmiInstance : IDisposable
{
    // Values arrive as the provider types them: UInt16 for enums like PartitionStyle/BusType,
    // Char16 as a ushort for DriveLetter, UInt64 for sizes, and an embedded instance for output
    // parameters like CreatedPartition and ExtendedStatus. Null when the instance has no such
    // property.
    object? Get(string property);

    IReadOnlyList<IWmiInstance> GetRelated(string className);

    WmiMethodResult Invoke(string method, IReadOnlyDictionary<string, object>? parameters);
}

// ReturnValue plus the method's out-parameters, embedded objects included.
internal sealed class WmiMethodResult(uint returnValue, IWmiInstance output) : IDisposable
{
    public uint ReturnValue { get; } = returnValue;

    public IWmiInstance Output { get; } = output;

    public void Dispose() => Output.Dispose();
}

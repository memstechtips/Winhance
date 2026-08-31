namespace Winhance.Infrastructure.Features.AdvancedTools.Services;

// The Storage Management API (root\Microsoft\Windows\Storage) as WmiStorageService needs it: query a
// class, read a property, follow an association, invoke a method. It decides nothing; the decisions
// stay in WmiStorageService, where a fake behind this seam can reach them without a disk.
internal interface IStorageManagementApi
{
    IReadOnlyList<IStorageInstance> Query(string className, string? condition);
}

internal interface IStorageInstance : IDisposable
{
    // Values arrive as the provider types them: UInt16 for PartitionStyle and BusType, Char16 as a
    // ushort for DriveLetter, UInt64 for sizes, and an embedded instance for CreatedPartition and
    // ExtendedStatus. Null when the instance has no such property.
    object? Get(string property);

    IReadOnlyList<IStorageInstance> GetRelated(string className);

    StorageMethodResult Invoke(string method, IReadOnlyDictionary<string, object>? parameters);
}

// ReturnValue plus the method's out-parameters, embedded objects included.
internal sealed class StorageMethodResult(uint returnValue, IStorageInstance output) : IDisposable
{
    public uint ReturnValue { get; } = returnValue;

    public IStorageInstance Output { get; } = output;

    public void Dispose() => Output.Dispose();
}

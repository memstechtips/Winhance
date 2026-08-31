using System.Globalization;
using System.Management;

namespace Winhance.Infrastructure.Features.AdvancedTools.Services;

internal sealed class WmiStorageManagementApi : IStorageManagementApi
{
    private const string StorageNamespace = @"root\Microsoft\Windows\Storage";

    public IReadOnlyList<IStorageInstance> Query(string className, string? condition)
    {
        var query = condition is null
            ? $"SELECT * FROM {className}"
            : $"SELECT * FROM {className} WHERE {condition}";

        using var searcher = new ManagementObjectSearcher(new ManagementScope(StorageNamespace), new ObjectQuery(query));
        return searcher.Get().Cast<ManagementObject>().Select(found => (IStorageInstance)new WmiInstance(found)).ToArray();
    }

    private sealed class WmiInstance(ManagementBaseObject instance) : IStorageInstance
    {
        public object? Get(string property)
        {
            try
            {
                var value = instance[property];
                return value is ManagementBaseObject embedded ? new WmiInstance(embedded) : value;
            }
            catch (ManagementException ex) when (ex.ErrorCode == ManagementStatus.NotFound)
            {
                // Not every method output carries every property; ExtendedStatus in particular is
                // absent rather than null on some calls.
                return null;
            }
        }

        public IReadOnlyList<IStorageInstance> GetRelated(string className)
        {
            using var related = Bound().GetRelated(className);
            return related.Cast<ManagementObject>().Select(found => (IStorageInstance)new WmiInstance(found)).ToArray();
        }

        public StorageMethodResult Invoke(string method, IReadOnlyDictionary<string, object>? parameters)
        {
            var bound = Bound();
            using var input = parameters is null ? null : bound.GetMethodParameters(method);
            if (input is not null)
            {
                foreach (var (name, value) in parameters!)
                {
                    input[name] = value;
                }
            }

            var output = bound.InvokeMethod(method, input, null);
            var returnValue = Convert.ToUInt32(output["ReturnValue"], CultureInfo.InvariantCulture);
            return new StorageMethodResult(returnValue, new WmiInstance(output));
        }

        public void Dispose() => instance.Dispose();

        // Only a live ManagementObject can be invoked or navigated; an embedded object out of a
        // method result is a ManagementBaseObject and cannot.
        private ManagementObject Bound() =>
            instance as ManagementObject
            ?? throw new InvalidOperationException("This storage object is an embedded value, not a live instance.");
    }
}

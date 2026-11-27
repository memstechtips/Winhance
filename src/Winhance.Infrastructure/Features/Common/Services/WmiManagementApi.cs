using System.Globalization;
using System.Management;

namespace Winhance.Infrastructure.Features.Common.Services;

internal sealed class WmiManagementApi : IWmiApi
{
    public IReadOnlyList<IWmiInstance> Query(string scope, string className, string? condition)
    {
        var query = condition is null
            ? $"SELECT * FROM {className}"
            : $"SELECT * FROM {className} WHERE {condition}";

        using var searcher = new ManagementObjectSearcher(new ManagementScope(scope), new ObjectQuery(query));
        return searcher.Get().Cast<ManagementObject>().Select(found => (IWmiInstance)new WmiInstance(found)).ToArray();
    }

    public WmiMethodResult InvokeClassMethod(
        string scope, string className, string method, IReadOnlyDictionary<string, object>? parameters)
    {
        using var wmiClass = new ManagementClass(
            new ManagementScope(scope), new ManagementPath(className), new ObjectGetOptions());

        using var input = parameters is null ? null : wmiClass.GetMethodParameters(method);
        if (input is not null)
        {
            foreach (var (name, value) in parameters!)
            {
                input[name] = value;
            }
        }

        return ResultOf(wmiClass.InvokeMethod(method, input, null));
    }

    // System.Management hands back null, not an empty object, when WMI returns no out-parameters
    // for a method; that reads as ReturnValue 0 with an empty output.
    internal static WmiMethodResult ResultOf(ManagementBaseObject? output) =>
        new(output is null ? 0 : ReadReturnValue(output), new WmiInstance(output));

    // A method output with no ReturnValue property reads as 0 here, not as a failed lookup.
    private static uint ReadReturnValue(ManagementBaseObject output)
    {
        try
        {
            return Convert.ToUInt32(output["ReturnValue"], CultureInfo.InvariantCulture);
        }
        catch (ManagementException ex) when (ex.ErrorCode == ManagementStatus.NotFound)
        {
            return 0;
        }
    }

    private sealed class WmiInstance(ManagementBaseObject? instance) : IWmiInstance
    {
        public object? Get(string property)
        {
            if (instance is null)
            {
                return null;
            }

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

        public IReadOnlyList<IWmiInstance> GetRelated(string className)
        {
            using var related = Bound().GetRelated(className);
            return related.Cast<ManagementObject>().Select(found => (IWmiInstance)new WmiInstance(found)).ToArray();
        }

        public WmiMethodResult Invoke(string method, IReadOnlyDictionary<string, object>? parameters)
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

            return ResultOf(bound.InvokeMethod(method, input, null));
        }

        public void Dispose() => instance?.Dispose();

        // Only a live ManagementObject can be invoked or navigated; an embedded object out of a
        // method result is a ManagementBaseObject and cannot, and an empty output has nothing at all.
        private ManagementObject Bound() =>
            instance as ManagementObject
            ?? throw new InvalidOperationException("This WMI object is not a live instance.");
    }
}

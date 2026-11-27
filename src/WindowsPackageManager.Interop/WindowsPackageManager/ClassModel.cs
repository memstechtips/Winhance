// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace WindowsPackageManager.Interop;

internal sealed class ClassModel
{
    public required Type InterfaceType { get; init; }

    public required Type ProjectedClassType { get; init; }

    public required IReadOnlyDictionary<ClsidContext, Guid> Clsids { get; init; }

    public Guid GetClsid(ClsidContext context)
    {
        if (!Clsids.TryGetValue(context, out var clsid))
        {
            throw new InvalidOperationException($"{ProjectedClassType.FullName} is not implemented in context {context}");
        }

        return clsid;
    }

    public Guid GetIid()
    {
        return InterfaceType.GUID;
    }
}

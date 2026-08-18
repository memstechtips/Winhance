// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.InteropServices;
using WinRT;

namespace WindowsPackageManager.Interop;

// Manual COM activation so the objects live in an elevated context; must be called from an elevated process or
// the winget server rejects the connection. Based on the winget cmdlets' ComObjectFactory (github.com/microsoft/winget-cli).
public class WindowsPackageManagerElevatedFactory : WindowsPackageManagerFactory
{
    // The only CLSID context supported by the DLL we call is Prod.
    // If we want to use Dev classes we have to use a Dev version of the DLL.
    public WindowsPackageManagerElevatedFactory()
        : base(ClsidContext.Prod, allowLowerTrustRegistration: false)
    {
    }

    protected override unsafe T CreateInstance<T>(Guid clsid, Guid iid)
    {
        void* pUnknown = null;

        try
        {
            var hr = WinGetServerManualActivation_CreateInstance(in clsid, in iid, 0, out pUnknown);
            Marshal.ThrowExceptionForHR(hr);
            return MarshalInterface<T>.FromAbi((IntPtr)pUnknown);
        }
        finally
        {
            // CoCreateInstance and FromAbi both AddRef on the native object.
            // Release once to prevent memory leak.
            if (pUnknown is not null)
            {
                Marshal.Release((IntPtr)pUnknown);
            }
        }
    }

    [DllImport("winrtact.dll", ExactSpelling = true)]
    private static unsafe extern int WinGetServerManualActivation_CreateInstance(
        in Guid clsid,
        in Guid iid,
        uint flags,
        out void* instance);
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.System.Com;
using WinRT;

namespace WindowsPackageManager.Interop;

public class WindowsPackageManagerStandardFactory : WindowsPackageManagerFactory
{
    public WindowsPackageManagerStandardFactory(ClsidContext clsidContext = ClsidContext.Prod, bool allowLowerTrustRegistration = false)
        : base(clsidContext, allowLowerTrustRegistration)
    {
    }

    protected override T CreateInstance<T>(Guid clsid, Guid iid)
    {
        var pUnknown = IntPtr.Zero;
        try
        {
            // Use CLSCTX_LOCAL_SERVER — WinGet registers as an out-of-process COM server
            // Add CLSCTX_ALLOW_LOWER_TRUST_REGISTRATION for unpackaged apps running as admin
            // (required for self-contained AppSdk deployments, matches UniGetUI approach)
            CLSCTX clsctx = CLSCTX.CLSCTX_LOCAL_SERVER;
            if (_allowLowerTrustRegistration)
            {
                clsctx |= CLSCTX.CLSCTX_ALLOW_LOWER_TRUST_REGISTRATION;
            }

            var hr = PInvoke.CoCreateInstance(clsid, null, clsctx, iid, out var result);
            Marshal.ThrowExceptionForHR(hr);
            pUnknown = Marshal.GetIUnknownForObject(result);
            return MarshalGeneric<T>.FromAbi(pUnknown);
        }
        finally
        {
            // CoCreateInstance and FromAbi both AddRef on the native object.
            // Release once to prevent memory leak.
            if (pUnknown != IntPtr.Zero)
            {
                Marshal.Release(pUnknown);
            }
        }
    }
}

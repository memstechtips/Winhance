using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Winhance.Core.Features.Common.Native;

public static class MsiApi
{
    [DllImport("msi.dll", CharSet = CharSet.Unicode)]
    public static extern uint MsiOpenPackageEx(string szPackagePath, uint dwOptions, out IntPtr hProduct);

    [DllImport("msi.dll", CharSet = CharSet.Unicode)]
    public static extern uint MsiGetProductProperty(IntPtr hProduct, string szProperty, StringBuilder lpValueBuf, ref uint pcchValueBuf);

    [DllImport("msi.dll")]
    public static extern uint MsiCloseHandle(IntPtr hAny);
}

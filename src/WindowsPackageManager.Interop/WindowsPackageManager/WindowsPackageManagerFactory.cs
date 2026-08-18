// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Management.Deployment;

namespace WindowsPackageManager.Interop;

// Per-method details: winget-cli src/Microsoft.Management.Deployment/PackageManager.idl
public abstract class WindowsPackageManagerFactory
{
    private readonly ClsidContext _clsidContext;
    protected readonly bool _allowLowerTrustRegistration;

    public WindowsPackageManagerFactory(ClsidContext clsidContext, bool allowLowerTrustRegistration = false)
    {
        _clsidContext = clsidContext;
        _allowLowerTrustRegistration = allowLowerTrustRegistration;
    }

    protected abstract T CreateInstance<T>(Guid clsid, Guid iid);

    public PackageManager CreatePackageManager() => CreateInstance<PackageManager>();

    public FindPackagesOptions CreateFindPackagesOptions() => CreateInstance<FindPackagesOptions>();

    public CreateCompositePackageCatalogOptions CreateCreateCompositePackageCatalogOptions() => CreateInstance<CreateCompositePackageCatalogOptions>();

    public InstallOptions CreateInstallOptions() => CreateInstance<InstallOptions>();

    public UninstallOptions CreateUninstallOptions() => CreateInstance<UninstallOptions>();

    public PackageMatchFilter CreatePackageMatchFilter() => CreateInstance<PackageMatchFilter>();

    private T CreateInstance<T>()
    {
        var clsid = ClassesDefinition.GetClsid<T>(_clsidContext);
        var iid = ClassesDefinition.GetIid<T>();
        return CreateInstance<T>(clsid, iid);
    }
}

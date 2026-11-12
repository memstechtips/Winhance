namespace Winhance.Core.Features.Common.Interfaces;

public interface IPortableDataCopy
{
    // Returns the folders and files it copied, empty when this is not a portable copy or nothing was missing.
    IReadOnlyList<string> CopyInstalledData(string localAppData);
}

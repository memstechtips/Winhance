namespace Winhance.Core.Features.Common.Models
{
    public interface IInstallableItem
    {
        string PackageId { get; }
        string DisplayName { get; }
        InstallItemType ItemType { get; }
        bool IsInstalled { get; set; }
        bool RequiresRestart { get; }
    }

    public enum InstallItemType
    {
        WindowsApp,
        Capability,
        Feature,
        ThirdParty
    }
}

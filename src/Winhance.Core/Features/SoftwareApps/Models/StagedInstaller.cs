namespace Winhance.Core.Features.SoftwareApps.Models;

// AppId is null for an installer of the user's own, which follows the default install options.
public sealed record StagedInstaller(string Name, string Folder, string? AppId = null);

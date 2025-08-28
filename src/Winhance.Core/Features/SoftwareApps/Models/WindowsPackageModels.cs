using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Models.WindowsRegistry;
using Winhance.Core.Features.SoftwareApps.Enums;

namespace Winhance.Core.Features.SoftwareApps.Models;

public record WindowsPackage
{
    public required string Category { get; init; }
    public required string FriendlyName { get; init; }
    public required string PackageName { get; init; }
    public WindowsAppType AppType { get; init; }
    public IReadOnlyList<string>? SubPackages { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<RegistrySetting>? RegistrySettings { get; init; }
    public bool IsInstalled { get; init; }
}

public record LegacyCapability
{
    public required string FriendlyName { get; init; }
    public required string Name { get; init; }
    public bool IsInstalled { get; init; }
}

public record WindowsService
{
    public required string Name { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }
}

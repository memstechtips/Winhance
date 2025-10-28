namespace Winhance.Core.Features.Common.Models;

public record SettingDependency
{

    public SettingDependencyType DependencyType { get; init; }

    public required string DependentSettingId { get; init; }

    public required string RequiredSettingId { get; init; }

    public string? RequiredModule { get; init; }

    public string? RequiredValue { get; init; }

}

public enum SettingDependencyType
{
    RequiresEnabled,
    RequiresDisabled,
    RequiresSpecificValue,
    RequiresValueBeforeAnyChange
}

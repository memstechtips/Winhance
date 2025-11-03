namespace Winhance.Core.Features.Common.Models;

public class RegContentSetting
{
    public required string EnabledContent { get; init; }
    public required string DisabledContent { get; init; }
    public bool RequiresElevation { get; init; } = true;
}

namespace Winhance.Core.Features.Common.Models;

public record NativePowerApiSetting
{
    public int InformationLevel { get; init; }
    public byte EnabledValue { get; init; }
    public byte DisabledValue { get; init; }
}

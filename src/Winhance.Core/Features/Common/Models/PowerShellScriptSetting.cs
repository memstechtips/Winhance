namespace Winhance.Core.Features.Common.Models;

public class PowerShellScriptSetting
{
    public string? Id { get; init; }
    public string? Script { get; init; }
    public string? EnabledScript { get; init; }
    public string? DisabledScript { get; init; }
    public string? Purpose { get; init; }
    public bool RequiresElevation { get; init; } = true;
}

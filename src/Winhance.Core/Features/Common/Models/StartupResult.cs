namespace Winhance.Core.Features.Common.Models;

public sealed record StartupResult
{
    public bool IsFirstLaunch { get; init; }
}

using System.Collections.Generic;

namespace Winhance.Core.Features.Common.Models;

public record SettingStateResult
{
    public bool IsEnabled { get; init; }
    public object? CurrentValue { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyDictionary<string, object?>? RawValues { get; init; }
}

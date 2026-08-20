using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.Core.Features.Common.Models;

public sealed record SettingStateResult
{
    public bool IsEnabled { get; init; }
    public object? CurrentValue { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }

    // A parallel signal to IsEnabled, never a replacement for it. Independent of Success, which stays a
    // TRANSPORT-level flag: an Undetermined setting still reports Success = true, so existing consumers are unaffected.
    public SettingDetectionOutcome Outcome { get; init; } = SettingDetectionOutcome.Resolved;

    public string? OutcomeDetail { get; init; }

    public int? AcValue { get; init; }
    public int? DcValue { get; init; }

    public IReadOnlyList<DynamicOption>? DynamicOptions { get; init; }
    public string? DynamicSelection { get; init; }

    public string? DynamicSelectionName { get; init; }

    public IReadOnlyDictionary<string, object?>? Readings { get; init; }

    public IReadOnlyList<string>? DnsServers { get; init; }
}

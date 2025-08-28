using System;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.Core.Features.Common.Models.WindowsRegistry;

public record RegistrySetting
{
    public required string KeyPath { get; init; }
    public string? ValueName { get; init; }
    public required object RecommendedValue { get; init; }
    public object? DefaultValue { get; init; }
    public object? EnabledValue { get; init; }
    public object? DisabledValue { get; init; }
    public required RegistryValueKind ValueType { get; init; }
    public bool AbsenceMeansEnabled { get; init; } = false;
    public bool IsPrimary { get; init; } = false;
    public Dictionary<string, object>? CustomProperties { get; set; }
}

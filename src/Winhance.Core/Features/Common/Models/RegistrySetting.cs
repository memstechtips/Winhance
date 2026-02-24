using System.Collections.Generic;
using System;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.Core.Features.Common.Models;

public record RegistrySetting
{
    public required string KeyPath { get; init; }
    public string? ValueName { get; init; }
    public object? RecommendedValue { get; init; }
    public object? DefaultValue { get; init; }
    public object? EnabledValue { get; init; }
    public object? DisabledValue { get; init; }
    public required RegistryValueKind ValueType { get; init; }
    public bool IsPrimary { get; init; } = false;
    public IReadOnlyDictionary<string, object>? CustomProperties { get; init; }
    public int? BinaryByteIndex { get; init; }
    public bool ModifyByteOnly { get; init; } = false;
    public byte? BitMask { get; init; }
    public string? CompositeStringKey { get; init; }
    public bool ApplyPerNetworkInterface { get; init; } = false;
}

namespace Winhance.Core.Features.Common.Catalog;

/// <summary>Open tag-set (design §3). Today: WindowsDefault, Recommended. Future presets add values here
/// with zero schema change.</summary>
public enum RoleKind { None, WindowsDefault, Recommended }

/// <summary>A role tag on a state, scoped to a power context (Always for non-power settings).</summary>
public sealed record StateRole(RoleKind Kind, PowerContext Context = PowerContext.Always);

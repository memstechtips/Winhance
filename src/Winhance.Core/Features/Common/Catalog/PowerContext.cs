namespace Winhance.Core.Features.Common.Catalog;

/// <summary>The context a value/role applies to. Always = the single implicit context (~99% of settings).
/// AC/DC = battery-aware power settings.</summary>
public enum PowerContext { Always, AC, DC }

namespace Winhance.Core.Features.Common.Catalog;

// Presentation gate: the control is disabled unless OtherId is in one of States. Nesting under a UiParentId gates
// NOTHING on its own; a gate is a claim that this setting is meaningless while the other sits outside the listed
// states - a fact only the author knows, so it is declared here rather than guessed by the UI. Keyed on state
// LABEL, never index: saved configs persist the index, so state order is a public contract that may only be appended to.
public sealed record EnabledWhen(string OtherId, IReadOnlyList<string> States);

namespace Winhance.Core.Features.Common.Catalog;

/// <summary>Presentation gate: this setting's control is disabled unless <c>OtherId</c> is
/// currently in one of <c>States</c>. Null on a Setting = never gated.
///
/// Nesting under a <c>UiParentId</c> gates NOTHING on its own - a parent is a container; a gate is a
/// CLAIM that this setting is MEANINGLESS while the other one sits outside the listed states. Whether
/// that claim holds is a fact about Windows only the setting's author knows, so it is declared here
/// rather than guessed by the UI. (What it replaced: <c>SelectedValue is int index and index != 0</c>,
/// duplicated in two view-model methods, which greyed both Windows-theme sub-toggles on every stock
/// Windows 11 install because "Light Mode" happens to be state 0.)
///
/// Keyed on state LABEL, never on state index. The index is a storage detail - saved .winhance configs
/// persist it, so state order is a public contract that may only be APPENDED to - and keying a gate off
/// a position is exactly what made the old bug positional. A label that does not exist on the target is
/// a permanently-unsatisfiable gate, which is why CatalogValidator resolves every one of them.</summary>
public sealed record EnabledWhen(string OtherId, IReadOnlyList<string> States);

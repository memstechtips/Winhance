namespace Winhance.Core.Features.Common.Catalog;

/// <summary>Marks a setting whose selectable options are produced at runtime from the machine rather than authored
/// as static States (e.g. the installed power plans, which vary per PC). Replaces the old per-setting
/// LoadDynamicOptions flag and the SettingId-based special-casing. The concrete source enumerates the options and
/// reports the current selection when the engine is wired into the UI; that runtime contract is defined at the UI
/// cutover, alongside removing the now-redundant current-selection Detector.</summary>
public interface IDynamicOptionSource { }

/// <summary>Runtime-sources the machine's installed power plans as the power-plan selection's options (the active
/// scheme GUID is the stored selection). Supplants the old PowerPlanComboBoxService index/GUID round-tripping and the
/// `setting.Id == PowerPlanSelection` special-cases.</summary>
public sealed class PowerPlanOptionSource : IDynamicOptionSource { }

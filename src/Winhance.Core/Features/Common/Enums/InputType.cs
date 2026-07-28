namespace Winhance.Core.Features.Common.Enums;

public enum InputType
{
    Toggle,
    Selection,
    NumericRange,
    Action,

    /// <summary>
    /// NOT CURRENTLY PRODUCED BY ANY PATH (traced 2026-07-28). <c>ControlKind</c> has no CheckBox
    /// member, and all three <c>ControlToInputType</c> maps (SettingViewModelFactory,
    /// ConfigExportService, AutounattendXmlGeneratorService) fall through to <see cref="Toggle"/> -
    /// and SettingViewModelFactory is the only production builder of SettingItemViewModelConfig, so
    /// no view model can carry this value. The unreachable DataTemplate and template-selector arm
    /// were removed; a CheckBox view model would now render as a Toggle.
    ///
    /// Kept because <c>ConfigurationItem.InputType</c> is serialized into .winhance config files, so
    /// this is part of a persisted contract. It is also last, so removing it would not renumber the
    /// others - but there is no benefit to removing it either. Restore the template and the selector
    /// arm if a ControlKind.CheckBox is ever introduced; the toggle-like predicates that already
    /// name it (SettingItemViewModel, TechnicalDetailsManager) were deliberately left in place.
    /// </summary>
    CheckBox,
}

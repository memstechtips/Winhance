namespace Winhance.Core.Features.Common.Enums;

public enum ScriptOption
{
    Enabled,
    Disabled,

    /// <summary>
    /// The selected ComboBox option applies NO PowerShell script — used for a
    /// "leave it alone / Custom" dropdown choice that must touch nothing.
    /// Both SettingOperationExecutor and the autounattend builder skip the
    /// script entirely when an option's <see cref="ComboBoxOption.Script"/> is None.
    /// </summary>
    None,
}

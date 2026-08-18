namespace Winhance.Core.Features.Common.Enums;

public enum InputType
{
    Toggle,
    Selection,
    NumericRange,
    Action,

    // Not produced by any path. Kept only because ConfigurationItem.InputType is serialized into .winhance files;
    // a CheckBox item renders as a Toggle.
    CheckBox,
}

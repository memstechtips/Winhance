namespace Winhance.UI.Features.Common.Controls;

/// <summary>
/// Which power context a setting's input control edits. This is the second axis of the settings card:
/// a card is (control type) x (input mode), and before the shared controls existed that matrix was
/// flattened into ten hand-written DataTemplates - which is how the detection-outcome overlay ended up
/// on two of them and missing from the rest.
/// </summary>
public enum SettingInputMode
{
    /// <summary>One value, no AC/DC split. Registry settings and non-Separate powercfg settings.</summary>
    Single,

    /// <summary>The plugged-in value of a Separate-mode powercfg setting.</summary>
    Ac,

    /// <summary>The on-battery value of a Separate-mode powercfg setting.</summary>
    Dc,
}

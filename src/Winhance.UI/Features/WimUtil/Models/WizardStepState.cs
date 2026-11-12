using CommunityToolkit.Mvvm.ComponentModel;

namespace Winhance.UI.Features.WimUtil.Models;

public partial class WizardStepState : ObservableObject
{
    public int StepNumber { get; set; }

    [ObservableProperty]
    public partial FluentIcons.Common.Icon Icon { get; set; }

    // A Segoe Fluent Icons glyph, shown instead of Icon for a symbol the Fluent icon set lacks (it has no optical disc).
    public string? Glyph { get; set; }

    public bool HasGlyph => !string.IsNullOrEmpty(Glyph);

    public bool UsesFluentIcon => !HasGlyph;

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string AccessibleName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsCurrent { get; set; }

    public bool HasNextStep { get; set; }

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLocked))]
    public partial bool IsAvailable { get; set; }

    [ObservableProperty]
    public partial bool IsComplete { get; set; }

    public bool IsLocked => !IsAvailable;
}

using CommunityToolkit.Mvvm.ComponentModel;

namespace Winhance.UI.Features.WimUtil.Models;

public partial class WizardStepState : ObservableObject
{
    public int StepNumber { get; set; }

    [ObservableProperty]
    public partial FluentIcons.Common.Icon Icon { get; set; }

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
    public partial bool HasBeenExpanded { get; private set; }

    // Let the body load on first use without unloading it on later collapses.
    partial void OnIsExpandedChanged(bool value)
    {
        if (value)
            HasBeenExpanded = true;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLocked))]
    public partial bool IsAvailable { get; set; }

    [ObservableProperty]
    public partial bool IsComplete { get; set; }

    public bool IsLocked => !IsAvailable;
}

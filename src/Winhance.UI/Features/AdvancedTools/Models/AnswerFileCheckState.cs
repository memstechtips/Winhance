using CommunityToolkit.Mvvm.ComponentModel;
using Winhance.Core.Features.AdvancedTools.Models;

namespace Winhance.UI.Features.AdvancedTools.Models;

// Whichever wizard step last validated autounattend.xml publishes here; the step-2 banner
// renders it, and step 3 compares against it to stay silent when the drivers changed nothing.
public partial class AnswerFileCheckState : ObservableObject
{
    [ObservableProperty]
    public partial AnswerFileReport? LastReport { get; set; }
}

using CommunityToolkit.Mvvm.ComponentModel;
using Winhance.Core.Features.AdvancedTools.Models;

namespace Winhance.UI.Features.AdvancedTools.Models;

// The one place the answer-file verdict lives -- the step-2 header and the banner both read it.
// Subject names the file the report describes, so a refused file never becomes step 2's status.
public partial class AnswerFileCheckState : ObservableObject
{
    [ObservableProperty]
    public partial string? Subject { get; set; }

    [ObservableProperty]
    public partial AnswerFileReport? LastReport { get; set; }

    // Subject first, so a LastReport listener never reads it against the previous file's path.
    public void Publish(string? subject, AnswerFileReport? report)
    {
        Subject = subject;
        LastReport = report;
    }
}

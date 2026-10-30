using CommunityToolkit.Mvvm.ComponentModel;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.WimUtil.Interfaces;
using Winhance.Core.Features.WimUtil.Models;

namespace Winhance.UI.Features.WimUtil.Models;

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

    // Adding drivers or the update rewrites autounattend.xml, so the copy on the media is re-checked once the
    // progress task is over (the task-output dialog would otherwise attach to a running task).
    // Step 2 already showed these findings; the dialog returns only when the change altered them.
    public async Task RecheckAfterChangeAsync(
        string answerFile,
        IAnswerFileValidator validator,
        IDialogService dialogService,
        ILocalizationService localizationService,
        ILogService logService)
    {
        try
        {
            var previous = LastReport;
            var report = await validator.ValidateAsync(answerFile);
            Publish(answerFile, report);
            if (report.Findings.Count > 0 && !SameFindings(report, previous))
            {
                await dialogService.ShowSummaryAsync(AnswerFileReportDialog.Request(report, localizationService));
            }
        }
        catch (Exception ex)
        {
            logService.LogWarning($"Could not check autounattend.xml: {ex.Message}");
        }
    }

    // A step shifts line numbers below its insertion point, so sameness ignores Location.
    private static bool SameFindings(AnswerFileReport report, AnswerFileReport? previous) =>
        previous is not null
        && report.Findings.Select(f => (f.Rule, f.Severity, f.Detail))
            .SequenceEqual(previous.Findings.Select(f => (f.Rule, f.Severity, f.Detail)));
}

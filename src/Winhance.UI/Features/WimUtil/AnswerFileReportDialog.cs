using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.WimUtil.Models;

namespace Winhance.UI.Features.WimUtil;

// Nothing here decides anything: the verdict and every finding are shown, the user chooses.
public static class AnswerFileReportDialog
{
    public static string Verdict(AnswerFileReport report, ILocalizationService localization) =>
        localization.GetString(report.Verdict switch
        {
            AnswerFileVerdict.WillFail => "WIMUtil_AnswerFile_Verdict_WillFail",
            AnswerFileVerdict.MayFail => "WIMUtil_AnswerFile_Verdict_MayFail",
            _ => "WIMUtil_AnswerFile_Verdict_Clean",
        });

    public static string Summary(AnswerFileReport report, ILocalizationService localization) =>
        localization.GetString(
            "WIMUtil_AnswerFile_Summary",
            report.Findings.Count(f => f.Severity == AnswerFileSeverity.Error),
            report.Findings.Count(f => f.Severity == AnswerFileSeverity.Warning));

    // Severity and rule name are localized; the location and the parser's own text stay verbatim.
    // The rule key is built from the enum name, which the literal-key gate cannot see.
    public static IReadOnlyList<string> Items(AnswerFileReport report, ILocalizationService localization) =>
        report.Findings
            .Select(f =>
            {
                var ruleKey = "WIMUtil_AnswerFile_Rule_" + f.Rule;
                return localization.GetString(f.Severity == AnswerFileSeverity.Error ? "Dialog_Error" : "Dialog_Warning")
                    + ": " + localization.GetString(ruleKey)
                    + Environment.NewLine + f.Location
                    + Environment.NewLine + f.Detail;
            })
            .ToList();

    public static IReadOnlyList<string> Lines(AnswerFileReport report, ILocalizationService localization)
    {
        // A clean file needs no counts line: "Errors: 0" still reads as a failure, and the terminal colours it red.
        var lines = new List<string> { Verdict(report, localization) };
        if (report.Findings.Count > 0)
            lines.Add(Summary(report, localization));

        foreach (var item in Items(report, localization))
        {
            lines.Add(string.Empty);
            lines.AddRange(item.Split(Environment.NewLine));
        }

        return lines;
    }

    public static SummaryVerdict SummaryVerdict(AnswerFileReport report, ILocalizationService localization) =>
        new(report.Verdict switch
            {
                AnswerFileVerdict.WillFail => SummaryTone.Error,
                AnswerFileVerdict.MayFail => SummaryTone.Warning,
                _ => SummaryTone.Success,
            },
            Verdict(report, localization),
            report.Findings.Count > 0 ? Summary(report, localization) : null);

    public static SummarySection FindingsSection(AnswerFileReport report, ILocalizationService localization) =>
        new(localization.GetString("WIMUtil_AnswerFile_FindingsHeading"),
            report.Findings
                .Select(f =>
                {
                    var ruleKey = "WIMUtil_AnswerFile_Rule_" + f.Rule;
                    return new SummaryRow(
                        SummaryIcon.Status,
                        localization.GetString(ruleKey),
                        f.Detail,
                        f.Location,
                        f.Severity == AnswerFileSeverity.Error ? SummaryTone.Error : SummaryTone.Warning);
                })
                .ToList());

    public static SummaryRequest Request(AnswerFileReport report, ILocalizationService localization) => new()
    {
        Title = localization.GetString("WIMUtil_AnswerFile_DialogTitle"),
        Verdict = SummaryVerdict(report, localization),
        Sections = report.Findings.Count > 0 ? [FindingsSection(report, localization)] : [],
        CopyText = string.Join(Environment.NewLine, Lines(report, localization)),
    };
}

using Winhance.Core.Features.AdvancedTools.Models;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.UI.Features.AdvancedTools;

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
    // The rule key is built from the enum name, which the literal-key gate cannot see; a test pins
    // one key per rule instead.
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
        var lines = new List<string> { Verdict(report, localization), Summary(report, localization) };
        foreach (var item in Items(report, localization))
        {
            lines.Add(string.Empty);
            lines.AddRange(item.Split(Environment.NewLine));
        }

        return lines;
    }

}

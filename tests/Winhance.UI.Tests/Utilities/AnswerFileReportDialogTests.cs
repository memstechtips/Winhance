using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.WimUtil.Models;
using Winhance.UI.Features.WimUtil;
using Xunit;

namespace Winhance.UI.Tests.Utilities;

public class AnswerFileReportDialogTests
{
    private static readonly AnswerFileFinding Error =
        new(AnswerFileRule.CommandEmpty, AnswerFileSeverity.Error, "line 3: settings[specialize]", "Path");

    private static readonly AnswerFileFinding Warning =
        new(AnswerFileRule.OrderDuplicate, AnswerFileSeverity.Warning, "line 9: settings[specialize]", "5");

    private readonly Mock<ILocalizationService> _localization = new();

    public AnswerFileReportDialogTests()
    {
        _localization.Setup(l => l.GetString(It.IsAny<string>())).Returns((string key) => key);
        _localization
            .Setup(l => l.GetString(It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns((string key, object[] args) => key + ":" + string.Join(",", args));
    }

    [Fact]
    public void Verdict_MapsEachLevelToItsKey()
    {
        AnswerFileReportDialog.Verdict(new AnswerFileReport([]), _localization.Object).Should().Be("WIMUtil_AnswerFile_Verdict_Clean");
        AnswerFileReportDialog.Verdict(new AnswerFileReport([Warning]), _localization.Object).Should().Be("WIMUtil_AnswerFile_Verdict_MayFail");
        AnswerFileReportDialog.Verdict(new AnswerFileReport([Warning, Error]), _localization.Object).Should().Be("WIMUtil_AnswerFile_Verdict_WillFail");
    }

    [Fact]
    public void Summary_CountsErrorsThenWarnings()
    {
        AnswerFileReportDialog.Summary(new AnswerFileReport([Warning, Error, Error]), _localization.Object)
            .Should().Be("WIMUtil_AnswerFile_Summary:2,1");
    }

    [Fact]
    public void Items_CarrySeverityRuleLocationAndDetail()
    {
        var items = AnswerFileReportDialog.Items(new AnswerFileReport([Error, Warning]), _localization.Object);

        items.Should().Equal(
            "Dialog_Error: WIMUtil_AnswerFile_Rule_CommandEmpty" + Environment.NewLine + "line 3: settings[specialize]" + Environment.NewLine + "Path",
            "Dialog_Warning: WIMUtil_AnswerFile_Rule_OrderDuplicate" + Environment.NewLine + "line 9: settings[specialize]" + Environment.NewLine + "5");
    }

    [Fact]
    public void Lines_AreTheItemsSplitWithABlankSeparator()
    {
        var lines = AnswerFileReportDialog.Lines(new AnswerFileReport([Error, Warning]), _localization.Object);

        lines.Should().Equal(
            "WIMUtil_AnswerFile_Verdict_WillFail",
            "WIMUtil_AnswerFile_Summary:1,1",
            "",
            "Dialog_Error: WIMUtil_AnswerFile_Rule_CommandEmpty", "line 3: settings[specialize]", "Path",
            "",
            "Dialog_Warning: WIMUtil_AnswerFile_Rule_OrderDuplicate", "line 9: settings[specialize]", "5");
    }

    [Fact]
    public void Lines_ForACleanFile_AreOnlyTheVerdict()
    {
        AnswerFileReportDialog.Lines(new AnswerFileReport([]), _localization.Object)
            .Should().Equal("WIMUtil_AnswerFile_Verdict_Clean");
    }

    [Fact]
    public void SummaryVerdict_ColoursEachLevelAndCountsOnlyWhenThereAreFindings()
    {
        AnswerFileReportDialog.SummaryVerdict(new AnswerFileReport([]), _localization.Object)
            .Should().Be(new SummaryVerdict(SummaryTone.Success, "WIMUtil_AnswerFile_Verdict_Clean"));
        AnswerFileReportDialog.SummaryVerdict(new AnswerFileReport([Warning]), _localization.Object)
            .Should().Be(new SummaryVerdict(SummaryTone.Warning, "WIMUtil_AnswerFile_Verdict_MayFail", "WIMUtil_AnswerFile_Summary:0,1"));
        AnswerFileReportDialog.SummaryVerdict(new AnswerFileReport([Warning, Error]), _localization.Object).Tone
            .Should().Be(SummaryTone.Error);
    }

    [Fact]
    public void FindingsSection_RowsCarryRuleDetailLocationAndSeverity()
    {
        var section = AnswerFileReportDialog.FindingsSection(new AnswerFileReport([Error, Warning]), _localization.Object);

        section.Heading.Should().Be("WIMUtil_AnswerFile_FindingsHeading");
        section.Rows.Should().Equal(
            new SummaryRow(SummaryIcon.Status, "WIMUtil_AnswerFile_Rule_CommandEmpty", "Path", "line 3: settings[specialize]", SummaryTone.Error),
            new SummaryRow(SummaryIcon.Status, "WIMUtil_AnswerFile_Rule_OrderDuplicate", "5", "line 9: settings[specialize]", SummaryTone.Warning));
    }

    [Fact]
    public void Request_ForACleanFile_HasTheVerdictAndNoFindings()
    {
        var request = AnswerFileReportDialog.Request(new AnswerFileReport([]), _localization.Object);

        request.Title.Should().Be("WIMUtil_AnswerFile_DialogTitle");
        request.Sections.Should().BeEmpty();
        request.ConfirmButtonText.Should().BeEmpty();
        request.CopyText.Should().Be("WIMUtil_AnswerFile_Verdict_Clean");
    }
}

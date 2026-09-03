using FluentAssertions;
using Moq;
using Winhance.Core.Features.AdvancedTools.Models;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.AdvancedTools;
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

}

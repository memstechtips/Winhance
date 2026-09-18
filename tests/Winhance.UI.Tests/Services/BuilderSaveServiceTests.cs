using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Selections;
using Winhance.Core.Features.WimUtil.Interfaces;
using Winhance.Core.Features.WimUtil.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Services;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class BuilderSaveServiceTests
{
    private const string SavedPath = @"C:\Users\Test\Winhance_Config_20260819.winhance";

    private readonly Mock<ISelectionSetBuilder> _selections = new();
    private readonly Mock<ISelectionSaveService> _saves = new();
    private readonly Mock<IAnswerFileValidator> _validator = new();
    private readonly Mock<IDialogService> _dialogs = new();
    private readonly Mock<ILocalizationService> _loc = new();
    private readonly Mock<ILogService> _log = new();

    public BuilderSaveServiceTests()
    {
        _loc.Setup(l => l.GetString(It.IsAny<string>())).Returns((string key) => key);
        _loc.Setup(l => l.GetString(It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns((string key, object[] args) => string.Format(key, args));

        _selections.Setup(s => s.FromBuilderSessionAsync()).ReturnsAsync(SelectionSet.Empty);
        _saves
            .Setup(s => s.SaveAsync(It.IsAny<BuilderTarget>(), It.IsAny<SelectionSet>(), It.IsAny<SelectionSaveOptions>()))
            .ReturnsAsync(SavedPath);
        _validator
            .Setup(v => v.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnswerFileReport([]));
    }

    private BuilderSaveService Sut() => new(
        _selections.Object,
        _saves.Object,
        _validator.Object,
        _dialogs.Object,
        _loc.Object,
        _log.Object);

    [Theory]
    [InlineData(BuilderTarget.Config)]
    [InlineData(BuilderTarget.Autounattend)]
    public async Task Save_HandsTheBuilderSessionToTheSaveService(BuilderTarget target)
    {
        await Sut().SaveAsync(target);

        _selections.Verify(s => s.FromBuilderSessionAsync(), Times.Once);
        _selections.Verify(s => s.FromMachineAsync(), Times.Never);
        _saves.Verify(s => s.SaveAsync(target, SelectionSet.Empty, null), Times.Once);
    }

    [Fact]
    public async Task SaveThrows_ShowsErrorDialog()
    {
        _saves
            .Setup(s => s.SaveAsync(It.IsAny<BuilderTarget>(), It.IsAny<SelectionSet>(), It.IsAny<SelectionSaveOptions>()))
            .ThrowsAsync(new IOException("disk full"));

        await Sut().SaveAsync(BuilderTarget.Config);

        _dialogs.Verify(d => d.ShowErrorAsync("Config_Export_Error_Message", "Config_Export_Error_Title", ""), Times.Once);
        _dialogs.Verify(d => d.ShowInformationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SaveAutounattend_WithFindings_ShowsTheReport()
    {
        _validator
            .Setup(v => v.ValidateAsync(SavedPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnswerFileReport(
            [
                new AnswerFileFinding(AnswerFileRule.DuplicateAccountName, AnswerFileSeverity.Error, "line 12: LocalAccount", "Marco"),
            ]));

        await Sut().SaveAsync(BuilderTarget.Autounattend);

        _dialogs.Verify(d => d.ShowTaskOutputDialogAsync(
            "WIMUtil_AnswerFile_DialogTitle",
            It.Is<IReadOnlyList<string>>(l => l.Contains("WIMUtil_AnswerFile_Verdict_WillFail")
                && l.Contains("Dialog_Error: WIMUtil_AnswerFile_Rule_DuplicateAccountName")
                && l.Contains("Marco"))), Times.Once);
    }

    [Fact]
    public async Task SaveAutounattend_CleanFile_ShowsNoReport()
    {
        await Sut().SaveAsync(BuilderTarget.Autounattend);

        _validator.Verify(v => v.ValidateAsync(SavedPath, It.IsAny<CancellationToken>()), Times.Once);
        _dialogs.Verify(d => d.ShowTaskOutputDialogAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>()), Times.Never);
    }

    [Fact]
    public async Task SaveConfig_NeverChecksTheFile()
    {
        await Sut().SaveAsync(BuilderTarget.Config);

        _validator.Verify(v => v.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SaveCancelled_NeverChecksTheFile()
    {
        _saves
            .Setup(s => s.SaveAsync(It.IsAny<BuilderTarget>(), It.IsAny<SelectionSet>(), It.IsAny<SelectionSaveOptions>()))
            .ReturnsAsync((string?)null);

        await Sut().SaveAsync(BuilderTarget.Autounattend);

        _validator.Verify(v => v.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CheckThrows_ShowsNoErrorDialog()
    {
        _validator
            .Setup(v => v.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("file locked"));

        await Sut().SaveAsync(BuilderTarget.Autounattend);

        _dialogs.Verify(d => d.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}

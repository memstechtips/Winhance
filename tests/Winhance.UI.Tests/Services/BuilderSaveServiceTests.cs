using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Common.Services;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class BuilderSaveServiceTests
{
    private const string SavedPath = @"C:\Users\Test\Winhance_Config_20260819.winhance";

    private readonly Mock<ISelectionSetBuilder> _selections = new();
    private readonly Mock<ISelectionSaveService> _saves = new();
    private readonly Mock<IDialogService> _dialogs = new();
    private readonly Mock<ILocalizationService> _loc = new();
    private readonly Mock<ILogService> _log = new();
    private readonly Mock<IProfileService> _profiles = new();
    private readonly Mock<IBuilderModeEntry> _entry = new();

    public BuilderSaveServiceTests()
    {
        _loc.Setup(l => l.GetString(It.IsAny<string>())).Returns((string key) => key);
        _loc.Setup(l => l.GetString(It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns((string key, object[] args) => string.Format(key, args));

        _selections.Setup(s => s.FromBuilderSessionAsync()).ReturnsAsync(SelectionSet.Empty);
        _saves
            .Setup(s => s.SaveAsync(It.IsAny<BuilderTarget>(), It.IsAny<SelectionSet>(), It.IsAny<SelectionSaveOptions>()))
            .ReturnsAsync(new SelectionSaveResult(SavedPath, []));
    }

    private BuilderSaveService Sut() => new(
        _selections.Object,
        _saves.Object,
        _dialogs.Object,
        _loc.Object,
        _log.Object,
        _profiles.Object,
        _entry.Object);

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
    public async Task SaveAsProfile_SavesTheSessionsSettingsUnderTheNameGiven()
    {
        var settings = new[] { new SettingChoice("privacy-advertising-id", new ChoiceValue.Toggle(false)) };
        _selections.Setup(s => s.FromBuilderSessionAsync()).ReturnsAsync(new SelectionSet(settings, [], []));
        _dialogs.Setup(d => d.AskTextAsync(It.IsAny<TextInputRequest>())).ReturnsAsync("Gaming");
        _profiles.Setup(p => p.SaveAsync("Gaming", settings)).ReturnsAsync(new SettingsProfile("Gaming", "Gaming", ProfileKind.User, @"C:\p\Gaming.winhance"));

        await Sut().SaveAsProfileAsync();

        _profiles.Verify(p => p.SaveAsync("Gaming", settings), Times.Once);
        _dialogs.Verify(d => d.ShowInformationAsync("Builder_SaveProfile_Saved", "Builder_SaveProfile_Title", ""), Times.Once);
    }

    [Fact]
    public async Task SaveAsProfile_Cancelled_SavesNothing()
    {
        _dialogs.Setup(d => d.AskTextAsync(It.IsAny<TextInputRequest>())).ReturnsAsync((string?)null);

        await Sut().SaveAsProfileAsync();

        _profiles.Verify(p => p.SaveAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<SettingChoice>>()), Times.Never);
    }

    [Fact]
    public async Task SaveAsProfile_OffersTheEditedProfilesOwnName_AndNoOtherTakenOne()
    {
        TextInputRequest? asked = null;
        _entry.Setup(e => e.EditingProfile).Returns(new SettingsProfile("Gaming", "Gaming", ProfileKind.User, @"C:\p\Gaming.winhance"));
        _profiles.Setup(p => p.IsNameAvailable(It.IsAny<string>())).Returns(false);
        _dialogs.Setup(d => d.AskTextAsync(It.IsAny<TextInputRequest>())).Callback((TextInputRequest r) => asked = r).ReturnsAsync((string?)null);

        await Sut().SaveAsProfileAsync();

        asked!.Text.Should().Be("Gaming");
        asked.IsValid("Gaming").Should().BeTrue();
        asked.IsValid("Work").Should().BeFalse();
    }

    [Fact]
    public async Task SaveAsProfile_FromABuiltInProfile_StartsWithNoName()
    {
        TextInputRequest? asked = null;
        _entry.Setup(e => e.EditingProfile).Returns(new SettingsProfile("builtin:recommended", "Recommended", ProfileKind.Recommended, null));
        _dialogs.Setup(d => d.AskTextAsync(It.IsAny<TextInputRequest>())).Callback((TextInputRequest r) => asked = r).ReturnsAsync((string?)null);

        await Sut().SaveAsProfileAsync();

        asked!.Text.Should().BeEmpty();
        asked.IsValid("Recommended").Should().BeFalse();
    }
}

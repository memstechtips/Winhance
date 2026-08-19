using Moq;
using Winhance.Core.Features.AdvancedTools.Interfaces;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Services;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class BuilderSaveServiceTests
{
    private const string ConfigPath = @"C:\Users\Test\Winhance_Config_20260819.winhance";
    private const string XmlPath = @"C:\Users\Test\autounattend.xml";

    private readonly Mock<ISelectionSetBuilder> _selections = new();
    private readonly Mock<IConfigFileWriter> _configFiles = new();
    private readonly Mock<IAutounattendXmlGeneratorService> _generator = new();
    private readonly Mock<ICatalogSettingsRegistry> _registry = new();
    private readonly Mock<ISaveFilePicker> _picker = new();
    private readonly Mock<IDialogService> _dialogs = new();
    private readonly Mock<ILocalizationService> _loc = new();
    private readonly Mock<ILogService> _log = new();

    public BuilderSaveServiceTests()
    {
        _loc.Setup(l => l.GetString(It.IsAny<string>())).Returns((string key) => key);
        _loc.Setup(l => l.GetString(It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns((string key, object[] args) => string.Format(key, args));

        _selections.Setup(s => s.FromBuilderSessionAsync()).ReturnsAsync(SelectionSet.Empty);
        _selections.Setup(s => s.CurrentScope).Returns(CatalogScope.CurrentMachine);

        _registry.Setup(r => r.GetAll(It.IsAny<bool>()))
            .Returns(new Dictionary<string, IReadOnlyList<Setting>>());
    }

    private BuilderSaveService Sut() => new(
        _selections.Object,
        _configFiles.Object,
        _generator.Object,
        _registry.Object,
        _picker.Object,
        _dialogs.Object,
        _loc.Object,
        _log.Object);

    private void ArrangePicker(string? path) =>
        _picker
            .Setup(p => p.PickSavePath(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(path);

    [Fact]
    public async Task Config_UsesBuilderSession_PickerAndFileWriter()
    {
        ArrangePicker(ConfigPath);

        await Sut().SaveAsync(BuilderTarget.Config);

        _selections.Verify(s => s.FromBuilderSessionAsync(), Times.Once);
        _configFiles.Verify(w => w.WriteAsync(SelectionSet.Empty, CatalogScope.CurrentMachine, ConfigPath), Times.Once);
        _generator.Verify(g => g.GenerateFromConfigAsync(It.IsAny<WinhanceConfigFile>(), It.IsAny<string>()), Times.Never);
        _dialogs.Verify(d => d.ShowInformationAsync("Config_Export_Success_Message", "Config_Export_Success_Title", ""), Times.Once);
    }

    [Fact]
    public async Task Autounattend_UsesBuilderSession_PickerAndGenerator()
    {
        ArrangePicker(XmlPath);

        await Sut().SaveAsync(BuilderTarget.Autounattend);

        _selections.Verify(s => s.FromBuilderSessionAsync(), Times.Once);
        _generator.Verify(g => g.GenerateFromConfigAsync(It.IsAny<WinhanceConfigFile>(), XmlPath), Times.Once);
        _configFiles.Verify(w => w.WriteAsync(It.IsAny<SelectionSet>(), It.IsAny<CatalogScope>(), It.IsAny<string>()), Times.Never);
        _dialogs.Verify(d => d.ShowInformationAsync("Config_Export_Success_Message", "Config_Export_Success_Title", ""), Times.Once);
    }

    [Theory]
    [InlineData(BuilderTarget.Config)]
    [InlineData(BuilderTarget.Autounattend)]
    public async Task Cancel_WritesNothing(BuilderTarget target)
    {
        ArrangePicker(null);

        await Sut().SaveAsync(target);

        _configFiles.Verify(w => w.WriteAsync(It.IsAny<SelectionSet>(), It.IsAny<CatalogScope>(), It.IsAny<string>()), Times.Never);
        _generator.Verify(g => g.GenerateFromConfigAsync(It.IsAny<WinhanceConfigFile>(), It.IsAny<string>()), Times.Never);
        _dialogs.Verify(d => d.ShowInformationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _dialogs.Verify(d => d.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task WriterThrows_ShowsErrorDialog()
    {
        ArrangePicker(ConfigPath);
        _configFiles
            .Setup(w => w.WriteAsync(It.IsAny<SelectionSet>(), It.IsAny<CatalogScope>(), It.IsAny<string>()))
            .ThrowsAsync(new IOException("disk full"));

        await Sut().SaveAsync(BuilderTarget.Config);

        _dialogs.Verify(d => d.ShowErrorAsync("Config_Export_Error_Message", "Config_Export_Error_Title", ""), Times.Once);
        _dialogs.Verify(d => d.ShowInformationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}

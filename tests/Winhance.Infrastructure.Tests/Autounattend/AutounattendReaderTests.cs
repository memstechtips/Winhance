using System.Text.RegularExpressions;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Autounattend.Interfaces;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
using Winhance.Core.Features.SoftwareApps.Catalogs;
using System.Xml.Linq;
using Winhance.Infrastructure.Features.Autounattend;
using Winhance.Infrastructure.Features.Autounattend.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Autounattend;

public class AutounattendReaderTests
{
    private const string FilePath = @"C:\Media\autounattend.xml";

    private readonly Mock<ICatalogSettingsRegistry> _registry = new();
    private readonly Mock<IWinhancementsScriptBuilder> _scripts = new();
    private readonly Mock<IFileSystemService> _files = new();
    private readonly Mock<IVersionService> _version = new();
    private readonly Mock<IConfigMigrationService> _migration = new();
    private readonly Mock<ILogService> _log = new();
    private string _written = string.Empty;

    public AutounattendReaderTests()
    {
        _registry.Setup(r => r.GetAll(It.IsAny<CatalogScope>())).Returns(ParityCatalog.ByFeature);
        _scripts.Setup(b => b.BuildAsync(It.IsAny<SelectionSet>(), It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<Setting>>>(), It.IsAny<WinBuild?>()))
            .ReturnsAsync("Write-Host 'Winhancements'");
        _version.Setup(v => v.GetCurrentVersion()).Returns(new VersionInfo { Version = "26.09.23" });
        _files.Setup(f => f.WriteAllTextAsync(FilePath, It.IsAny<string>(), default))
            .Callback<string, string, CancellationToken>((_, contents, _) => _written = contents)
            .Returns(Task.CompletedTask);
        _files.Setup(f => f.ReadAllTextAsync(FilePath, default)).ReturnsAsync(() => _written);
    }

    private AutounattendReader Sut() => new(_files.Object, _migration.Object, _log.Object);

    private Task WriteAsync(SelectionSet set) =>
        new AutounattendWriter(_registry.Object, _scripts.Object, _files.Object, _version.Object, _log.Object)
            .WriteAsync(set, CatalogScope.CurrentMachine, FilePath);

    [Fact]
    public async Task ReadAsync_WithAFileWinhanceWrote_ReadsBackTheChoicesItWasWrittenFrom()
    {
        var choice = new SettingChoice("parity-toggle-hklm", new ChoiceValue.Toggle(false));
        await WriteAsync(new SelectionSet([choice], [new AppChoice("app", "App", null, "App.Capability", null, null)], []));

        var read = await Sut().ReadAsync(FilePath);

        read!.Version.Should().Be("26.09.23");
        var set = ConfigFileMapper.FromFile(read.Config!, ParityCatalog.ByFeature);
        set.Settings.Should().Equal(choice);
        set.WindowsApps.Select(a => a.Id).Should().Equal("app");
        _migration.Verify(m => m.MigrateConfig(read.Config!), Times.Once);
    }

    // A choice the reopen drops is lost on the next Generate, so every answer-file card has to come back as written.
    [Fact]
    public async Task ReadAsync_WithEveryAnswerFileCard_ReadsBackTheChoiceItWasWrittenFrom()
    {
        _registry.Setup(r => r.GetAll(It.IsAny<CatalogScope>())).Returns(SettingCatalog.ByFeature);
        var written = SettingCatalog.All.Where(s => s.IsAnswerFileOnly)
            .Select(s => new SettingChoice(s.Id, SampleFor(s)))
            .ToList();
        await WriteAsync(new SelectionSet(written, [], []));

        var read = await Sut().ReadAsync(FilePath);

        var back = ConfigFileMapper.FromFile(read!.Config!, SettingCatalog.ByFeature).Settings
            .ToDictionary(c => c.SettingId, c => c.Value);
        foreach (var choice in written)
        {
            // Setup reads the passwords from the same file, so the reopen copy keeps them too.
            var expected = choice.Value is ChoiceValue.List list ? list with { SavePasswords = true } : choice.Value;
            back.Should().ContainKey(choice.SettingId).WhoseValue.Should().Be(expected, choice.SettingId);
        }
    }

    [Fact]
    public async Task ReadAsync_WithACarriedPicture_ReadsBackItsBytes()
    {
        _registry.Setup(r => r.GetAll(It.IsAny<CatalogScope>())).Returns(SettingCatalog.ByFeature);
        var picture = SettingCatalog.All.First(s => s.Options?.Source == OptionSource.Pictures);
        const string destination = @"C:\Windows\Web\Wallpaper\Winhance\beach.jpg";
        await WriteAsync(new SelectionSet([new SettingChoice(picture.Id, new ChoiceValue.Keyed(destination, "beach"))], [], [])
        {
            Files = [new CarriedFile(picture.Id, destination, "AAAA")],
        });

        var read = await Sut().ReadAsync(FilePath);

        ConfigFileMapper.FromFile(read!.Config!, SettingCatalog.ByFeature).Files
            .Should().Equal(new CarriedFile(picture.Id, destination, "AAAA"));
    }

    [Theory]
    [InlineData(@"C:\Wallpapers\Winhance", @"C:\Media\sources\$OEM$\$1\Wallpapers\Winhance")]
    [InlineData(@"C:\Windows\Web\Wallpaper\Winhance", @"C:\Media\sources\$OEM$\$$\Web\Wallpaper\Winhance")]
    public async Task MediaFile_WritesRawAssetsAndReopensThemForAStandaloneSave(string destination, string staged)
    {
        const string mediaFolder = @"C:\Media";
        string picture = destination + @"\beach.jpg";
        string album = destination + @"\Holiday";
        var carried = new CarriedFile[]
        {
            new("theme-wallpaper-picture", picture, "AQID"),
            new("theme-wallpaper-album", album + @"\one.jpg", "BAUG"),
            new("theme-wallpaper-album", album + @"\two.png", "BwgJ"),
        };
        var onMedia = ArrangeMedia();
        _registry.Setup(r => r.GetAll(It.IsAny<CatalogScope>())).Returns(SettingCatalog.ByFeature);
        var set = new SelectionSet([
            new SettingChoice("theme-wallpaper-picture", new ChoiceValue.Keyed(picture, "beach")),
            new SettingChoice("theme-wallpaper-album", new ChoiceValue.Text(album)),
        ], [], []) { Files = carried };
        var writer = new AutounattendWriter(_registry.Object, _scripts.Object, _files.Object, _version.Object, _log.Object);
        await writer.WriteMediaAsync(set, CatalogScope.CurrentMachine, FilePath, mediaFolder, _ => Task.CompletedTask, CancellationToken.None);

        var extensions = XDocument.Parse(_written).Root!.Element(AutounattendDocument.WinhanceExtensions + "Extensions")!;
        extensions.Elements(AutounattendDocument.WinhanceExtensions + "File").Should().BeEmpty();
        extensions.Elements(AutounattendDocument.WinhanceExtensions + "StagedFile").Should().HaveCount(4);
        onMedia[staged + @"\beach.jpg"].Should().Equal(1, 2, 3);
        onMedia[staged + @"\Holiday\one.jpg"].Should().Equal(4, 5, 6);
        onMedia[@"C:\Media\sources\$OEM$\$1\ProgramData\Winhance\Unattend\Scripts\Winhancements.ps1"].Should().StartWith(new byte[] { 0xef, 0xbb, 0xbf });

        var read = await Sut().ReadAsync(FilePath);
        var reopened = ConfigFileMapper.FromFile(read!.Config!, SettingCatalog.ByFeature);
        reopened.Files.Should().BeEquivalentTo(carried);

        string staleSlide = staged + @"\Holiday\two.png";
        string unrelatedSlide = staged + @"\Holiday\user.jpg";
        onMedia[unrelatedSlide] = [10];

        await writer.WriteMediaAsync(set with { Files = carried[..2] }, CatalogScope.CurrentMachine, FilePath, mediaFolder, _ => Task.CompletedTask, CancellationToken.None);
        onMedia.Should().NotContainKey(staleSlide);
        onMedia.Should().ContainKey(unrelatedSlide);

        await writer.WriteAsync(reopened, CatalogScope.CurrentMachine, FilePath);
        XDocument.Parse(_written).Root!.Element(AutounattendDocument.WinhanceExtensions + "Extensions")!
            .Elements(AutounattendDocument.WinhanceExtensions + "File").Should().HaveCount(2);
        _written.Should().NotContain("BAUG").And.NotContain("BwgJ");
        var standalone = ConfigFileMapper.FromFile((await Sut().ReadAsync(FilePath))!.Config!, SettingCatalog.ByFeature);
        standalone.Files.Should().Equal(carried[0]);
        standalone.Value("theme-wallpaper-album").Should().Be(new ChoiceValue.Text(album));
    }

    [Fact]
    public async Task MediaFile_DifferingUnownedPayload_BlocksBeforePublication()
    {
        const string path = @"C:\Media\sources\$OEM$\$1\ProgramData\Winhance\Unattend\Scripts\Winhancements.ps1";
        var media = ArrangeMedia();
        media[path] = [1];
        var writer = new AutounattendWriter(_registry.Object, _scripts.Object, _files.Object, _version.Object, _log.Object);

        Func<Task> save = () => writer.WriteMediaAsync(SelectionSet.Empty, CatalogScope.CurrentMachine, FilePath, @"C:\Media", _ => Task.CompletedTask, CancellationToken.None);
        await save.Should().ThrowAsync<IOException>().WithMessage("*not owned*");
        media.Should().ContainSingle().Which.Value.Should().Equal(1);
    }

    private Dictionary<string, byte[]> ArrangeMedia()
    {
        var media = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        _files.Setup(f => f.FileExists(It.IsAny<string>())).Returns((string path) => media.ContainsKey(path));
        _files.Setup(f => f.OpenRead(It.IsAny<string>())).Returns((string path) => new MemoryStream(media[path]));
        _files.Setup(f => f.ReadAllBytesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string path, CancellationToken _) => media[path]);
        _files.Setup(f => f.WriteAllBytesAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Callback<string, byte[], CancellationToken>((path, bytes, _) => media[path] = bytes).Returns(Task.CompletedTask);
        _files.Setup(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((path, text, _) =>
            {
                media[path] = System.Text.Encoding.UTF8.GetBytes(text);
                if (path == FilePath) _written = text;
            }).Returns(Task.CompletedTask);
        _files.Setup(f => f.CopyFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .Callback<string, string, bool>((source, target, _) => media[target] = media[source]);
        _files.Setup(f => f.MoveFileReplacing(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((source, target) =>
            {
                media[target] = media[source];
                media.Remove(source);
                if (target == FilePath) _written = System.Text.Encoding.UTF8.GetString(media[target]);
            });
        _files.Setup(f => f.DeleteFile(It.IsAny<string>())).Callback<string>(path => media.Remove(path));
        _files.Setup(f => f.DeleteDirectory(It.IsAny<string>(), true)).Callback<string, bool>((path, _) =>
        {
            foreach (var file in media.Keys.Where(p => p.StartsWith(path + @"\", StringComparison.OrdinalIgnoreCase)).ToList())
                media.Remove(file);
        });
        return media;
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task MediaFile_FailurePreservesPreviousXmlAndAllSidecars(bool lateFailure, bool cancelled)
    {
        const string folder = @"C:\Media";
        const string picture = @"C:\Wallpapers\Winhance\beach.jpg";
        var media = ArrangeMedia();
        var writer = new AutounattendWriter(_registry.Object, _scripts.Object, _files.Object, _version.Object, _log.Object);
        var set = new SelectionSet([new SettingChoice("theme-wallpaper-picture", new ChoiceValue.Keyed(picture, "beach"))], [], [])
        {
            Files = [new CarriedFile("theme-wallpaper-picture", picture, "AQID")],
        };
        await writer.WriteMediaAsync(set, CatalogScope.CurrentMachine, FilePath, folder, _ => Task.CompletedTask, CancellationToken.None);
        var previous = new Dictionary<string, byte[]>(media);
        if (lateFailure)
        {
            bool fail = true;
            _files.Setup(f => f.MoveFileReplacing(It.IsAny<string>(), FilePath)).Callback<string, string>((source, target) =>
            {
                if (fail)
                {
                    fail = false;
                    throw new IOException("Publish failed");
                }
                media[target] = media[source];
                media.Remove(source);
            });
        }
        using var cts = new CancellationTokenSource();
        Func<string, Task> validate = _ =>
        {
            if (cancelled) cts.Cancel();
            return cancelled || lateFailure ? Task.CompletedTask : Task.FromException(new IOException("Invalid XML"));
        };
        Func<Task> change = () => writer.WriteMediaAsync(SelectionSet.Empty, CatalogScope.CurrentMachine, FilePath, folder, validate, cts.Token);
        if (cancelled)
            await change.Should().ThrowAsync<OperationCanceledException>();
        else
            await change.Should().ThrowAsync<IOException>();
        media.Should().BeEquivalentTo(previous);
    }

    [Fact]
    public async Task MediaFile_DeselectingEntirePayloadsRemovesOnlyUnmodifiedOwnedFiles()
    {
        const string folder = @"C:\Media";
        const string picture = @"C:\Wallpapers\Winhance\beach.jpg";
        const string album = @"C:\Wallpapers\Winhance\Holiday";
        const string images = @"C:\Media\sources\$OEM$\$1\Wallpapers\Winhance";
        const string custom = @"C:\Media\sources\$OEM$\$1\ProgramData\Winhance\Unattend\Scripts\Custom\System-1.ps1";
        var media = ArrangeMedia();
        var writer = new AutounattendWriter(_registry.Object, _scripts.Object, _files.Object, _version.Object, _log.Object);
        var set = new SelectionSet([
            new SettingChoice("theme-wallpaper-picture", new ChoiceValue.Keyed(picture, "beach")),
            new SettingChoice("theme-wallpaper-album", new ChoiceValue.Text(album)),
            new SettingChoice("autounattend-user-script-system", new ChoiceValue.List([
                new ChoiceValue.ListRow(new Dictionary<string, string> { ["type"] = "0", ["script"] = "Write-Host 'hello'" })])),
        ], [], [])
        {
            Files = [new CarriedFile("theme-wallpaper-picture", picture, "AQID"),
                new CarriedFile("theme-wallpaper-album", album + @"\one.jpg", "BAUG"),
                new CarriedFile("theme-wallpaper-album", album + @"\two.jpg", "BwgJ")],
        };
        await writer.WriteMediaAsync(set, CatalogScope.CurrentMachine, FilePath, folder, _ => Task.CompletedTask, CancellationToken.None);
        media.Should().ContainKey(custom);
        string modified = images + @"\Holiday\two.jpg";
        string unrelated = images + @"\Holiday\user.jpg";
        media[modified] = [12];
        media[unrelated] = [13];
        await writer.WriteMediaAsync(SelectionSet.Empty, CatalogScope.CurrentMachine, FilePath, folder, _ => Task.CompletedTask, CancellationToken.None);
        media.Should().NotContainKey(custom).And.NotContainKey(images + @"\beach.jpg").And.NotContainKey(images + @"\Holiday\one.jpg");
        media[modified].Should().Equal(12);
        media[unrelated].Should().Equal(13);
    }

    [Fact]
    public async Task CompactConfig_RestoresKnownAppIdentifiersAndPreservesDynamicChoices()
    {
        var app = WindowsAppDefinitions.GetWindowsApps().Items.First(a => a.AppxPackageName is { Length: > 0 });
        var known = new AppChoice(app.Id, app.Name, app.AppxPackageName, null, null, null);
        var dynamicApp = new AppChoice("winget-Example.Tool", "Example Tool", null, null, null, "Example.Tool");
        await WriteAsync(new SelectionSet([], [known], [dynamicApp]));

        var config = XDocument.Parse(_written).Root!.Element(AutounattendDocument.WinhanceExtensions + "Extensions")!
            .Element(AutounattendDocument.WinhanceExtensions + "Config")!.Value;
        config.Should().Contain("\"Id\":\"" + app.Id + "\",\"IsSelected\":true");
        config.Should().NotContain("\"AppxPackageName\"");
        config.Should().NotContain("\n");
        var read = ConfigFileMapper.FromFile((await Sut().ReadAsync(FilePath))!.Config!, ParityCatalog.ByFeature);
        read.WindowsApps.Should().BeEquivalentTo(new[] { known });
        read.ExternalApps.Should().Equal(dynamicApp);
    }

    private static ChoiceValue SampleFor(Setting setting) => setting.Control switch
    {
        ControlKind.Toggle or ControlKind.Action => new ChoiceValue.Toggle(true),
        ControlKind.CheckBox => new ChoiceValue.CheckBox(true),
        ControlKind.Selection => new ChoiceValue.Option(Math.Max(0, setting.States.Count - 1)),
        ControlKind.TextBox => new ChoiceValue.Text("round-trip"),
        ControlKind.KeyedSelection => new ChoiceValue.Keyed("key", "Label"),
        ControlKind.CheckList => new ChoiceValue.Keys(["Windows 11 Pro", "Windows 11 Home"]),
        ControlKind.List => new ChoiceValue.List([new ChoiceValue.ListRow(setting.List!.Fields.ToDictionary(
            f => f.Key,
            f => f.Kind switch
            {
                FieldKind.Selection => "0",
                FieldKind.CheckBox => "true",
                FieldKind.Password => "secret",
                _ => "value",
            }))]),
        _ => throw new InvalidOperationException($"{setting.Id} is a {setting.Control}, which no answer-file card uses."),
    };

    [Fact]
    public async Task ReadAsync_WithAFileFromBeforeChoicesWereCarried_ReadsAsWinhanceWithNoChoices()
    {
        await WriteAsync(SelectionSet.Empty);
        _written = Regex.Replace(_written, @"\s*<Config>[\s\S]*?</Config>", string.Empty);

        var read = await Sut().ReadAsync(FilePath);

        read.Should().Be(new WinhanceAnswerFile("26.09.23", null));
    }

    [Fact]
    public async Task ReadAsync_WhenCarriedChoicesHaveANullSection_ReadsAsWinhanceWithNoChoices()
    {
        await WriteAsync(SelectionSet.Empty);
        _written = Regex.Replace(_written, @"<Config>[\s\S]*?</Config>", "<Config>{\"Optimize\":null}</Config>");

        var read = await Sut().ReadAsync(FilePath);

        read.Should().Be(new WinhanceAnswerFile("26.09.23", null));
    }

    [Fact]
    public async Task ReadAsync_WithAFileWinhanceDidNotWrite_ReadsAsNull()
    {
        _written = "<?xml version=\"1.0\"?><unattend xmlns=\"urn:schemas-microsoft-com:unattend\"><settings pass=\"specialize\" /></unattend>";

        (await Sut().ReadAsync(FilePath)).Should().BeNull();
    }

    [Fact]
    public async Task ReadAsync_WithAFileThatIsNotXml_ReadsAsNull()
    {
        _written = "not xml";

        (await Sut().ReadAsync(FilePath)).Should().BeNull();
    }
}

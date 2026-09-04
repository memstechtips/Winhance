using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class ChangeHistoryServiceTests
{
    private readonly Mock<IFileSystemService> _mockFileSystem = new();
    private readonly Mock<ILocalizationService> _mockLocalization = new();
    private readonly Mock<ILogService> _mockLog = new();
    private readonly ChangeHistoryService _service;
    private string _appended = string.Empty;

    public ChangeHistoryServiceTests()
    {
        _mockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem
            .Setup(f => f.AppendAllText(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<System.Text.Encoding>()))
            .Callback<string, string, System.Text.Encoding>((_, text, _) => _appended += text);
        _mockLocalization.Setup(l => l.GetString("ChangeHistory_FileHeader"))
            .Returns("Changes made by Winhance are listed below (newest at the bottom).");
        _service = new ChangeHistoryService(_mockFileSystem.Object, _mockLocalization.Object, _mockLog.Object);
    }

    [Fact]
    public void LogSettingChange_WithGroup_WritesGroupNameBeforeAfter()
    {
        _service.LogSettingChange("Telemetry", "Privacy", "Enabled", "Disabled");

        _appended.Should().EndWith("Privacy — Telemetry: Enabled → Disabled\r\n");
        _appended.Should().StartWith("[");
    }

    [Fact]
    public void LogSettingChange_WithoutGroup_OmitsGroupSeparator()
    {
        _service.LogSettingChange("Telemetry", null, "Enabled", "Disabled");

        _appended.Should().Contain("] Telemetry: Enabled → Disabled");
        _appended.Should().NotContain("—");
    }

    [Fact]
    public void LogSettingAction_WritesNameOnly()
    {
        _service.LogSettingAction("Clean Temp Files", "System");

        _appended.Should().Contain("] System — Clean Temp Files\r\n");
        _appended.Should().NotContain("→");
    }

    [Theory]
    [InlineData(AppChangeKind.Installed, "ChangeHistory_AppInstalled", "App installed: {0}")]
    [InlineData(AppChangeKind.Removed, "ChangeHistory_AppRemoved", "App removed: {0}")]
    [InlineData(AppChangeKind.EnableStarted, "ChangeHistory_AppEnableStarted", "Enable started for {0} in a separate window")]
    public void LogAppChange_UsesLocalizedTemplate(AppChangeKind kind, string key, string template)
    {
        var value = template;
        _mockLocalization.Setup(l => l.TryGetString(key, out value)).Returns(true);

        _service.LogAppChange("Microsoft Edge", kind);

        _appended.Should().Contain($"] {template.Replace("{0}", "Microsoft Edge")}\r\n");
    }

    // The separator between label and name has to come from the template: a colon baked into the C#
    // cannot be a fullwidth one for CJK, and cannot take the space French puts in front of it.
    [Theory]
    [InlineData("App installed\uFF1A{0}", "] App installed\uFF1AMicrosoft Edge\r\n")]
    [InlineData("App installed : {0}", "] App installed : Microsoft Edge\r\n")]
    [InlineData("{0} was installed", "] Microsoft Edge was installed\r\n")]
    public void LogAppChange_SeparatorAndOrderComeFromTheTemplate(string template, string expected)
    {
        var value = template;
        _mockLocalization.Setup(l => l.TryGetString("ChangeHistory_AppInstalled", out value)).Returns(true);

        _service.LogAppChange("Microsoft Edge", AppChangeKind.Installed);

        // EndWith, not Contain: it proves the C# appended nothing of its own after the template.
        _appended.Should().EndWith(expected);
    }

    [Fact]
    public void LogSettingChange_UsesLocalizedTemplate()
    {
        var value = "{0}\uFF1A{1} \u2192 {2}";
        _mockLocalization.Setup(l => l.TryGetString("ChangeHistory_SettingChanged", out value)).Returns(true);

        _service.LogSettingChange("Telemetry", null, "Enabled", "Disabled");

        _appended.Should().Contain("] Telemetry\uFF1AEnabled \u2192 Disabled\r\n");
    }

    [Fact]
    public void BeginBatch_HeaderWrittenLazily_EntriesIndented()
    {
        using (_service.BeginBatch("Config import (my-config.winhance)"))
        {
            _appended.Should().BeEmpty("header is lazy — nothing written until the first entry");
            _service.LogSettingChange("Telemetry", "Privacy", "Enabled", "Disabled");
        }

        _appended.Should().Contain("] Config import (my-config.winhance):\r\n");
        _appended.Should().Contain("    [", "batched entries are indented four spaces");
    }

    [Fact]
    public void BeginBatch_NoEntries_WritesNothing()
    {
        using (_service.BeginBatch("Config import (empty.winhance)")) { }

        _appended.Should().BeEmpty();
    }

    [Fact]
    public void BeginBatch_Nested_JoinsOuterBatch()
    {
        using (_service.BeginBatch("Outer"))
        using (_service.BeginBatch("Inner"))
        {
            _service.LogSettingChange("Telemetry", null, "Enabled", "Disabled");
        }

        _appended.Should().Contain("] Outer:\r\n");
        _appended.Should().NotContain("Inner");
    }

    [Fact]
    public void Entry_AfterBatchDisposed_NotIndented()
    {
        using (_service.BeginBatch("Batch")) { _service.LogSettingChange("A", null, "x", "y"); }
        _appended = string.Empty;

        _service.LogSettingChange("Telemetry", null, "Enabled", "Disabled");

        _appended.Should().StartWith("[");
    }

    [Fact]
    public void FirstWrite_FileMissing_WritesLocalizedHeaderFirst()
    {
        _mockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);
        _mockFileSystem.Setup(f => f.DirectoryExists(It.IsAny<string>())).Returns(true);
        string firstAppend = string.Empty;
        bool headerCaptured = false;
        _mockFileSystem
            .Setup(f => f.AppendAllText(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<System.Text.Encoding>()))
            .Callback<string, string, System.Text.Encoding>((_, text, _) =>
            {
                if (!headerCaptured)
                {
                    firstAppend = text;
                    headerCaptured = true;
                }
            });

        _service.LogSettingChange("Telemetry", null, "Enabled", "Disabled");

        firstAppend.Should().Contain("Changes made by Winhance");
        firstAppend.Should().EndWith("\r\n\r\n");
    }

    [Fact]
    public void AllMethods_FileSystemThrows_NeverThrow_LogWarning()
    {
        _mockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Throws(new System.IO.IOException("disk full"));
        _mockFileSystem
            .Setup(f => f.AppendAllText(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<System.Text.Encoding>()))
            .Throws(new System.IO.IOException("disk full"));

        var act1 = () => _service.LogSettingChange("a", "b", "c", "d");
        var act2 = () => _service.LogSettingAction("a", "b");
        var act3 = () => _service.LogAppChange("a", AppChangeKind.Installed);
        var act4 = () => { using (_service.BeginBatch("h")) { _service.LogSettingChange("a", null, "c", "d"); } };
        var act5 = () => _service.GetFilePath();

        act1.Should().NotThrow();
        act2.Should().NotThrow();
        act3.Should().NotThrow();
        act4.Should().NotThrow();
        act5.Should().NotThrow();
        _mockLog.Verify(
            l => l.Log(LogLevel.Warning, It.Is<string>(s => s.Contains("Failed to")), It.IsAny<Exception?>(), It.IsAny<string>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public void GetFilePath_ReturnsProgramDataWinhancePath()
    {
        var path = _service.GetFilePath();

        path.Should().EndWith("ChangeHistory.txt");
        path.Should().Contain("Winhance");
    }

    [Fact]
    public void LogAppChange_LocalizationThrows_DoesNotThrow()
    {
        _mockLocalization
            .Setup(l => l.TryGetString(It.IsAny<string>(), out It.Ref<string>.IsAny))
            .Throws(new InvalidOperationException("boom"));

        var act = () => _service.LogAppChange("Microsoft Edge", AppChangeKind.Installed);

        act.Should().NotThrow();
        _mockLog.Verify(
            l => l.Log(LogLevel.Warning, It.Is<string>(s => s.Contains("Failed to")), It.IsAny<Exception?>(), It.IsAny<string>()),
            Times.AtLeastOnce);
    }
}

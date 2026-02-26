using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class WindowsCompatibilityFilterTests
{
    private readonly Mock<IWindowsVersionService> _mockVersionService = new();
    private readonly Mock<ILogService> _mockLogService = new();

    #region Constructor

    [Fact]
    public void Constructor_NullVersionService_ThrowsArgumentNullException()
    {
        var act = () => new WindowsCompatibilityFilter(null!, _mockLogService.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("versionService");
    }

    [Fact]
    public void Constructor_NullLogService_ThrowsArgumentNullException()
    {
        var act = () => new WindowsCompatibilityFilter(_mockVersionService.Object, null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logService");
    }

    #endregion

    #region FilterSettingsByWindowsVersion (with applyFilter=true)

    [Fact]
    public void FilterSettingsByWindowsVersion_NoRestrictions_ReturnsAllSettings()
    {
        // Arrange
        _mockVersionService.Setup(v => v.IsWindows11()).Returns(true);
        _mockVersionService.Setup(v => v.GetWindowsBuildNumber()).Returns(22621);

        var filter = CreateFilter();
        var settings = new List<SettingDefinition>
        {
            CreateSetting("s1"),
            CreateSetting("s2"),
            CreateSetting("s3")
        };

        // Act
        var result = filter.FilterSettingsByWindowsVersion(settings);

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public void FilterSettingsByWindowsVersion_Windows10OnlySetting_OnWindows11_FilteredOut()
    {
        // Arrange
        _mockVersionService.Setup(v => v.IsWindows11()).Returns(true);
        _mockVersionService.Setup(v => v.GetWindowsBuildNumber()).Returns(22621);

        var filter = CreateFilter();
        var settings = new List<SettingDefinition>
        {
            CreateSetting("win10only", isWindows10Only: true),
            CreateSetting("normal")
        };

        // Act
        var result = filter.FilterSettingsByWindowsVersion(settings);

        // Assert
        result.Should().ContainSingle()
            .Which.Id.Should().Be("normal");
    }

    [Fact]
    public void FilterSettingsByWindowsVersion_Windows11OnlySetting_OnWindows10_FilteredOut()
    {
        // Arrange
        _mockVersionService.Setup(v => v.IsWindows11()).Returns(false);
        _mockVersionService.Setup(v => v.GetWindowsBuildNumber()).Returns(19045);

        var filter = CreateFilter();
        var settings = new List<SettingDefinition>
        {
            CreateSetting("win11only", isWindows11Only: true),
            CreateSetting("normal")
        };

        // Act
        var result = filter.FilterSettingsByWindowsVersion(settings);

        // Assert
        result.Should().ContainSingle()
            .Which.Id.Should().Be("normal");
    }

    [Fact]
    public void FilterSettingsByWindowsVersion_MinimumBuildNotMet_FilteredOut()
    {
        // Arrange
        _mockVersionService.Setup(v => v.IsWindows11()).Returns(true);
        _mockVersionService.Setup(v => v.GetWindowsBuildNumber()).Returns(22000);

        var filter = CreateFilter();
        var settings = new List<SettingDefinition>
        {
            CreateSetting("needsNewBuild", minimumBuild: 22621),
            CreateSetting("normal")
        };

        // Act
        var result = filter.FilterSettingsByWindowsVersion(settings);

        // Assert
        result.Should().ContainSingle()
            .Which.Id.Should().Be("normal");
    }

    [Fact]
    public void FilterSettingsByWindowsVersion_MaximumBuildExceeded_FilteredOut()
    {
        // Arrange
        _mockVersionService.Setup(v => v.IsWindows11()).Returns(true);
        _mockVersionService.Setup(v => v.GetWindowsBuildNumber()).Returns(26100);

        var filter = CreateFilter();
        var settings = new List<SettingDefinition>
        {
            CreateSetting("oldBuild", maximumBuild: 22621),
            CreateSetting("normal")
        };

        // Act
        var result = filter.FilterSettingsByWindowsVersion(settings);

        // Assert
        result.Should().ContainSingle()
            .Which.Id.Should().Be("normal");
    }

    [Fact]
    public void FilterSettingsByWindowsVersion_BuildInSupportedRange_NotFilteredOut()
    {
        // Arrange
        _mockVersionService.Setup(v => v.IsWindows11()).Returns(true);
        _mockVersionService.Setup(v => v.GetWindowsBuildNumber()).Returns(22621);

        var filter = CreateFilter();
        var settings = new List<SettingDefinition>
        {
            CreateSetting("ranged", supportedRanges: new[] { (22000, 23000) })
        };

        // Act
        var result = filter.FilterSettingsByWindowsVersion(settings);

        // Assert
        result.Should().ContainSingle()
            .Which.Id.Should().Be("ranged");
    }

    [Fact]
    public void FilterSettingsByWindowsVersion_BuildOutsideSupportedRange_FilteredOut()
    {
        // Arrange
        _mockVersionService.Setup(v => v.IsWindows11()).Returns(true);
        _mockVersionService.Setup(v => v.GetWindowsBuildNumber()).Returns(26100);

        var filter = CreateFilter();
        var settings = new List<SettingDefinition>
        {
            CreateSetting("ranged", supportedRanges: new[] { (22000, 22631) }),
            CreateSetting("normal")
        };

        // Act
        var result = filter.FilterSettingsByWindowsVersion(settings);

        // Assert
        result.Should().ContainSingle()
            .Which.Id.Should().Be("normal");
    }

    #endregion

    #region FilterSettingsByWindowsVersion (with applyFilter=false)

    [Fact]
    public void FilterSettingsByWindowsVersion_ApplyFilterFalse_ReturnsAllWithCompatibilityMessages()
    {
        // Arrange
        _mockVersionService.Setup(v => v.IsWindows11()).Returns(true);
        _mockVersionService.Setup(v => v.GetWindowsBuildNumber()).Returns(22621);

        var filter = CreateFilter();
        var settings = new List<SettingDefinition>
        {
            CreateSetting("win10only", isWindows10Only: true),
            CreateSetting("normal")
        };

        // Act
        var result = filter.FilterSettingsByWindowsVersion(settings, applyFilter: false).ToList();

        // Assert - all settings returned (not filtered), but win10only gets a compatibility message
        result.Should().HaveCount(2);
        var win10Setting = result.First(s => s.Id == "win10only");
        win10Setting.CustomProperties.Should().ContainKey(
            Winhance.Core.Features.Common.Constants.CustomPropertyKeys.VersionCompatibilityMessage);
    }

    #endregion

    #region Helpers

    private WindowsCompatibilityFilter CreateFilter()
    {
        return new WindowsCompatibilityFilter(
            _mockVersionService.Object,
            _mockLogService.Object);
    }

    private static SettingDefinition CreateSetting(
        string id,
        bool isWindows10Only = false,
        bool isWindows11Only = false,
        int? minimumBuild = null,
        int? maximumBuild = null,
        (int MinBuild, int MaxBuild)[]? supportedRanges = null)
    {
        return new SettingDefinition
        {
            Id = id,
            Name = id,
            Description = $"Test setting {id}",
            IsWindows10Only = isWindows10Only,
            IsWindows11Only = isWindows11Only,
            MinimumBuildNumber = minimumBuild,
            MaximumBuildNumber = maximumBuild,
            SupportedBuildRanges = supportedRanges ?? Array.Empty<(int, int)>()
        };
    }

    #endregion
}

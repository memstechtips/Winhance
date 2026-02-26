using FluentAssertions;
using Microsoft.Win32;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class RecommendedSettingsServiceTests
{
    private readonly Mock<IDomainServiceRouter> _routerMock;
    private readonly Mock<IWindowsVersionService> _versionServiceMock;
    private readonly Mock<ILogService> _logMock;
    private readonly RecommendedSettingsService _sut;

    public RecommendedSettingsServiceTests()
    {
        _routerMock = new Mock<IDomainServiceRouter>();
        _versionServiceMock = new Mock<IWindowsVersionService>();
        _logMock = new Mock<ILogService>();

        _sut = new RecommendedSettingsService(
            _routerMock.Object,
            _versionServiceMock.Object,
            _logMock.Object);
    }

    private static SettingDefinition CreateSetting(
        string id,
        object? recommendedValue = null,
        bool isWindows10Only = false,
        bool isWindows11Only = false,
        int? minBuild = null,
        int? maxBuild = null)
    {
        var registrySettings = new List<RegistrySetting>();

        if (recommendedValue != null)
        {
            registrySettings.Add(new RegistrySetting
            {
                KeyPath = @"HKCU\Software\Test",
                ValueName = id,
                ValueType = RegistryValueKind.DWord,
                RecommendedValue = recommendedValue,
                IsPrimary = true,
            });
        }

        return new SettingDefinition
        {
            Id = id,
            Name = $"Setting {id}",
            Description = $"Description for {id}",
            RegistrySettings = registrySettings,
            IsWindows10Only = isWindows10Only,
            IsWindows11Only = isWindows11Only,
            MinimumBuildNumber = minBuild,
            MaximumBuildNumber = maxBuild,
        };
    }

    private void SetupWindows11(int buildNumber = 22621)
    {
        _versionServiceMock.Setup(v => v.IsWindows11()).Returns(true);
        _versionServiceMock.Setup(v => v.GetWindowsBuildNumber()).Returns(buildNumber);
    }

    private void SetupWindows10(int buildNumber = 19045)
    {
        _versionServiceMock.Setup(v => v.IsWindows11()).Returns(false);
        _versionServiceMock.Setup(v => v.GetWindowsBuildNumber()).Returns(buildNumber);
    }

    private Mock<IDomainService> SetupDomainWithSettings(
        string domainName,
        IEnumerable<SettingDefinition> settings)
    {
        var domainMock = new Mock<IDomainService>();
        domainMock.Setup(d => d.DomainName).Returns(domainName);
        domainMock.Setup(d => d.GetSettingsAsync())
            .ReturnsAsync(settings);

        _routerMock
            .Setup(r => r.GetDomainService(It.IsAny<string>()))
            .Returns(domainMock.Object);

        return domainMock;
    }

    [Fact]
    public void DomainName_ReturnsRecommendedSettings()
    {
        _sut.DomainName.Should().Be("RecommendedSettings");
    }

    [Fact]
    public async Task GetSettingsAsync_ReturnsEmpty()
    {
        var result = await _sut.GetSettingsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRecommendedSettingsAsync_ReturnsSettingsWithRecommendedValues()
    {
        SetupWindows11();

        var settings = new List<SettingDefinition>
        {
            CreateSetting("setting-with-rec", recommendedValue: 1),
            CreateSetting("setting-without-rec"),
            CreateSetting("another-with-rec", recommendedValue: 0),
        };

        SetupDomainWithSettings("TestDomain", settings);

        var result = await _sut.GetRecommendedSettingsAsync("setting-with-rec");

        var resultList = result.ToList();
        resultList.Should().HaveCount(2);
        resultList.Should().Contain(s => s.Id == "setting-with-rec");
        resultList.Should().Contain(s => s.Id == "another-with-rec");
        resultList.Should().NotContain(s => s.Id == "setting-without-rec");
    }

    [Fact]
    public async Task GetRecommendedSettingsAsync_FiltersOutIncompatibleOS_Windows11Only()
    {
        SetupWindows10();

        var settings = new List<SettingDefinition>
        {
            CreateSetting("win11-only", recommendedValue: 1, isWindows11Only: true),
            CreateSetting("universal", recommendedValue: 1),
        };

        SetupDomainWithSettings("TestDomain", settings);

        var result = await _sut.GetRecommendedSettingsAsync("universal");

        var resultList = result.ToList();
        resultList.Should().HaveCount(1);
        resultList.Should().Contain(s => s.Id == "universal");
    }

    [Fact]
    public async Task GetRecommendedSettingsAsync_FiltersOutIncompatibleOS_Windows10Only()
    {
        SetupWindows11();

        var settings = new List<SettingDefinition>
        {
            CreateSetting("win10-only", recommendedValue: 1, isWindows10Only: true),
            CreateSetting("universal", recommendedValue: 1),
        };

        SetupDomainWithSettings("TestDomain", settings);

        var result = await _sut.GetRecommendedSettingsAsync("universal");

        var resultList = result.ToList();
        resultList.Should().HaveCount(1);
        resultList.Should().Contain(s => s.Id == "universal");
    }

    [Fact]
    public async Task GetRecommendedSettingsAsync_FiltersOutBelowMinimumBuild()
    {
        SetupWindows11(buildNumber: 22000);

        var settings = new List<SettingDefinition>
        {
            CreateSetting("needs-newer", recommendedValue: 1, minBuild: 22621),
            CreateSetting("compatible", recommendedValue: 1),
        };

        SetupDomainWithSettings("TestDomain", settings);

        var result = await _sut.GetRecommendedSettingsAsync("compatible");

        var resultList = result.ToList();
        resultList.Should().HaveCount(1);
        resultList.Should().Contain(s => s.Id == "compatible");
    }

    [Fact]
    public async Task GetRecommendedSettingsAsync_FiltersOutAboveMaximumBuild()
    {
        SetupWindows11(buildNumber: 25000);

        var settings = new List<SettingDefinition>
        {
            CreateSetting("old-only", recommendedValue: 1, maxBuild: 22621),
            CreateSetting("compatible", recommendedValue: 1),
        };

        SetupDomainWithSettings("TestDomain", settings);

        var result = await _sut.GetRecommendedSettingsAsync("compatible");

        var resultList = result.ToList();
        resultList.Should().HaveCount(1);
        resultList.Should().Contain(s => s.Id == "compatible");
    }

    [Fact]
    public async Task GetRecommendedSettingsAsync_UnknownDomain_ThrowsFromRouter()
    {
        SetupWindows11();

        _routerMock
            .Setup(r => r.GetDomainService(It.IsAny<string>()))
            .Throws(new ArgumentException("No domain service found for 'unknown-setting'"));

        var action = () => _sut.GetRecommendedSettingsAsync("unknown-setting");

        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*unknown-setting*");
    }

    [Fact]
    public async Task GetRecommendedSettingsAsync_AllSettingsFiltered_ReturnsEmpty()
    {
        SetupWindows10();

        var settings = new List<SettingDefinition>
        {
            CreateSetting("win11-only", recommendedValue: 1, isWindows11Only: true),
        };

        SetupDomainWithSettings("TestDomain", settings);

        var result = await _sut.GetRecommendedSettingsAsync("win11-only");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRecommendedSettingsAsync_Windows10CompatibleSettings_Returned()
    {
        SetupWindows10();

        var settings = new List<SettingDefinition>
        {
            CreateSetting("win10-setting", recommendedValue: 1, isWindows10Only: true),
        };

        SetupDomainWithSettings("TestDomain", settings);

        var result = await _sut.GetRecommendedSettingsAsync("win10-setting");

        result.Should().HaveCount(1);
        result.First().Id.Should().Be("win10-setting");
    }

    [Fact]
    public async Task GetRecommendedSettingsAsync_BuildInRange_Returned()
    {
        SetupWindows11(buildNumber: 22621);

        var settings = new List<SettingDefinition>
        {
            CreateSetting("in-range", recommendedValue: 1, minBuild: 22000, maxBuild: 23000),
        };

        SetupDomainWithSettings("TestDomain", settings);

        var result = await _sut.GetRecommendedSettingsAsync("in-range");

        result.Should().HaveCount(1);
        result.First().Id.Should().Be("in-range");
    }
}

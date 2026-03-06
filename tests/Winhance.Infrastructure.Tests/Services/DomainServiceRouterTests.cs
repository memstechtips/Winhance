using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class DomainServiceRouterTests
{
    private static Mock<IDomainService> CreateMockDomain(string domainName)
    {
        var mock = new Mock<IDomainService>();
        mock.Setup(s => s.DomainName).Returns(domainName);
        return mock;
    }

    [Fact]
    public void GetDomainService_ByDomainName_ReturnsCorrectService()
    {
        var mockService = CreateMockDomain("WindowsTheme");
        var router = new DomainServiceRouter(new[] { mockService.Object });

        var result = router.GetDomainService("WindowsTheme");

        result.Should().BeSameAs(mockService.Object);
    }

    [Fact]
    public void GetDomainService_MultipleServices_ReturnsCorrectOne()
    {
        var theme = CreateMockDomain("WindowsTheme");
        var power = CreateMockDomain("Power");
        var privacy = CreateMockDomain("PrivacyAndSecurity");
        var router = new DomainServiceRouter(new[] { theme.Object, power.Object, privacy.Object });

        router.GetDomainService("Power").Should().BeSameAs(power.Object);
        router.GetDomainService("WindowsTheme").Should().BeSameAs(theme.Object);
        router.GetDomainService("PrivacyAndSecurity").Should().BeSameAs(privacy.Object);
    }

    [Fact]
    public void GetDomainService_UnknownId_ThrowsArgumentException()
    {
        var router = new DomainServiceRouter(new[] { CreateMockDomain("Power").Object });

        var action = () => router.GetDomainService("NonExistent");

        action.Should().Throw<ArgumentException>()
            .WithMessage("*NonExistent*");
    }

    [Fact]
    public void AddSettingMappings_AllowsLookupBySettingId()
    {
        var mockService = CreateMockDomain("WindowsTheme");
        var router = new DomainServiceRouter(new[] { mockService.Object });

        router.AddSettingMappings("WindowsTheme", new[] { "dark-mode-toggle", "accent-color" });

        router.GetDomainService("dark-mode-toggle").Should().BeSameAs(mockService.Object);
        router.GetDomainService("accent-color").Should().BeSameAs(mockService.Object);
    }

    [Fact]
    public void GetDomainService_SettingIdFallback_WhenDirectLookupFails()
    {
        var mockService = CreateMockDomain("Power");
        var router = new DomainServiceRouter(new[] { mockService.Object });
        router.AddSettingMappings("Power", new[] { "hibernation-toggle", "sleep-timeout" });

        // "hibernation-toggle" is not a direct domain name, but is mapped
        router.GetDomainService("hibernation-toggle").Should().BeSameAs(mockService.Object);
    }

    [Fact]
    public void GetDomainService_DirectMatchTakesPrecedenceOverSettingMap()
    {
        var directService = CreateMockDomain("MyFeature");
        var otherService = CreateMockDomain("Other");
        var router = new DomainServiceRouter(new[] { directService.Object, otherService.Object });

        // Map "MyFeature" as a setting ID pointing to "Other"
        router.AddSettingMappings("Other", new[] { "MyFeature" });

        // Direct match should take precedence
        router.GetDomainService("MyFeature").Should().BeSameAs(directService.Object);
    }

    [Fact]
    public void AddSettingMappings_OverwritesPreviousMapping()
    {
        var service1 = CreateMockDomain("Feature1");
        var service2 = CreateMockDomain("Feature2");
        var router = new DomainServiceRouter(new[] { service1.Object, service2.Object });

        router.AddSettingMappings("Feature1", new[] { "setting-x" });
        router.AddSettingMappings("Feature2", new[] { "setting-x" });

        // Last mapping wins for ConcurrentDictionary
        router.GetDomainService("setting-x").Should().BeSameAs(service2.Object);
    }

    [Fact]
    public void Constructor_EmptyServices_DoesNotThrow()
    {
        var action = () => new DomainServiceRouter(Enumerable.Empty<IDomainService>());
        action.Should().NotThrow();
    }

    [Fact]
    public void Constructor_DuplicateDomainNames_LastWins()
    {
        var service1 = CreateMockDomain("Same");
        var service2 = CreateMockDomain("Same");
        var router = new DomainServiceRouter(new[] { service1.Object, service2.Object });

        // Dictionary iteration: last entry wins for the same key
        router.GetDomainService("Same").Should().BeSameAs(service2.Object);
    }
}

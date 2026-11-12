using System.Net;
using System.Text;
using FluentAssertions;
using Moq;
using Moq.Protected;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Core.Features.SoftwareApps.Utilities;
using Winhance.Infrastructure.Features.SoftwareApps.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class StoreCatalogServiceTests
{
    private const string CalcFamily = "Microsoft.WindowsCalculator_8wekyb3d8bbwe";
    private const string NotepadFamily = "Microsoft.WindowsNotepad_8wekyb3d8bbwe";

    private readonly Mock<HttpMessageHandler> _handler = new();
    private readonly Mock<IWindowsRegistryService> _registry = new();
    private readonly List<HttpRequestMessage> _requests = [];

    private static readonly StoreCatalogApp[] Apps =
    [
        new("9WZDNCRFHVN5", CalcFamily),
        new("9MSMLRH6LZF3", NotepadFamily),
        new("9NBLGGH4NNS1", "Microsoft.DesktopAppInstaller_8wekyb3d8bbwe"),
    ];

    private static string Packed(string dotted)
    {
        PackedVersion.TryFromDotted(dotted, out var packed);
        return packed.ToString();
    }

    private static readonly string Windows10 = Packed("10.0.17763.0");

    private static string Package(string family, string version, int rank = 30000) =>
        $$"""
        {"PackageFamilyName":"{{family}}","Version":"{{Packed(version)}}","PackageRank":{{rank}},"PackageFormat":"MsixBundle","Architectures":["neutral"],
         "PlatformDependencies":[{"PlatformName":"Windows.Universal","MinVersion":"{{Windows10}}"}]}
        """;

    private static string Catalog(params string[] packages) =>
        """{"Products":[{"DisplaySkuAvailabilities":[{"Sku":{"Properties":{"Packages":[@PACKAGES@]}}}]}]}"""
            .Replace("@PACKAGES@", string.Join(',', packages));

    private StoreCatalogService Sut(HttpResponseMessage? response = null, Exception? failure = null)
    {
        var setup = _handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
        if (failure is not null)
            setup.ThrowsAsync(failure);
        else
            setup.Callback<HttpRequestMessage, CancellationToken>((request, _) => _requests.Add(request)).ReturnsAsync(response!);
        return new StoreCatalogService(new HttpClient(_handler.Object), _registry.Object, Mock.Of<ILogService>());
    }

    private static HttpResponseMessage Ok(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private void Installed(params string[] keys) =>
        _registry.Setup(r => r.GetSubKeyNames(InstalledStoreVersions.Applications)).Returns(keys);

    [Fact]
    public async Task AsksOnceForEveryId_AndNamesTheAppsWithANewerVersion()
    {
        Installed(
            "Microsoft.WindowsCalculator_2021.2502.2.0_neutral_~_8wekyb3d8bbwe",
            "Microsoft.WindowsNotepad_11.2607.14.0_neutral_~_8wekyb3d8bbwe");
        var sut = Sut(Ok(Catalog(Package(CalcFamily, "2021.2607.0.0"), Package(NotepadFamily, "11.2607.14.0"))));

        var updates = await sut.GetUpdatesAsync(Apps);

        updates.Should().ContainSingle().Which.Should().Be(
            new KeyValuePair<string, StoreCatalogUpdate>(CalcFamily, new StoreCatalogUpdate("2021.2502.2.0", "2021.2607.0.0")));
        _requests.Should().ContainSingle().Which.RequestUri!.ToString().Should().Be(
            "https://displaycatalog.mp.microsoft.com/v7.0/products?bigIds=9WZDNCRFHVN5,9MSMLRH6LZF3,9NBLGGH4NNS1&market=US&languages=en-us");
    }

    [Fact]
    public async Task InstalledHigherThanTheCatalog_IsNotAnUpdate()
    {
        Installed("Microsoft.WindowsCalculator_2021.2700.0.0_neutral_~_8wekyb3d8bbwe");
        var sut = Sut(Ok(Catalog(Package(CalcFamily, "2021.2607.0.0"))));

        (await sut.GetUpdatesAsync(Apps)).Should().BeEmpty();
    }

    [Fact]
    public async Task InstalledEqualToTheCatalog_IsNotAnUpdate()
    {
        Installed("Microsoft.WindowsCalculator_2021.2607.0.0_neutral_~_8wekyb3d8bbwe");
        var sut = Sut(Ok(Catalog(Package(CalcFamily, "2021.2607.0.0"))));

        (await sut.GetUpdatesAsync(Apps)).Should().BeEmpty();
    }

    [Fact]
    public async Task NoProductsForAnId_IsUnknownNotUpToDate()
    {
        Installed("Microsoft.DesktopAppInstaller_2025.926.104.0_neutral_~_8wekyb3d8bbwe");
        var sut = Sut(Ok("""{"Products":[]}"""));

        (await sut.GetUpdatesAsync(Apps)).Should().BeEmpty();
    }

    [Fact]
    public async Task AppWithNoRegistryKey_MakesNoClaim()
    {
        Installed();
        var sut = Sut(Ok(Catalog(Package(CalcFamily, "2021.2607.0.0"))));

        (await sut.GetUpdatesAsync(Apps)).Should().BeEmpty();
    }

    [Fact]
    public async Task ServerError_GivesNoAnswers()
    {
        Installed("Microsoft.WindowsCalculator_2021.2502.2.0_neutral_~_8wekyb3d8bbwe");
        var sut = Sut(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        (await sut.GetUpdatesAsync(Apps)).Should().BeEmpty();
    }

    [Fact]
    public async Task NetworkFailure_GivesNoAnswers()
    {
        Installed("Microsoft.WindowsCalculator_2021.2502.2.0_neutral_~_8wekyb3d8bbwe");
        var sut = Sut(failure: new HttpRequestException("offline"));

        (await sut.GetUpdatesAsync(Apps)).Should().BeEmpty();
    }

    [Fact]
    public async Task UnreadableBody_GivesNoAnswers()
    {
        Installed("Microsoft.WindowsCalculator_2021.2502.2.0_neutral_~_8wekyb3d8bbwe");
        var sut = Sut(Ok("<html>not json</html>"));

        (await sut.GetUpdatesAsync(Apps)).Should().BeEmpty();
    }

    [Fact]
    public async Task NoApps_MakesNoRequest()
    {
        var sut = Sut(Ok(Catalog()));

        (await sut.GetUpdatesAsync([])).Should().BeEmpty();
        _requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CallerCancellation_IsNotSwallowed()
    {
        Installed("Microsoft.WindowsCalculator_2021.2502.2.0_neutral_~_8wekyb3d8bbwe");
        var sut = Sut(failure: new OperationCanceledException());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        var act = () => sut.GetUpdatesAsync(Apps, cancelled.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}

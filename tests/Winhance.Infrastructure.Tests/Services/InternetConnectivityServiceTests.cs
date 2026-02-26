using System.Net;
using FluentAssertions;
using Moq;
using Moq.Protected;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class InternetConnectivityServiceTests
{
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<HttpMessageHandler> _mockHttpHandler = new();
    private readonly HttpClient _httpClient;
    private readonly InternetConnectivityService _service;

    public InternetConnectivityServiceTests()
    {
        _httpClient = new HttpClient(_mockHttpHandler.Object);
        _service = new InternetConnectivityService(_mockLogService.Object, _httpClient);
    }

    #region Caching Behavior

    [Fact]
    public async Task IsInternetConnectedAsync_SecondCallWithinCacheWindow_ReturnsCachedResult()
    {
        // Arrange — first call succeeds with HTTP 200
        SetupHttpResponse(HttpStatusCode.OK);

        // Act — first call populates the cache
        var firstResult = await _service.IsInternetConnectedAsync(
            forceCheck: false, CancellationToken.None);

        // Second call should use cache — even if we break the handler
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Should not be called"));

        var secondResult = await _service.IsInternetConnectedAsync(
            forceCheck: false, CancellationToken.None);

        // Assert
        firstResult.Should().BeTrue();
        secondResult.Should().BeTrue();
    }

    [Fact]
    public async Task IsInternetConnectedAsync_ForceCheck_BypassesCache()
    {
        // Arrange — first call succeeds
        SetupHttpResponse(HttpStatusCode.OK);

        var firstResult = await _service.IsInternetConnectedAsync(
            forceCheck: false, CancellationToken.None);
        firstResult.Should().BeTrue();

        // Now make all HTTP calls fail
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network unreachable"));

        // Act — forceCheck should bypass the cached "true" result
        var secondResult = await _service.IsInternetConnectedAsync(
            forceCheck: true, CancellationToken.None);

        // Assert — the HTTP check should fail, so the result is false
        // Note: This test relies on NetworkInterface.GetIsNetworkAvailable() returning true
        // in the test environment. If the network is truly available, it will try HTTP calls
        // which will fail, resulting in false. If not available, it returns false immediately.
        secondResult.Should().BeFalse();
    }

    #endregion

    #region Cancellation

    [Fact]
    public async Task IsInternetConnectedAsync_CancellationTokenAlreadyCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert — the method checks for cancellation at the very beginning
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _service.IsInternetConnectedAsync(
                forceCheck: false, cts.Token));
    }

    [Fact]
    public async Task IsInternetConnectedAsync_CancellationDuringHttpCheck_ThrowsOperationCanceledException()
    {
        // Arrange — simulate cancellation during HTTP request.
        // HttpClient wraps OperationCanceledException in TaskCanceledException,
        // which IS an OperationCanceledException (subclass), so we assert on the base type.
        using var cts = new CancellationTokenSource();

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>((_, ct) =>
            {
                cts.Cancel();
                throw new OperationCanceledException(cts.Token);
            });

        // Act & Assert — use Assert.ThrowsAnyAsync to accept both
        // OperationCanceledException and TaskCanceledException (its subclass)
        var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _service.IsInternetConnectedAsync(
                forceCheck: true, cts.Token));

        ex.Should().BeAssignableTo<OperationCanceledException>();
    }

    [Fact]
    public async Task IsInternetConnectedAsync_UserInitiatedCancellation_LogsUserCancellationMessage()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _service.IsInternetConnectedAsync(
                forceCheck: false, cts.Token, userInitiatedCancellation: true));

        _mockLogService.Verify(
            l => l.LogInformation(It.Is<string>(s => s.Contains("cancelled by user"))),
            Times.AtLeastOnce);
    }

    #endregion

    #region HTTP Connectivity

    [Fact]
    public async Task IsInternetConnectedAsync_SuccessfulHttpResponse_ReturnsTrue()
    {
        // Arrange
        SetupHttpResponse(HttpStatusCode.OK);

        // Act
        var result = await _service.IsInternetConnectedAsync(
            forceCheck: true, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsInternetConnectedAsync_AllHttpChecksFail_ReturnsFalse()
    {
        // Arrange — all HTTP requests fail
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        // Act
        var result = await _service.IsInternetConnectedAsync(
            forceCheck: true, CancellationToken.None);

        // Assert — this depends on whether the test environment reports a network as available.
        // If NetworkInterface.GetIsNetworkAvailable() returns false, we get false immediately.
        // If it returns true, all HTTP checks fail, resulting in false.
        result.Should().BeFalse();
    }

    #endregion

    #region Constructor Validation

    [Fact]
    public void Constructor_NullLogService_ThrowsArgumentNullException()
    {
        var act = () => new InternetConnectivityService(null!, _httpClient);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logService");
    }

    [Fact]
    public void Constructor_NullHttpClient_ThrowsArgumentNullException()
    {
        var act = () => new InternetConnectivityService(_mockLogService.Object, null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("httpClient");
    }

    #endregion

    #region Helpers

    private void SetupHttpResponse(HttpStatusCode statusCode)
    {
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(statusCode));
    }

    #endregion
}

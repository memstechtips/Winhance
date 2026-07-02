using System;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Moq.Protected;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.SoftwareApps.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class RepoIconSourceTests
{
    // 1×1 transparent PNG — decodes cleanly via BitmapDecoder on the Windows test pass.
    private static readonly byte[] ValidPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    private readonly Mock<ILogService> _mockLog = new();

    // HttpClient.Dispose() forwards to HttpMessageHandler.Dispose(bool), which a
    // Strict mock rejects without an explicit setup. Stub Dispose on every handler.
    private static Mock<HttpMessageHandler> NewStrictHandler()
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler.Protected().Setup("Dispose", ItExpr.IsAny<bool>());
        return handler;
    }

    private static Mock<HttpMessageHandler> SetupHandler(HttpStatusCode status, byte[]? body = null)
    {
        var handler = NewStrictHandler();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(status)
            {
                Content = new ByteArrayContent(body ?? Array.Empty<byte>()),
            });
        return handler;
    }

    private static string Sha256HexLower(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    [Fact]
    public async Task GetIconBytesAsync_ReturnsBytes_WhenSha256Matches()
    {
        var handler = SetupHandler(HttpStatusCode.OK, ValidPng);
        using var client = new HttpClient(handler.Object);
        var source = new RepoIconSource(client, _mockLog.Object);

        var result = await source.GetIconBytesAsync("icons/windows/x.png", Sha256HexLower(ValidPng));

        result.Should().NotBeNull();
        result.Should().Equal(ValidPng);
    }

    [Fact]
    public async Task GetIconBytesAsync_ReturnsNull_WhenSha256Mismatches()
    {
        var handler = SetupHandler(HttpStatusCode.OK, ValidPng);
        using var client = new HttpClient(handler.Object);
        var source = new RepoIconSource(client, _mockLog.Object);

        var result = await source.GetIconBytesAsync("icons/windows/x.png", "deadbeef");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetIconBytesAsync_ReturnsNull_OnHttp404()
    {
        var handler = SetupHandler(HttpStatusCode.NotFound);
        using var client = new HttpClient(handler.Object);
        var source = new RepoIconSource(client, _mockLog.Object);

        var result = await source.GetIconBytesAsync("icons/windows/missing.png", null);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetIconBytesAsync_ReturnsNull_WhenBodyIsNotADecodableImage()
    {
        // Exercises BitmapDecoder — only runs on the Windows test pass.
        var handler = SetupHandler(HttpStatusCode.OK, System.Text.Encoding.UTF8.GetBytes("<html>nope"));
        using var client = new HttpClient(handler.Object);
        var source = new RepoIconSource(client, _mockLog.Object);

        var result = await source.GetIconBytesAsync("icons/windows/notimage.png", null);

        result.Should().BeNull();
    }
}

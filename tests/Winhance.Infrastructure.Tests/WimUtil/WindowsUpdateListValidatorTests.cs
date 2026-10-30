using System.Text.Json;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.WimUtil.Models;
using Winhance.Infrastructure.Features.WimUtil.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.WimUtil;

public class WindowsUpdateListValidatorTests
{
    private const string SeedJson = """
        {
          "schema": 1,
          "generatedAt": "2026-11-11T19:05:00Z",
          "builds": [
            {
              "build": 26100,
              "arch": "x64",
              "name": "Windows 11 24H2",
              "updates": [
                {
                  "kb": "KB5127648",
                  "title": "2026-10 Cumulative Update for Windows 11 Version 24H2 for x64-based Systems (KB5127648) (26100.9711)",
                  "targetUbr": 9711,
                  "releaseDate": "2026-10-14",
                  "catalogId": "8d5353b2-7db6-451a-a42c-2dc9c486372d",
                  "files": [
                    {
                      "role": "checkpoint",
                      "kb": "KB5043080",
                      "fileName": "windows11.0-kb5043080-x64_953449672073f8fb99badb4cc6d5d7849b9c83e8.msu",
                      "url": "https://catalog.sf.dl.delivery.mp.microsoft.com/filestreamingservice/files/d8b7f92b-bd35-4b4c-96e5-46ce984b31e0/public/windows11.0-kb5043080-x64_953449672073f8fb99badb4cc6d5d7849b9c83e8.msu",
                      "size": 533761740,
                      "sha1": "953449672073f8fb99badb4cc6d5d7849b9c83e8",
                      "sha256": "819632610a101af11c72907fb13ff05b5fb4222c0e263ee9a03bcc1d1d5f26a4"
                    },
                    {
                      "role": "target",
                      "kb": "KB5127648",
                      "fileName": "windows11.0-kb5127648-x64_2b88d2d308adc938fcd4d2876a68ee0b2a3c4c09.msu",
                      "url": "https://catalog.sf.dl.delivery.mp.microsoft.com/filestreamingservice/files/4225b261-8248-43e5-a808-34f3faafe3f6/public/windows11.0-kb5127648-x64_2b88d2d308adc938fcd4d2876a68ee0b2a3c4c09.msu",
                      "size": 4775050195,
                      "sha1": "2b88d2d308adc938fcd4d2876a68ee0b2a3c4c09",
                      "sha256": "8a1067c763121953876e2e0dcbf5e2c5832545a44ef6baba362938287e05fcfb"
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly Mock<ILogService> _log = new();

    public static TheoryData<string, Func<WindowsUpdateList, WindowsUpdateList>> BadLists => new()
    {
        { "other host", OnTarget(f => f with { Url = f.Url.Replace("catalog.sf.dl.delivery.mp.microsoft.com", "example.com") }) },
        { "http", OnTarget(f => f with { Url = f.Url.Replace("https://", "http://") }) },
        { "query string", OnTarget(f => f with { Url = f.Url + "?a=b" }) },
        { "explicit port 443", OnTarget(f => f with { Url = f.Url.Replace("microsoft.com/", "microsoft.com:443/") }) },
        { "userinfo", OnTarget(f => f with { Url = f.Url.Replace("https://", "https://user@") }) },
        { "fragment", OnTarget(f => f with { Url = f.Url + "#x" }) },
        { "name and SHA1 differ", OnTarget(f => f with { Sha1 = new string('a', 40) }) },
        { "name arch differs from build arch", s => s with { Builds = [s.Builds[0] with { Arch = "arm64" }] } },
        { "target not last", OnUpdate(u => u with { Files = [u.Files[1], u.Files[0]] }) },
        { "two targets", OnUpdate(u => u with { Files = [u.Files[0] with { Role = "target" }, u.Files[1]] }) },
        { "target KB differs from the update KB", OnUpdate(u => u with { Kb = "KB5127649" }) },
        { "upper-case sha256", OnTarget(f => f with { Sha256 = f.Sha256.ToUpperInvariant() }) },
        { "short sha256", OnTarget(f => f with { Sha256 = f.Sha256[..63] }) },
        { "unknown role", OnUpdate(u => u with { Files = [u.Files[0] with { Role = "extra" }, u.Files[1]] }) },
        { "zero size", OnTarget(f => f with { Size = 0 }) },
        { "schema 2", s => s with { Schema = 2 } },
    };

    [Fact]
    public void Validate_SeedEntry_IsAccepted()
    {
        var builds = WindowsUpdateListValidator.Validate(Seed(), _log.Object);

        builds.Should().ContainSingle();
        builds[0].Updates.Should().ContainSingle().Which.Kb.Should().Be("KB5127648");
        _log.Verify(l => l.LogWarning(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [MemberData(nameof(BadLists))]
    public void Validate_BadEntry_IsDropped(string reason, Func<WindowsUpdateList, WindowsUpdateList> corrupt)
    {
        var builds = WindowsUpdateListValidator.Validate(corrupt(Seed()), _log.Object);

        builds.Should().BeEmpty(reason);
        _log.Verify(l => l.LogWarning(It.IsAny<string>(), It.IsAny<string>()), Times.AtLeastOnce);
    }

    [Fact]
    public void Validate_OneBadUpdateAmongGoodOnes_KeepsTheGoodOnesAndNamesTheBadKb()
    {
        var seed = Seed();
        var good = seed.Builds[0].Updates[0];
        var bad = good with { Kb = "KB5127649" };
        var list = seed with { Builds = [seed.Builds[0] with { Updates = [bad, good] }] };

        var builds = WindowsUpdateListValidator.Validate(list, _log.Object);

        builds[0].Updates.Should().ContainSingle().Which.Kb.Should().Be("KB5127648");
        _log.Verify(l => l.LogWarning(It.Is<string>(m => m.Contains("KB5127649")), It.IsAny<string>()), Times.Once);
    }

    private static WindowsUpdateList Seed() => JsonSerializer.Deserialize<WindowsUpdateList>(SeedJson, JsonOptions)!;

    private static Func<WindowsUpdateList, WindowsUpdateList> OnUpdate(Func<WindowsUpdate, WindowsUpdate> change) =>
        seed => seed with { Builds = [seed.Builds[0] with { Updates = [change(seed.Builds[0].Updates[0])] }] };

    private static Func<WindowsUpdateList, WindowsUpdateList> OnTarget(Func<WindowsUpdateFile, WindowsUpdateFile> change) =>
        OnUpdate(u => u with { Files = [u.Files[0], change(u.Files[1])] });
}

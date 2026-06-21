using System.Collections.Generic;
using Winhance.Core.Features.Common.Catalog;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

public class SystemTrayDetectorTests
{
    private sealed class FakeCtx : IDetectionContext
    {
        public string[] SubKeys = System.Array.Empty<string>();
        public Dictionary<string, object?> Values = new(); // "path\\name" -> value
        public string[] GetSubKeyNames(string keyPath) => SubKeys;
        public object? GetValue(string keyPath, string? valueName) =>
            Values.TryGetValue($"{keyPath}\\{valueName}", out var v) ? v : null;
        public bool KeyExists(string keyPath) => false;
        public string? PrimaryDnsV4OfActiveAdapter() => null;
        public bool IsSystemRestoreEnabled() => false;
    }

    private const string Key = @"HKEY_CURRENT_USER\Control Panel\NotifyIconSettings";
    private static readonly Setting Dummy = new() { Id = "tray", Name = "t", Description = "t" };
    private static readonly SystemTrayDetector Det = new("Show all", "Hide all");

    [Fact]
    public void All_promoted_is_show_all()
    {
        var ctx = new FakeCtx { SubKeys = new[] { "a", "b" } };
        ctx.Values[$@"{Key}\a\IsPromoted"] = 1;
        ctx.Values[$@"{Key}\b\IsPromoted"] = 1;
        Assert.Equal("Show all", Det.Detect(Dummy, ctx));
    }

    [Fact]
    public void None_promoted_is_hide_all()
    {
        var ctx = new FakeCtx { SubKeys = new[] { "a", "b" } };
        ctx.Values[$@"{Key}\a\IsPromoted"] = 0;
        ctx.Values[$@"{Key}\b\IsPromoted"] = 0;
        Assert.Equal("Hide all", Det.Detect(Dummy, ctx));
    }

    [Fact]
    public void Mixed_is_custom()
    {
        var ctx = new FakeCtx { SubKeys = new[] { "a", "b" } };
        ctx.Values[$@"{Key}\a\IsPromoted"] = 1;
        ctx.Values[$@"{Key}\b\IsPromoted"] = 0;
        Assert.Null(Det.Detect(Dummy, ctx));
    }

    [Fact]
    public void No_subkeys_is_custom()
    {
        Assert.Null(Det.Detect(Dummy, new FakeCtx()));
    }

    [Fact]
    public void No_ispromoted_values_is_custom()
    {
        var ctx = new FakeCtx { SubKeys = new[] { "a" } }; // subkey exists but no IsPromoted value
        Assert.Null(Det.Detect(Dummy, ctx));
    }
}

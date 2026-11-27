using System.Text.RegularExpressions;
using FluentAssertions;
using Winhance.UI.Features.Common.Services;
using Xunit;
using static Winhance.UI.Tests.Conventions.ConventionSource;

namespace Winhance.UI.Tests.Conventions;

// The tray host keeps only the strings under TrayHost.StringPrefixes, so a key outside them shows as "[Key]" in the
// tray. These are the files whose strings the host itself shows; the app updates and profile switches it starts run
// in the full app, with the whole table.
public class TrayConventionTests
{
    private static readonly string[] HostFiles =
    [
        Ui + "Features/Common/Services/TrayHost.cs",
        Ui + "Features/Common/Services/TrayStatus.cs",
        Ui + "Features/Common/Services/TrayController.cs",
        Ui + "Features/Common/Services/ProfileService.cs",
    ];

    [Fact]
    public void EveryStringTheTrayHostShows_IsUnderAKeptPrefix()
    {
        var calls = HostFiles
            .SelectMany(file => Regex.Matches(File.ReadAllText(Path.Combine(Root, file)), @"GetString\(\s*([^,)]*)"))
            .Select(m => m.Groups[1].Value.Trim())
            .ToList();

        calls.Count.Should().BeGreaterThan(30);
        calls.Where(arg => !arg.StartsWith('"')).Should().BeEmpty("a key built at runtime cannot be checked against the prefixes");
        calls.Select(arg => arg.Trim('"'))
            .Where(key => !TrayHost.StringPrefixes.Any(prefix => key.StartsWith(prefix, StringComparison.Ordinal)))
            .Should().BeEmpty("the tray host drops every other string");
    }
}

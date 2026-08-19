using FluentAssertions;
using Winhance.Core.Features.Common.TechnicalDetails;
using Xunit;

namespace Winhance.Core.Tests.TechnicalDetails;

/// <summary>
/// The option matrix's spanning header is charged to the columns underneath it, so the hive is
/// abbreviated to keep a 68-character path from stretching the table. That makes this a display
/// transform on a value that also drives the Registry Editor button -- it has to shorten the paths
/// it recognises and leave everything else exactly as it found it.
/// </summary>
public class RegistryPathFormatterTests
{
    [Theory]
    [InlineData(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System",
                @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System")]
    [InlineData(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer",
                @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer")]
    [InlineData(@"HKEY_CLASSES_ROOT\Directory\Background", @"HKCR\Directory\Background")]
    [InlineData(@"HKEY_USERS\.DEFAULT\Control Panel", @"HKU\.DEFAULT\Control Panel")]
    [InlineData(@"HKEY_CURRENT_CONFIG\Software", @"HKCC\Software")]
    public void Abbreviate_ShortensAKnownHive(string path, string expected) =>
        RegistryPathFormatter.Abbreviate(path).Should().Be(expected);

    [Fact]
    public void Abbreviate_ShortensAHiveWithNoSubkey() =>
        RegistryPathFormatter.Abbreviate("HKEY_LOCAL_MACHINE").Should().Be("HKLM");

    [Fact]
    public void Abbreviate_MatchesTheHiveCaseInsensitively() =>
        RegistryPathFormatter.Abbreviate(@"hkey_local_machine\SOFTWARE").Should().Be(@"HKLM\SOFTWARE");

    [Fact]
    public void Abbreviate_OnlyMatchesAWholeSegment()
    {
        // HKEY_USERS_BACKUP is a key name that merely starts the same way. Rewriting it would point
        // the tooltip and the Registry Editor button at somewhere that does not exist.
        RegistryPathFormatter.Abbreviate(@"HKEY_USERS_BACKUP\Data")
            .Should().Be(@"HKEY_USERS_BACKUP\Data");
    }

    [Fact]
    public void Abbreviate_DistinguishesTheTwoHKEY_CURRENT_Hives()
    {
        RegistryPathFormatter.Abbreviate(@"HKEY_CURRENT_USER\A").Should().Be(@"HKCU\A");
        RegistryPathFormatter.Abbreviate(@"HKEY_CURRENT_CONFIG\A").Should().Be(@"HKCC\A");
    }

    [Fact]
    public void Abbreviate_LeavesANonRegistryPathAlone()
    {
        // Scheduled-task groups go through the same header, and their paths are not registry paths.
        RegistryPathFormatter.Abbreviate(@"\Microsoft\Windows\Customer Experience Improvement Program")
            .Should().Be(@"\Microsoft\Windows\Customer Experience Improvement Program");
    }

    [Fact]
    public void Abbreviate_LeavesAnAlreadyShortPathAlone() =>
        RegistryPathFormatter.Abbreviate(@"HKLM\SOFTWARE").Should().Be(@"HKLM\SOFTWARE");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Abbreviate_TreatsNoPathAsEmpty(string? path) =>
        RegistryPathFormatter.Abbreviate(path).Should().BeEmpty();
}

using FluentAssertions;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

public class WritesGroupPolicyTests
{
    private const string PolicyKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Explorer";
    private const string PlainKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";

    [Theory]
    [InlineData("taskbar-lock-position-size", true)]
    [InlineData("taskbar-position", false)]
    public void CatalogSettings_ReportPolicyWrites(string id, bool expected) =>
        SettingCatalog.Find(id)!.WritesGroupPolicy.Should().Be(expected);

    [Fact]
    public void PolicyPathTarget_CountsWithoutTheFlag() =>
        With(new RegTarget("k", [PolicyKey], "Value", RegistryValueKind.DWord)).WritesGroupPolicy.Should().BeTrue();

    [Fact]
    public void ReadOnlyPolicyTarget_DoesNotCount() =>
        With(new RegTarget("k", [PolicyKey], "Value", RegistryValueKind.DWord) { IsGroupPolicy = true, ReadOnly = true })
            .WritesGroupPolicy.Should().BeFalse();

    [Fact]
    public void PolicyWriteEffectOnAState_Counts()
    {
        var setting = With(new RegTarget("k", [PlainKey], "Value", RegistryValueKind.DWord)) with
        {
            States =
            [
                new SettingState
                {
                    Label = TestKeys.Of("On"),
                    Effects = [new RegistryWriteEffect(PolicyKey, "Value", RegistryValueKind.DWord, 1)],
                },
            ],
        };

        setting.WritesGroupPolicy.Should().BeTrue();
    }

    [Fact]
    public void PlainTarget_DoesNotCount() =>
        With(new RegTarget("k", [PlainKey], "Value", RegistryValueKind.DWord)).WritesGroupPolicy.Should().BeFalse();

    private static Setting With(RegTarget target) => new()
    {
        Id = "test-setting",
        Display = new() { Name = TestKeys.Of("test-setting"), Description = TestKeys.Of("test-setting") },
        Targets = [target],
    };
}

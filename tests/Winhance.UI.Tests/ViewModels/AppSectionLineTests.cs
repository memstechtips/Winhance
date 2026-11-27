using FluentAssertions;
using FluentIcons.Common;
using Microsoft.UI.Xaml;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.SoftwareApps.ViewModels;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

public class AppSectionLineTests
{
    private static readonly AppSectionChrome Chrome = new(new CornerRadius(8), new Thickness(1), new Thickness(0, 1, 0, 0),
        new Thickness(12, 6, 12, 10), 2, 12);

    private static AppCategory Section(string name, int apps) => new(name, name, Icon.Apps,
        Enumerable.Range(0, apps).Select(i => new AppItemViewModel(
            new ItemDefinition { Id = $"{name}-{i}", Name = $"{name} {i}", Description = string.Empty, GroupName = name },
            Mock.Of<ILocalizationService>(l => l.GetString(It.IsAny<string>()) == string.Empty),
            Mock.Of<IDispatcherService>(), Mock.Of<IThemeService>())).ToList());

    [Fact]
    public void Split_PutsEveryAppOnALineOfAtMostTheColumnCount_InOrder()
    {
        var sections = new[] { Section("A", 9), Section("B", 4) };

        var lines = AppSectionLine.Split(sections, 4, Chrome);

        lines.Select(l => (l.Section.GroupName, l.Apps.Count, l.IsFirst, l.IsLast)).Should().Equal(
            ("A", 4, true, false), ("A", 4, false, false), ("A", 1, false, true), ("B", 4, true, true));
        lines.SelectMany(l => l.Apps).Should().Equal(sections.SelectMany(s => s.Apps));
    }

    [Fact]
    public void Split_KeepsAnEmptySectionAsItsHeader()
    {
        var line = AppSectionLine.Split([Section("Empty", 0)], 4, Chrome).Should().ContainSingle().Subject;

        line.Apps.Should().BeEmpty();
        line.Corners.Should().Be(new CornerRadius(8));
        line.Edges.Should().Be(new Thickness(1));
        line.BodyPadding.Should().Be(new Thickness(12, 6, 12, 10));
    }

    [Fact]
    public void Split_DrawsTheCardAcrossItsLines()
    {
        var lines = AppSectionLine.Split([Section("A", 9)], 4, Chrome);

        lines.Select(l => l.Corners).Should().Equal(new CornerRadius(8, 8, 0, 0), new CornerRadius(0), new CornerRadius(0, 0, 8, 8));
        lines.Select(l => l.Edges).Should().Equal(new Thickness(1, 1, 1, 0), new Thickness(1, 0, 1, 0), new Thickness(1, 0, 1, 1));
        lines.Select(l => l.Divider).Should().Equal(new Thickness(0, 1, 0, 0), default(Thickness), default(Thickness));
        lines.Select(l => l.BodyPadding).Should().Equal(
            new Thickness(12, 6, 12, 2), new Thickness(12, 0, 12, 2), new Thickness(12, 0, 12, 10));
        lines.Select(l => l.Gap.Bottom).Should().Equal(0, 0, 12);
    }

    [Fact]
    public void Split_ReusesTheLinesThatComeOutTheSame()
    {
        var a = Section("A", 9);
        var previous = AppSectionLine.Split([a], 4, Chrome);
        var withNewApp = a with { Apps = [.. a.Apps, Section("A", 1).Apps[0]] };

        var lines = AppSectionLine.Split([withNewApp], 4, Chrome, previous);

        lines[0].Should().BeSameAs(previous[0]);
        lines[1].Should().BeSameAs(previous[1]);
        lines[2].Should().NotBeSameAs(previous[2]);
        lines[2].Apps.Should().HaveCount(2);
    }

    [Fact]
    public void Split_ToNewColumns_BuildsNewLinesForTheSectionsThatRewrap()
    {
        var a = Section("A", 9);
        var b = Section("B", 2);
        var previous = AppSectionLine.Split([a, b], 4, Chrome);

        var lines = AppSectionLine.Split([a, b], 3, Chrome, previous);

        lines.Where(l => l.Section.GroupName == "A").Should().NotContain(l => previous.Contains(l, ReferenceEqualityComparer.Instance));
        lines[^1].Should().BeSameAs(previous[^1]);
    }

    [Fact]
    public void Split_ARenamedSection_BuildsNewLines()
    {
        var b = Section("B", 2);
        var previous = AppSectionLine.Split([b], 4, Chrome);

        var lines = AppSectionLine.Split([b with { DisplayName = "Renamed" }], 4, Chrome, previous);

        lines.Should().ContainSingle().Which.Should().NotBeSameAs(previous[0]);
    }
}

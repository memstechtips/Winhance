using Microsoft.UI.Xaml;

namespace Winhance.UI.Features.SoftwareApps.ViewModels;

// The compact view lists a section as its lines, not as one item: a section built as one item realized every row in
// it, a few hundred controls each, and the list could not virtualize inside it. The lines draw the section card
// between them: the first carries the header, the divider and the top corners, the last the bottom corners and the
// gap to the next section.
public sealed record AppSectionLine(AppCategory Section, IReadOnlyList<AppItemViewModel> Apps, bool IsFirst, bool IsLast)
{
    public CornerRadius Corners { get; private init; }

    public Thickness Edges { get; private init; }

    public Thickness Divider { get; private init; }

    public Thickness BodyPadding { get; private init; }

    public Thickness Gap { get; private init; }

    // A line that comes out the same as one in previous is that instance, so a list given the new lines can keep the
    // row it already built for it.
    public static List<AppSectionLine> Split(IReadOnlyList<AppCategory> sections, int columns, AppSectionChrome chrome,
        IReadOnlyList<AppSectionLine>? previous = null)
    {
        var reusable = new Dictionary<(string, AppItemViewModel?), AppSectionLine>();
        foreach (var line in previous ?? [])
            reusable.TryAdd(Key(line), line);
        var lines = new List<AppSectionLine>();
        foreach (var section in sections)
        {
            int count = Math.Max(1, (int)Math.Ceiling(section.Apps.Count / (double)columns));
            for (int i = 0; i < count; i++)
            {
                var apps = section.Apps.Skip(i * columns).Take(columns).ToList();
                var line = Line(section, apps, i == 0, i == count - 1, chrome);
                lines.Add(reusable.TryGetValue(Key(line), out var old) && old.LooksLike(line) ? old : line);
            }
        }

        return lines;
    }

    private static (string Group, AppItemViewModel? First) Key(AppSectionLine line) =>
        (line.Section.GroupName, line.Apps.Count > 0 ? line.Apps[0] : null);

    private bool LooksLike(AppSectionLine other) =>
        Section.GroupName == other.Section.GroupName && Section.DisplayName == other.Section.DisplayName
        && Section.SectionIcon == other.Section.SectionIcon
        && IsFirst == other.IsFirst && IsLast == other.IsLast && Corners == other.Corners && Edges == other.Edges
        && Divider == other.Divider && BodyPadding == other.BodyPadding && Gap == other.Gap && Apps.SequenceEqual(other.Apps);

    private static AppSectionLine Line(AppCategory section, List<AppItemViewModel> apps, bool first, bool last, AppSectionChrome c) =>
        new(section, apps, first, last)
        {
            Corners = new CornerRadius(first ? c.Radius.TopLeft : 0, first ? c.Radius.TopRight : 0,
                last ? c.Radius.BottomRight : 0, last ? c.Radius.BottomLeft : 0),
            Edges = new Thickness(c.Border.Left, first ? c.Border.Top : 0, c.Border.Right, last ? c.Border.Bottom : 0),
            Divider = first ? c.Divider : default,
            BodyPadding = new Thickness(c.Body.Left, first ? c.Body.Top : 0, c.Body.Right, last ? c.Body.Bottom : c.RowSpacing),
            Gap = new Thickness(0, 0, 0, last ? c.SectionSpacing : 0),
        };
}

public readonly record struct AppSectionChrome(CornerRadius Radius, Thickness Border, Thickness Divider, Thickness Body,
    double RowSpacing, double SectionSpacing);

using System.Text.RegularExpressions;
using Xunit;
using static Winhance.UI.Tests.Conventions.ConventionSource;

namespace Winhance.UI.Tests.Conventions;

// A control's own state is a DependencyProperty: x:Bind and the property system already track those, so a control
// that also raises PropertyChanged keeps a second change channel its bindings never needed.
public class ControlConventionTests
{
    // WinUI and toolkit types that derive from Control and appear as a base somewhere in Winhance.UI.
    private static readonly HashSet<string> ControlRoots =
        ["Control", "UserControl", "Page", "ContentControl", "ItemsControl", "ComboBox", "InfoBar", "SettingsCard"];

    private static readonly Regex ClassDeclaration = new(
        @"\bclass\s+(?<name>\w+)(?:<[^<>{]*>)?(?:\s*\([^{;]*?\))?\s*:\s*(?<bases>[^{;]+?)\s*(?:\bwhere\b|\{)");

    // The MVVM Toolkit attributes that generate the interface onto a class.
    private static readonly Regex GeneratedNotify = new(
        @"\[(?:ObservableObject|INotifyPropertyChanged)\b[^\]]*\]\s*(?:\[[^\]]*\]\s*)*(?:\w+\s+)*class\s+(?<name>\w+)");
    private static readonly Regex XamlRoot = new(@"\A[^<]*(?:<\?[^>]*>[^<]*)?<(?:\w+:)?(?<base>\w+)\b[^>]*?\bx:Class=""(?:[\w.]+\.)?(?<name>\w+)""");

    [Fact]
    public void Controls_DoNotImplementINotifyPropertyChanged()
    {
        var (bases, notifying) = Scan();

        var offenders = notifying.Where(n => IsControl(n.Name, bases, [])).Select(n => $"{n.Site} {n.Name}").ToList();

        Assert.True(offenders.Count == 0,
            "A control implements INotifyPropertyChanged; make its state DependencyProperties or bind through the "
            + "view model it shows:\n  " + string.Join("\n  ", offenders));
    }

    [Fact]
    public void Controls_TheScanReachesControlsThroughTheirBases()
    {
        var (bases, _) = Scan();

        Assert.True(IsControl("NavButton", bases, []));
        Assert.True(IsControl("SettingToggleSwitch", bases, []));
        Assert.False(IsControl("SettingItemViewModel", bases, []));
    }

    private static (Dictionary<string, HashSet<string>> Bases, List<(string Name, string Site)> Notifying) Scan()
    {
        var bases = new Dictionary<string, HashSet<string>>();
        var notifying = new List<(string Name, string Site)>();
        foreach (var file in CsUi)
        {
            var text = Text(file);
            foreach (Match m in ClassDeclaration.Matches(text))
            {
                var listed = m.Groups["bases"].Value.Split(',')
                    .Select(b => Regex.Replace(b.Trim(), @"<.*\z", string.Empty).Split('.')[^1]).ToList();
                Add(bases, m.Groups["name"].Value, listed);
                if (listed.Contains("INotifyPropertyChanged"))
                    notifying.Add((m.Groups["name"].Value, $"{file}:{text[..m.Index].Count(c => c == '\n') + 1}"));
            }
            foreach (Match m in GeneratedNotify.Matches(text))
                notifying.Add((m.Groups["name"].Value, $"{file}:{text[..m.Index].Count(c => c == '\n') + 1}"));
        }
        foreach (var file in XamlUi)
        {
            var m = XamlRoot.Match(Text(file));
            if (m.Success)
                Add(bases, m.Groups["name"].Value, [m.Groups["base"].Value]);
        }
        return (bases, notifying);
    }

    private static void Add(Dictionary<string, HashSet<string>> bases, string name, IEnumerable<string> listed)
    {
        if (!bases.TryGetValue(name, out var set))
            bases[name] = set = [];
        set.UnionWith(listed);
    }

    private static bool IsControl(string name, Dictionary<string, HashSet<string>> bases, HashSet<string> seen) =>
        seen.Add(name) && bases.TryGetValue(name, out var listed)
        && listed.Any(b => ControlRoots.Contains(b) || IsControl(b, bases, seen));
}

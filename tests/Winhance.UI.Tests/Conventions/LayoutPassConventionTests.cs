using System.Text.RegularExpressions;
using Xunit;
using static Winhance.UI.Tests.Conventions.ConventionSource;

namespace Winhance.UI.Tests.Conventions;

// WinUI refuses any change to a child collection while a measure or arrange pass runs (0x800F1000, "Child collection
// must not be modified during measure or arrange"), and building or dropping an x:Load part is one. The scan follows
// each MeasureOverride and ArrangeOverride into the methods of its file it calls and into the handlers of the events
// it raises, wherever those are subscribed.
public class LayoutPassConventionTests
{
    private static readonly Regex LayoutOverride = new(@"\boverride\s+Size\s+(?<name>(?:Measure|Arrange)Override)\s*\(");
    private static readonly Regex RaisedEvent = new(@"\b(?<event>\w+)\?\.Invoke\s*\(");
    private static readonly Regex Call = new(@"(?<![\w.])(?<name>\w+)\s*\(");
    private static readonly Regex TreeChange = new(
        @"\bUnloadObject\s*\(|\bFindName\s*\(|\bChildren\s*\.\s*(?:Add|Insert|Remove|RemoveAt|Clear|Move)\s*\(");
    private static readonly Regex TrailingWord = new(@"(?<word>\w+)\s*\z");

    private static readonly HashSet<string> NotADeclaration = ["new", "return", "await", "else", "yield", "throw", "in", "is", "as", "case"];

    [Fact]
    public void LayoutPasses_DoNotChangeTheElementTree()
    {
        var offenders = Reached().Where(m => TreeChange.IsMatch(m.Body)).Select(m => $"{m.File} {m.Name}").Distinct().ToList();

        Assert.True(offenders.Count == 0,
            "A measure or arrange pass reaches code that builds, drops or reparents an element; decide it in "
            + "SizeChanged or LayoutUpdated, once the pass is over:\n  " + string.Join("\n  ", offenders));
    }

    [Fact]
    public void LayoutPasses_TheScanReachesTheHandlersOfRaisedEvents()
    {
        Assert.Contains(Reached(), m => m.File.EndsWith("OptionMatrixView.xaml.cs", StringComparison.Ordinal) && m.Name == "OnTableMeasured");
    }

    private static List<(string File, string Name, string Body)> Reached()
    {
        var work = new Stack<(string File, string Name)>();
        foreach (var file in CsUi)
        {
            foreach (Match m in LayoutOverride.Matches(Text(file)))
                work.Push((file, m.Groups["name"].Value));
        }

        var seen = new HashSet<(string, string)>();
        var reached = new List<(string File, string Name, string Body)>();
        while (work.TryPop(out var method))
        {
            if (!seen.Add(method))
                continue;

            foreach (var body in Bodies(Text(method.File), method.Name))
            {
                reached.Add((method.File, method.Name, body));
                foreach (Match call in Call.Matches(body))
                    work.Push((method.File, call.Groups["name"].Value));
                foreach (Match raised in RaisedEvent.Matches(body))
                {
                    var subscription = new Regex(
                        @"(?<!\w)" + raised.Groups["event"].Value + @"\s*\+=\s*(?:new\s+[\w.<>]+\s*\(\s*)?(?<handler>\w+)\s*\)?\s*;");
                    foreach (var file in CsUi)
                    {
                        foreach (Match handler in subscription.Matches(Text(file)))
                            work.Push((file, handler.Groups["handler"].Value));
                    }
                }
            }
        }
        return reached;
    }

    // A declaration is the name after a type and before a parameter list that opens a block or an expression body; a
    // call has none of that, so it yields nothing.
    private static IEnumerable<string> Bodies(string t, string name)
    {
        foreach (Match m in new Regex(@"(?<![\w.])" + Regex.Escape(name) + @"\s*\(").Matches(t))
        {
            var before = t[..m.Index].TrimEnd();
            if (before.Length == 0 || !(char.IsLetterOrDigit(before[^1]) || before[^1] is '_' or '>' or ']' or '?'))
                continue;
            var word = TrailingWord.Match(before);
            if (word.Success && NotADeclaration.Contains(word.Groups["word"].Value))
                continue;

            var close = CloseParen(t, m.Index + m.Length - 1);
            var rest = t[(close + 1)..].TrimStart();
            if (rest.StartsWith('{'))
                yield return StatementAfter(t, close);
            else if (rest.StartsWith("=>", StringComparison.Ordinal))
                yield return StatementAfter(t, t.IndexOf("=>", close, StringComparison.Ordinal) + 1);
        }
    }
}

using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;
using static Winhance.UI.Tests.Conventions.ConventionSource;

namespace Winhance.UI.Tests.Conventions;

// Census rows 1, 2, 15 and 16: view models use the MVVM toolkit's generators and one shared helper per job,
// so the same notification, command, error and collection code is not written by hand in every class.
public class ViewModelConventionTests
{
    private static readonly Regex RelayCommandAttribute = new(@"\[RelayCommand(?:\((?:[^()]|\([^()]*\))*\))?\]");
    private static readonly Regex CatchAll = new(@"\bcatch\b\s*(?:\(\s*(?:System\.)?Exception\b|\{)");
    private static readonly Regex ClearCall = new(@"(?<![\w.])(?:this\.)?(\w+)\.Clear\(\s*\)\s*;");
    private static readonly Regex Loop = new(@"\b(?:foreach|for)\s*\(");
    private static readonly Regex LanguageHandler = new(@"\bLanguageChanged\s*\+=\s*(\w+)\s*;");
    private static readonly Regex ObservableField = new(@"\bObservableCollection<(?:[^<>]|<[^<>]*>)*>\??\s+(\w+)\s*(?:[{=;]|=>)");
    private static readonly Regex RaiseCall = new(@"(?<!void )\b(?:OnPropertyChanged|SetProperty)\s*\(|\bPropertyChanged\?\.Invoke\s*\(");
    private static readonly Regex MethodStart = new(@"(?<![\w.])(\w+(?:<(?:[^<>]|<[^<>]*>)*>)?|\((?:[^()]|\([^()]*\))*\))(?:\[\])?\??\s+(\w+)\s*(?:<[^<>()]*>)?\s*\(");
    private static readonly Regex SetterStart = new(@"\bset\s*(?=\{|=>)");
    // Before a parenthesised MethodStart type: the match is if (cond) Call(, not a tuple-returning declaration.
    private static readonly Regex ControlHead = new(@"\b(?:if|while|for|foreach|switch|using|lock|fixed|catch|when)\s*$");
    private static readonly HashSet<string> NotATypeName =
    [
        "return", "new", "await", "class", "record", "struct", "interface", "else", "case", "throw", "in", "is", "as",
        "using", "yield", "goto", "when", "where", "nameof", "typeof", "sizeof", "default", "if", "for", "foreach",
        "while", "switch", "catch", "lock", "fixed",
    ];
    private static readonly Regex HandRaise = new(@"\bOnPropertyChanged\s*\(\s*nameof\s*\(\s*(\w+)\s*\)");
    private static readonly Regex LocalCall = new(@"(?:(?<![\w.])|\bthis\.)(\w+)\s*\(");
    private static readonly Lazy<RaisingMethods> Raisers = new(FindRaisingMethods);

    [Fact]
    public void HandWrittenPropertyNotification_MatchesTheLedger()
    {
        // A raise (a call to OnPropertyChanged( or SetProperty(, or PropertyChanged?.Invoke() plus every call to a
        // raising method (see FindRaisingMethods), so a helper that wraps the raise counts at each call site.
        var actual = new Dictionary<string, int>();
        foreach (var file in CsAll)
        {
            var t = Text(file);
            var count = RaiseCall.Count(t);
            var names = Raisers.Value.In(file);
            if (names.Count > 0)
            {
                var declared = MethodStart.Matches(t).Where(m => !NotATypeName.Contains(m.Groups[1].Value)
                        && !(t[m.Index] == '(' && ControlHead.IsMatch(t[Math.Max(0, m.Index - 16)..m.Index])))
                    .Select(m => m.Groups[2].Index).ToHashSet();
                count += CallTo(names).Matches(t).Count(m => !declared.Contains(m.Index));
            }
            if (count > 0)
                actual[file] = count;
        }

        Ledger.AssertMatches("PropertyNotification", actual,
            "use [ObservableProperty] with [NotifyPropertyChangedFor] instead of a hand-written notifying setter");
    }

    [Fact]
    public void SettersCallingARaisingMethod_MatchTheLedger()
    {
        // A set { } or set => body that calls a raising method (see FindRaisingMethods). Each call counts.
        var actual = new Dictionary<string, int>();
        foreach (var file in CsAll)
        {
            var names = Raisers.Value.In(file);
            if (names.Count == 0)
                continue;
            var t = Text(file);
            var call = CallTo(names);
            var count = 0;
            foreach (Match m in SetterStart.Matches(t))
            {
                var at = m.Index + m.Length;
                count += call.Count(t[at] == '{' ? MatchBody(t, at) : StatementAfter(t, at + 1));
            }
            if (count > 0)
                actual[file] = count;
        }

        Ledger.AssertMatches("RaisingSetter", actual,
            "make it an [ObservableProperty] with [NotifyPropertyChangedFor] or [NotifyCanExecuteChangedFor], and a partial OnXChanged for a side effect");
    }

    [Fact]
    public void RelayCommandConstruction_MatchesTheLedger()
    {
        // new RelayCommand / new AsyncRelayCommand, generic or not. Target-typed new() is not caught.
        var actual = Count(CsAll, @"\bnew\s+(?:Async)?RelayCommand\b");

        Ledger.AssertMatches("RelayCommandConstruction", actual, "use [RelayCommand] on a method instead");
    }

    [Fact]
    public void CommandErrorScaffold_MatchesTheLedger()
    {
        // A [RelayCommand] method whose body catches Exception (or uses a bare catch) is a copy of the error policy, and
        // so is a method of the same file the command calls directly. Each such method counts once.
        var actual = new Dictionary<string, int>();
        foreach (var file in CsAll)
        {
            var t = Text(file);
            var methods = MethodsIn(t).ToLookup(m => m.Name, m => m.Body);
            var commands = new Dictionary<string, string>();
            foreach (Match m in RelayCommandAttribute.Matches(t))
            {
                var open = t.IndexOf('(', m.Index + m.Length);
                if (open < 0)
                    continue;
                var name = Regex.Match(t[..open], @"(\w+)\s*$").Groups[1].Value;
                commands.TryAdd(name, StatementAfter(t, CloseParen(t, open)));
            }
            var callees = CalledIn(commands.Values, methods).Where(n => !commands.ContainsKey(n)).Distinct();
            var count = commands.Values.Count(CatchAll.IsMatch)
                + callees.Count(n => methods[n].Any(CatchAll.IsMatch));
            if (count > 0)
                actual[file] = count;
        }

        Ledger.AssertMatches("CommandErrorScaffold", actual,
            "let the shared async-command error policy log and report instead of a try/catch in the command");
    }

    [Fact]
    public void ClearThenAdd_MatchesTheLedger()
    {
        // X.Clear(); on an ObservableCollection declared in the same file, followed in the same block by a for or
        // foreach loop that calls X.Add(. Each Clear counts once. Clear followed by Adds with no loop is not caught.
        var actual = new Dictionary<string, int>();
        foreach (var file in CsAll)
        {
            var t = Text(file);
            var collections = ObservableField.Matches(t).Select(d => d.Groups[1].Value).ToHashSet();
            var count = 0;
            foreach (Match m in ClearCall.Matches(t))
            {
                var name = m.Groups[1].Value;
                if (!collections.Contains(name))
                    continue;
                var start = m.Index + m.Length;
                var region = t[start..BlockEnd(t, start)];
                var add = new Regex(@"(?<![\w.])" + Regex.Escape(name) + @"\.Add\(");
                if (Loop.Matches(region).Any(l => add.IsMatch(StatementAfter(region, CloseParen(region, l.Index + l.Length - 1)))))
                    count++;
            }
            if (count > 0)
                actual[file] = count;
        }

        Ledger.AssertMatches("ClearThenAdd", actual,
            "replace the collection contents with the shared batch-replace helper instead of Clear() then Add in a loop");
    }

    [Fact]
    public void LanguageChangedHandlers_RaiseTheirTextFromOneList()
    {
        // A method subscribed as X.LanguageChanged += Name; in a file must not raise OnPropertyChanged(nameof(...))
        // itself; the names live in one static array the handler loops over. The same holds for a text property
        // raised by hand in any method of the file the handler reaches; a non-text raise there is state, not text.
        var handlers = 0;
        var offenders = new List<string>();
        foreach (var file in CsAll)
        {
            var t = Text(file);
            var methods = MethodsIn(t).ToLookup(m => m.Name, m => m.Body);
            foreach (var name in LanguageHandler.Matches(t).Select(m => m.Groups[1].Value).Distinct())
            {
                if (!methods.Contains(name))
                    continue;
                handlers++;
                if (methods[name].Any(b => b.Contains("OnPropertyChanged(nameof(", StringComparison.Ordinal)))
                    offenders.Add($"{file}: {name}");
                var seen = new HashSet<string> { name };
                var todo = new Stack<string>(CalledIn(methods[name], methods));
                while (todo.TryPop(out var reached))
                {
                    if (!seen.Add(reached))
                        continue;
                    foreach (var raised in methods[reached].SelectMany(b => HandRaise.Matches(b)).Select(m => m.Groups[1].Value))
                    {
                        if (new Regex(@"(?:\bstring\??|<string\??>\??)\s+" + raised + @"\s*(?:=>|\{)").IsMatch(t))
                            offenders.Add($"{file}: {name} -> {reached} raises {raised}");
                    }
                    foreach (var next in CalledIn(methods[reached], methods))
                        todo.Push(next);
                }
            }
        }

        handlers.Should().BeGreaterThan(5);
        offenders.Should().BeEmpty(
            "a language-change handler should list the names in a static LocalizedProperties array and raise them in a loop");
    }

    private sealed record RaisingMethods(Dictionary<string, HashSet<string>> ByFile, HashSet<string> Shared)
    {
        public HashSet<string> In(string file) => [.. Shared, .. ByFile.GetValueOrDefault(file) ?? []];
    }

    // A raising method is one whose body raises PropertyChanged. It counts in its own file, and in every file when no
    // method of that name anywhere has a body that does not raise, so a call on another object still counts.
    private static RaisingMethods FindRaisingMethods()
    {
        var byFile = new Dictionary<string, HashSet<string>>();
        var plain = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in CsAll)
        {
            foreach (var (name, _, body) in MethodsIn(Text(file)))
            {
                if (!RaiseCall.IsMatch(body))
                {
                    plain.Add(name);
                    continue;
                }
                if (!byFile.TryGetValue(file, out var names))
                    byFile[file] = names = new HashSet<string>(StringComparer.Ordinal);
                names.Add(name);
            }
        }
        var shared = byFile.Values.SelectMany(s => s).Where(n => !plain.Contains(n)).ToHashSet(StringComparer.Ordinal);
        return new RaisingMethods(byFile, shared);
    }

    // Every method declared with a body: its name, where the name sits, and the body.
    private static IEnumerable<(string Name, int At, string Body)> MethodsIn(string t)
    {
        foreach (Match m in MethodStart.Matches(t))
        {
            if (NotATypeName.Contains(m.Groups[1].Value) || NotATypeName.Contains(m.Groups[2].Value)
                || m.Groups[2].Value is "OnPropertyChanged" or "SetProperty")
                continue;
            var close = CloseParen(t, m.Index + m.Length - 1);
            var next = close + 1;
            while (next < t.Length && char.IsWhiteSpace(t[next]))
                next++;
            if (next < t.Length && (t[next] == '{' || string.CompareOrdinal(t, next, "=>", 0, 2) == 0))
                yield return (m.Groups[2].Value, m.Groups[2].Index, StatementAfter(t, close));
        }
    }

    private static IEnumerable<string> CalledIn(IEnumerable<string> bodies, ILookup<string, string> methods) =>
        bodies.SelectMany(b => LocalCall.Matches(b)).Select(m => m.Groups[1].Value).Where(methods.Contains);

    private static Regex CallTo(IEnumerable<string> names) =>
        new(@"(?<!\w)(?:" + string.Join("|", names.Order(StringComparer.Ordinal).Select(Regex.Escape)) + @")\s*\(");
}

using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;
using static Winhance.UI.Tests.Conventions.ConventionSource;

namespace Winhance.UI.Tests.Conventions;

// Guards the ledgers themselves: they parse (Ledger.Read refuses a reason it does not know, "migrate" included),
// each belongs to a rule that is still checked, and every reason says what it allows.
public class LedgerConventionTests
{
    private static readonly string[] Rules =
    [
        "CatchLogRethrow", "ClearThenAdd", "CommandErrorScaffold", "ContentDialogConstruction", "DeadConverter",
        "DiscardedTask", "GetService", "ItemsControl", "LambdaSubscription", "NavigationCacheModeInCode",
        "NonFluentIcon", "PreferenceAsyncRead", "PrivateLocalizationHelper", "PropertyNotification", "RaisingSetter",
        "RawDebounce", "ReflectionBinding", "RelayCommandConstruction", "ReleaseDroppedMemory", "ShowMessage", "TryEnqueue",
        "UnpairedSubscription", "UnsizedImageDecode", "Visibility",
    ];

    private static IEnumerable<string> LedgerRules() =>
        Directory.EnumerateFiles(Ledger.Dir, "*.ledger").Select(f => Path.GetFileNameWithoutExtension(f));

    [Fact]
    public void SourceScan_FindsTheSourceTree()
    {
        CsAll.Should().HaveCountGreaterThan(500);
        XamlAll.Should().HaveCountGreaterThan(40);
        CsUi.Should().HaveCountGreaterThan(150);
        XamlUi.Should().HaveCountGreaterThan(40);
    }

    [Fact]
    public void Ledgers_AreWellFormedAndBelongToARule()
    {
        LedgerRules().Should().NotBeEmpty().And.BeSubsetOf(Rules);
        foreach (var rule in LedgerRules())
        {
            foreach (var entry in Ledger.Read(rule))
                File.Exists(Path.Combine(Root, entry.Path)).Should().BeTrue($"{rule}.ledger names {entry.Path}");
        }
    }

    [Fact]
    public void LedgerReasons_EachSitUnderTheirOwnDefinition()
    {
        var lines = File.ReadAllLines(Path.Combine(Root, "tests", "Winhance.UI.Tests", "Conventions", "Ledger.cs"));
        var start = Array.FindIndex(lines, l => l.Contains("Reasons = new HashSet<string>", StringComparison.Ordinal));
        var end = Array.FindIndex(lines, start, l => l.Trim() == "};");
        var found = 0;
        var undefined = new List<string>();
        for (var i = start + 1; i < end; i++)
        {
            if (lines[i].TrimStart().StartsWith("//", StringComparison.Ordinal))
                continue;
            var literals = Regex.Matches(lines[i], "\"([^\"]*)\"");
            found += literals.Count;
            if (literals.Count > 1 || (literals.Count == 1 && !lines[i - 1].TrimStart().StartsWith("//", StringComparison.Ordinal)))
                undefined.AddRange(literals.Select(m => m.Groups[1].Value));
        }

        found.Should().Be(Ledger.Reasons.Count);
        undefined.Should().BeEmpty("each reason sits alone on its line, under a // line saying which sites it allows");
    }

    [Fact]
    public void LedgerReasons_AreEachUsedByALedger()
    {
        var used = LedgerRules().SelectMany(Ledger.Read).Select(e => e.Reason).ToHashSet(StringComparer.Ordinal);

        Ledger.Reasons.Where(r => !used.Contains(r)).Should().BeEmpty("a reason no ledger line uses allows nothing; delete it");
    }

    [Fact]
    public void CollectionSubclass_NamesOnlyAnObservableCollectionWithoutObservableObject()
    {
        foreach (var entry in Ledger.Read("PropertyNotification").Where(e => e.Reason == "collection-subclass"))
        {
            var text = Text(entry.Path);
            text.Should().Contain(": ObservableCollection<", $"{entry.Path} is ledgered collection-subclass");
            text.Should().NotContain("ObservableObject", $"{entry.Path} could use [ObservableProperty]");
        }
    }
}

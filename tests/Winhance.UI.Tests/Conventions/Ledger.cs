using FluentAssertions;
using Winhance.TestSupport;

namespace Winhance.UI.Tests.Conventions;

// A ledger lists the sites a convention still allows: one line per file and reason, "<repo path>\t<count>\t<reason>".
// The count must match exactly, so a new site fails and so does a fixed one until its line is lowered or deleted.
// A rule with no ledger file allows nothing. There is no "migrate" reason: a site that breaks a rule is fixed, not
// ledgered for later.
internal static class Ledger
{
    public static readonly string Dir =
        Path.Combine(RepoPaths.SolutionDir(), "tests", "Winhance.UI.Tests", "Conventions", "Ledgers");

    public static readonly IReadOnlySet<string> Reasons = new HashSet<string>(StringComparer.Ordinal)
    {
        // A label FitToWidth hides when its row does not fit: the fit is measured with the label shown, so it has to
        // exist.
        "measured-fit",
        // A setting card bound to EffectiveIsVisible, or a group header bound to HasVisibleItems: x:Load there tore
        // down and rebuilt dozens of cards on every search keystroke, and loading a header whose list had already
        // left the page threw inside a timer tick, both of which crashed XAML.
        "search-filter",
        // A Visibility a VisualState sets, by Setter or key frame, and that element's starting value: a state sets
        // dependency properties only, and x:Load is not one.
        "visual-state",
        // A page's main content (the overview scroller, the settings list) collapsed only while its RocketLoader runs:
        // it shows whenever the page is not loading, and its scroller and items are set up while it waits.
        "loading-curtain",
        // The WIMUtil step body still loads on first use. Tearing down its shared content host on collapse
        // can leave the header open with no body the next time.
        "preserve-step-content",
        // An item or separator of a MenuFlyout: its parent is the flyout, not a panel, so x:Load cannot go on it.
        "menu-item",
        // A += with no -= on a source that cannot outlive the subscriber: one it owns (its own child control, a
        // dialog's own button, a collection or tray icon it created), or one that lasts the whole process as the
        // subscriber does (App, the one MainWindow, a singleton service on another singleton).
        "same-lifetime",
        // A raise of this object's own property written in a handler of ANOTHER object's PropertyChanged, event,
        // event-bus message or change callback (the subscribed method, a lambda in it, or the method it hands its
        // sender and args to); no setter runs. Any other call to a raising method takes the reason of the raise it reaches.
        "forwarded-change",
        // A repeating timer that is not a debounce (a poll or a clock), so CreateDebounceTimer does not fit.
        "periodic-timer",
        // A Task.Delay raced in Task.WhenAny as the upper bound on a wait, which goes on either way when it runs out.
        "timeout",
        // A one-shot Task.Delay nothing restarts or cancels (a splash's minimum shown time, a diagnostics count once a
        // page settles), so there is no burst for a debounce to fold.
        "fixed-wait",
        // A property over a collection (the collection itself, or a value computed from its items), raised outside a
        // forwarded-change handler after the items changed in place, or by a helper such a handler shares.
        "collection-projection",
        // A Low-priority TryEnqueue that must run after the pending layout pass; RunOnUIThread runs inline on the UI thread.
        "after-layout",
        // A property computed live off a service or another view model rather than stored, raised outside a
        // forwarded-change handler by the code that changed or refilled that source, or by a helper such a handler shares.
        "live-computed",
        // A write that keeps a value it was given (a setter that refuses some values, a declined or rejected write, a
        // cancel) and raises the kept property, so the bound control reads it back.
        "refused-value",
        // A write that must not fire the setter's change callback, because the caller is already inside the change it
        // reports, so it sets the field through SetProperty directly.
        "silent-write",
        // A property on a class that has to derive from ObservableCollection (a group of a CollectionViewSource with
        // IsSourceGrouped), which has no ObservableObject base for [ObservableProperty], so it raises through the
        // collection's own OnPropertyChanged(PropertyChangedEventArgs).
        "collection-subclass",
        // A catch filtered to one expected failure (a file the user picked cannot be written) that shows its own message.
        "specific-failure",
        // A delay whose token also cancels the work it starts (a package search), which a debounce timer cannot.
        "cancellable-search",
        // A property read through a nullable state object that one assignment creates or drops, so no backing field exists to generate.
        "overlay-projection",
        // A command catch that turns its failure into the command's own on-screen state (an InfoBar message), which
        // the shared error dialog would duplicate.
        "own-failure-state",
        // A method a command calls to answer a question about the PC (is this folder usable, which image the media
        // holds), where a failure means no and the command shows that answer instead of an error.
        "failure-is-answer",
        // A release right where a page or view drops a large element tree outside a frame's Navigated handler (a mode
        // change, a view switch, going back to a section page's overview).
        "drop-point",
        // A release in a frame's *_Navigated handler: every navigation drops the page it leaves, cached ones included.
        "navigation-hook",
        // A Clear() then Add loop on the group collection of a CollectionViewSource with IsSourceGrouped: WinUI's grouped
        // view takes Reset and Add, and an indexed Replace of a group from the batch-replace helper crashed XAML natively.
        "grouped-source",
        // An ItemsControl over a short fixed set (a catalog's own fields, Windows' colours), named in its Bounded: comment.
        "bounded-set",
        // An ItemsControl inside a SettingsListView item or header, named in its Nested: comment: the outer list
        // virtualizes, and an inner ListView would measure at infinite height and add a second scroller (#439).
        "nested-in-list",
        // A catch of OperationCanceledException that writes the cancel to the log (and undoes what the run left, when
        // it did any) before passing the cancel on.
        "cancel-log",
        // A catch in startup code (Program, App, the first pages) that writes to StartupLogger, the file log that
        // exists before the log service does.
        "startup-log",
    };

    public sealed record Entry(string Path, int Count, string Reason);

    public static List<Entry> Read(string rule)
    {
        var file = Path.Combine(Dir, rule + ".ledger");
        if (!File.Exists(file))
            return [];

        var entries = new List<Entry>();
        var lines = File.ReadAllText(file).Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].Length == 0)
                continue;
            var fields = lines[i].Split('\t');
            if (fields.Length != 3 || fields[0].Length == 0 || !int.TryParse(fields[1], out var count) || count <= 0)
                throw new InvalidDataException($"{rule}.ledger line {i + 1}: expected <path>\\t<positive count>\\t<reason>");
            if (!Reasons.Contains(fields[2]))
                throw new InvalidDataException($"{rule}.ledger line {i + 1}: unknown reason '{fields[2]}'");
            if (entries.Any(e => e.Path == fields[0] && e.Reason == fields[2]))
                throw new InvalidDataException($"{rule}.ledger line {i + 1}: {fields[0]} already has a '{fields[2]}' line");
            entries.Add(new Entry(fields[0], count, fields[2]));
        }
        return entries;
    }

    public static void AssertMatches(string rule, IReadOnlyDictionary<string, int> actual, string fix)
    {
        var allowed = Read(rule).GroupBy(e => e.Path).ToDictionary(g => g.Key, g => g.Sum(e => e.Count));
        var problems = new List<string>();
        foreach (var path in actual.Keys.Union(allowed.Keys).Order(StringComparer.Ordinal))
        {
            var found = actual.GetValueOrDefault(path);
            var expected = allowed.GetValueOrDefault(path);
            if (found > expected)
                problems.Add($"new violation in {path} ({found} found, ledger allows {expected}): {fix}");
            else if (found < expected)
                problems.Add($"{path} now has fewer ({found} found, ledger says {expected}): lower or delete its line in Ledgers/{rule}.ledger");
        }
        problems.Should().BeEmpty();
    }

    // The sites that have a reason's shape must be exactly the sites its lines cover, so no site sits under a reason
    // whose definition does not fit it.
    public static void AssertReason(string rule, string reason, IReadOnlyDictionary<string, int> shaped)
    {
        var ledgered = Read(rule).Where(e => e.Reason == reason).ToDictionary(e => e.Path, e => e.Count);
        var problems = shaped.Keys.Union(ledgered.Keys).Order(StringComparer.Ordinal)
            .Where(path => shaped.GetValueOrDefault(path) != ledgered.GetValueOrDefault(path))
            .Select(path => $"{path}: {shaped.GetValueOrDefault(path)} sites have the {reason} shape, Ledgers/{rule}.ledger gives it {ledgered.GetValueOrDefault(path)}")
            .ToList();
        problems.Should().BeEmpty();
    }
}

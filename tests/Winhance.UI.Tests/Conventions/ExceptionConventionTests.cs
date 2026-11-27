using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;
using static Winhance.UI.Tests.Conventions.ConventionSource;

namespace Winhance.UI.Tests.Conventions;

// A catch that only logs and rethrows adds a second copy of a failure the caller already handles. The one place it
// is required is a dispatcher callback: an exception thrown there ends the process before any caller sees it.
public class ExceptionConventionTests
{
    private const string DispatcherService = Ui + "Features/Common/Services/DispatcherService.cs";

    private static readonly Regex CatchHeader = new(@"\bcatch\b[^{;]*\{");
    private static readonly Regex LogCall = new(@"(?<!\w)(?:[\w?]+\.)*Log\w*\s*\(");
    private static readonly Regex Rethrow = new(@"\bthrow\s*(?:\w+\s*)?;");

    [Fact]
    public void CatchLogRethrow_MatchesTheLedger()
    {
        Ledger.AssertMatches("CatchLogRethrow", LogRethrowCatches(_ => true),
            "let the exception reach the caller that handles it; only DispatcherService logs and rethrows");
        Ledger.AssertReason("CatchLogRethrow", "cancel-log",
            LogRethrowCatches(c => Regex.IsMatch(c, @"\Acatch\s*\(\s*OperationCanceledException\s*\)\s*\{")));
        Ledger.AssertReason("CatchLogRethrow", "startup-log",
            LogRethrowCatches(c => c.Contains("StartupLogger.Log(", StringComparison.Ordinal)));
    }

    [Fact]
    public void DispatcherCallbacks_GoThroughLogged()
    {
        // Every queued callback and timer tick, except an async lambda, whose failure already reaches its awaiting
        // caller through the TaskCompletionSource it completes.
        Count([DispatcherService], @"(?:\.TryEnqueue\s*\(|\.Tick\s*\+=)(?![^;]*\basync\b)(?![^;]*\bLogged\s*\()")
            .Should().BeEmpty("a callback that throws on the dispatcher must be logged before it ends the process");
    }

    // Each catch block under src/ outside DispatcherService that calls a Log* method and rethrows, counted per file
    // when the catch (header and body) has the given shape.
    private static Dictionary<string, int> LogRethrowCatches(Func<string, bool> shape)
    {
        var counts = new Dictionary<string, int>();
        foreach (var file in CsAll.Where(f => f != DispatcherService))
        {
            var t = Text(file);
            var count = CatchHeader.Matches(t)
                .Select(m => (Header: m.Value, Body: MatchBody(t, m.Index + m.Length - 1)))
                .Count(c => LogCall.IsMatch(c.Body) && Rethrow.IsMatch(c.Body) && shape(c.Header[..^1] + c.Body));
            if (count > 0)
                counts[file] = count;
        }
        return counts;
    }
}

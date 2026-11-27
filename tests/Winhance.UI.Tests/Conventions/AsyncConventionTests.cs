using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;
using static Winhance.UI.Tests.Conventions.ConventionSource;

namespace Winhance.UI.Tests.Conventions;

// Census rows 6, 12 and 13: a task nobody awaits goes through FireAndForget so its exception is logged, async void
// is kept to event handlers that catch their own errors, and debouncing goes through the dispatcher service.
public class AsyncConventionTests
{
    private const string DispatcherService = Ui + "Features/Common/Services/DispatcherService.cs";
    private const string TaskExtensions = "src/Winhance.Core/Features/Common/Extensions/TaskExtensions.cs";

    // An async void method, or an async lambda handed straight to an event.
    private static readonly Regex AsyncVoid = new(@"\basync\s+void\s+\w+\s*\(|\+=\s*async\b");
    private static readonly Regex HandlerParameters = new(@"\A\s*object\??\s+\w+\s*,\s*[\w.]*EventArgs\??\s+\w+\s*\z");
    private static readonly Regex Catch = new(@"\Acatch\b");
    private static readonly Regex CatchAll = new(@"\Acatch\s*(?:\(\s*(?:System\.)?Exception(?:\s+\w+)?\s*\))?\s*\z");

    [Fact]
    public void DiscardedTasks_MatchTheLedger()
    {
        // _ = Foo.BarAsync( or _ = Task.Run(. A generic or chained call before the discard's last segment is not caught.
        var actual = Count(CsAll, @"(?<![\w.])_\s*=\s*[\w.?]*?(?:Async|Task\.Run)\s*\(");

        Ledger.AssertMatches("DiscardedTask", actual, "call .FireAndForget(log) so a failure is logged");
    }

    [Fact]
    public void AsyncVoid_IsOnlyACatchAllEventHandler()
    {
        // FireAndForget itself is the one async void the rule exists to funnel into.
        var sites = CsAll.Where(f => f != TaskExtensions)
            .SelectMany(file => AsyncVoid.Matches(Text(file))
                .Where(m => !IsCatchAllHandler(Text(file), m))
                .Select(m => $"{file}:{Text(file).AsSpan(0, m.Index).Count('\n') + 1}"));

        sites.Should().BeEmpty(
            "async void is only an (object sender, XEventArgs e) handler whose whole body is one try with a catch-all; anything else returns Task and is started with FireAndForget(log)");
    }

    [Theory]
    [InlineData("async void A(object sender, RoutedEventArgs e) { try { await X(); } catch (Exception ex) { Log(ex); } }", true)]
    [InlineData("async void A(object? sender, EventArgs e) { try { await X(); } catch { } finally { Y(); } }", true)]
    [InlineData("async void A(object sender, RoutedEventArgs e) { try { await X(); } catch (IOException ex) { } catch (Exception ex) { } }", true)]
    [InlineData("async void A(Foo sender, RoutedEventArgs e) { try { await X(); } catch (Exception ex) { } }", false)]
    [InlineData("async void A(object sender, RoutedEventArgs e) { Y(); try { await X(); } catch (Exception ex) { } }", false)]
    [InlineData("async void A(object sender, RoutedEventArgs e) { try { await X(); } catch (IOException ex) { } }", false)]
    [InlineData("async void A(object sender, RoutedEventArgs e) { try { await X(); } catch (Exception ex) when (ex is IOException) { } }", false)]
    [InlineData("async void A() { try { await X(); } catch { } }", false)]
    [InlineData("async void A(object sender, RoutedEventArgs e) => await X();", false)]
    [InlineData("Click += async (s, e) => { try { await X(); } catch { } };", false)]
    public void AsyncVoid_AllowsOnlyTheCatchAllHandlerShape(string code, bool allowed) =>
        IsCatchAllHandler(code, AsyncVoid.Match(code)).Should().Be(allowed);

    // The body splits into the header before each top-level block: "try", then catch or finally headers, with nothing
    // outside them. An exception filter (when) is not a catch-all.
    private static bool IsCatchAllHandler(string t, Match m)
    {
        if (!m.Value.EndsWith('('))
            return false;
        var open = m.Index + m.Length - 1;
        var close = CloseParen(t, open);
        var body = StatementAfter(t, close);
        if (!HandlerParameters.IsMatch(t[(open + 1)..close]) || !body.StartsWith('{'))
            return false;

        var rest = body[1..^1];
        var headers = new List<string>();
        while (rest.Trim().Length > 0)
        {
            var brace = rest.IndexOf('{');
            if (brace < 0)
                return false;
            headers.Add(rest[..brace].Trim());
            rest = rest[(brace + MatchBody(rest, brace).Length)..];
        }
        return headers.Count > 1 && headers[0] == "try"
            && headers.Skip(1).All(h => Catch.IsMatch(h) || h == "finally")
            && headers.Any(CatchAll.IsMatch);
    }

    [Fact]
    public void RawDebounce_MatchesTheLedger()
    {
        // In UI code outside DispatcherService: every DispatcherQueue.CreateTimer(, Task.Delay(, new PeriodicTimer( and
        // DispatcherTimer, so a debounce cannot hide as a plain wait; the ledger names each that is not a debounce.
        var timers = Merge(
            Count(CsUi, @"\.CreateTimer\s*\(", DispatcherService),
            Count(CsUi, @"\bnew\s+PeriodicTimer\s*\(", DispatcherService),
            Count(CsUi, @"\bDispatcherTimer\b", DispatcherService));
        var delays = Count(CsUi, @"\bTask\.Delay\s*\(", DispatcherService);

        Ledger.AssertMatches("RawDebounce", Merge(timers, delays), "debounce with IDispatcherService.CreateDebounceTimer");
        // A timer in a file that sets IsRepeating = false is a hand-built debounce, never a periodic one.
        Ledger.AssertReason("RawDebounce", "periodic-timer",
            timers.Where(t => !Regex.IsMatch(Text(t.Key), @"\bIsRepeating\s*=\s*false\b")).ToDictionary());
        Ledger.AssertReason("RawDebounce", "timeout", Count(CsUi, @"\bTask\.WhenAny\s*\([^;]*?\bTask\.Delay\s*\(", DispatcherService));
        Ledger.AssertReason("RawDebounce", "cancellable-search", Count(CsUi, @"\bTask\.Delay\s*\((?:[^()]|\([^()]*\))*,", DispatcherService));
    }
}

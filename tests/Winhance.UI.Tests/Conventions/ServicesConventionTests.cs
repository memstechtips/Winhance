using System.Text.RegularExpressions;
using Xunit;
using static Winhance.UI.Tests.Conventions.ConventionSource;

namespace Winhance.UI.Tests.Conventions;

// Census rows 4, 5, 7, 8, 17, 19, 20 and 21: each cross-cutting job (dialogs, dispatch, service lookup, page
// caching, localization, messaging, heap release, preferences) has one entry point, and these count the bypasses.
public class ServicesConventionTests
{
    private static readonly Regex NamedSubscription = new(
        @"(?<![\w.?)>])(?<source>(?:\w+(?:<[^<>;]*>)?(?:\([^()]*\))?\??\.)*(?<event>\w+))\s*\+=\s*(?:new\s+[\w.<>]+\s*\(\s*)?(?<handler>\w+)\s*\)?\s*;");
    private static readonly Regex VoidMethod = new(@"\bvoid\s+(\w+)\s*\(");
    private static readonly Regex OwnEvent = new(@"\A(?:this\.)?\w+\z");

    [Fact]
    public void ContentDialogConstruction_MatchesTheLedger()
    {
        // new ContentDialog (qualified or not) outside DialogService and the Dialogs/*DialogBuilder classes it uses.
        var exempt = CsUi.Where(f => f == Ui + "Features/Common/Services/DialogService.cs"
            || Regex.IsMatch(f, @"\Asrc/Winhance\.UI/Features/Common/Dialogs/\w+DialogBuilder\.cs\z"));

        Ledger.AssertMatches("ContentDialogConstruction",
            Count(CsAll, @"\bnew\s+(?:[\w.]+\.)?ContentDialog\b", [.. exempt]),
            "show the dialog through IDialogService");
    }

    [Fact]
    public void ShowMessage_MatchesTheLedger()
    {
        // Any ShowMessage( call or declaration: the fire-and-forget twin of ShowInformationAsync.
        Ledger.AssertMatches("ShowMessage", Count(CsAll, @"\bShowMessage\s*\("), "await ShowInformationAsync instead");
    }

    [Fact]
    public void RawTryEnqueue_MatchesTheLedger()
    {
        // .TryEnqueue( outside DispatcherService.
        var actual = Count(CsAll, @"\.TryEnqueue\s*\(", Ui + "Features/Common/Services/DispatcherService.cs");

        Ledger.AssertMatches("TryEnqueue", actual, "dispatch through IDispatcherService");
    }

    [Fact]
    public void GetService_MatchesTheLedger()
    {
        // GetService<T>( or GetService(typeof ...). GetRequiredService does not match.
        Ledger.AssertMatches("GetService", Count(CsAll, @"\bGetService\s*[<(]"),
            "use GetRequiredService so a missing registration fails loudly");
    }

    [Fact]
    public void NavigationCacheModeInCode_MatchesTheLedger()
    {
        Ledger.AssertMatches("NavigationCacheModeInCode", Count(CsAll, @"\bNavigationCacheMode\s*=(?!=)"),
            "set NavigationCacheMode on the page in XAML");
    }

    [Fact]
    public void PrivateLocalizationHelpers_MatchTheLedger()
    {
        // A private, internal or protected string-returning Localized(, Localize( or ResolveLocalized( method.
        var actual = Count(CsAll,
            @"\b(?:private|internal|protected)\s+(?:static\s+)?string\??\s+(?:Localized|Localize|ResolveLocalized)\s*\(");

        Ledger.AssertMatches("PrivateLocalizationHelper", actual, "call the shared localization helper");
    }

    [Fact]
    public void LambdaSubscriptions_MatchTheLedger()
    {
        // In UI code: += followed by a lambda (optionally async, with or without parentheses) or an anonymous delegate.
        // A lambda cannot be unsubscribed, so a long-lived source keeps the subscriber alive.
        var actual = Count(CsUi, @"\+=\s*(?:async\s+)?(?:\((?:[^()]|\([^()]*\))*\)|\w+)\s*=>|\+=\s*delegate\b");

        Ledger.AssertMatches("LambdaSubscription", actual,
            "publish through IEventBus, or subscribe a named handler that is removed when the subscriber goes away");
    }

    [Fact]
    public void UnpairedSubscriptions_MatchTheLedger()
    {
        // In UI code: Source.Event += Handler; where Handler is a void method of the file and no Event -= Handler
        // appears anywhere in it. An object subscribing to its own event cannot be kept alive by it, so a bare
        // Event += Handler is left out; a source reached through a cast in parentheses is not caught.
        var actual = new Dictionary<string, int>();
        foreach (var file in CsUi)
        {
            var t = Text(file);
            var methods = VoidMethod.Matches(t).Select(m => m.Groups[1].Value).ToHashSet();
            var count = NamedSubscription.Matches(t).Count(m =>
                methods.Contains(m.Groups["handler"].Value)
                && !OwnEvent.IsMatch(m.Groups["source"].Value)
                && !Regex.IsMatch(t, $@"\b{m.Groups["event"].Value}\s*-=\s*(?:new\s+[\w.<>]+\s*\(\s*)?{m.Groups["handler"].Value}\b"));
            if (count > 0)
                actual[file] = count;
        }

        Ledger.AssertMatches("UnpairedSubscription", actual,
            "remove the handler with -= when the subscriber goes away (unload, navigate away, dispose)");
    }

    [Fact]
    public void ReleaseDroppedMemoryCalls_MatchTheLedger()
    {
        // ReleaseDroppedMemoryLater( anywhere but NativeHeaps, which defines it.
        const string nativeHeaps = Ui + "Features/Common/Utilities/NativeHeaps.cs";
        var actual = Count(CsAll, @"\bReleaseDroppedMemoryLater\s*\(", nativeHeaps);

        Ledger.AssertMatches("ReleaseDroppedMemory", actual, "release from a frame's Navigated handler (reason navigation-hook), or where a view drops its elements without a navigation (reason drop-point)");
        Ledger.AssertReason("ReleaseDroppedMemory", "navigation-hook",
            Count(CsAll, @"\b\w+_Navigated\s*\([^()]*\)\s*\{(?:[^{}]|\{[^{}]*\})*?\bReleaseDroppedMemoryLater\s*\(", nativeHeaps));
    }

    [Fact]
    public void AsyncPreferenceReads_MatchTheLedger()
    {
        // GetPreferenceAsync( or GetPreferenceAsync<T>(, declarations included: the async twin of GetPreference.
        Ledger.AssertMatches("PreferenceAsyncRead", Count(CsAll, @"\bGetPreferenceAsync\s*(?:<[^<>()]*>)?\s*\("),
            "read the preference with GetPreference");
    }
}

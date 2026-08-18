using System.Text.RegularExpressions;
using FluentAssertions;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.Core.Tests.Localization;

// GetString(key) ?? "fallback" can never fire (GetString returns the literal "[key]" on a miss); ~290 had
// accumulated. Fails the build if the form comes back; GetStringOrDefault is the replacement.
public class LocalizationFallbackTests
{
    // Only a receiver that names the localization service: JsonElement.GetString() genuinely returns null and ??
    // after it is correct.
    private static readonly Regex DeadFallback = new(
        @"(_localizationService|_localization|localizationService)\??\.GetString\([^;]*?\)\s*\?\?",
        RegexOptions.Compiled | RegexOptions.Singleline);

    [Fact]
    public void NoSourceFile_UsesTheUnreachableGetStringFallback()
    {
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(
                     Path.Combine(RepoPaths.SolutionDir(), "src"), "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            if (!DeadFallback.IsMatch(text)) continue;

            foreach (Match match in DeadFallback.Matches(text))
            {
                int line = text.Take(match.Index).Count(c => c == '\n') + 1;
                offenders.Add($"{Path.GetFileName(file)}:{line}");
            }
        }

        offenders.Should().BeEmpty(
            "GetString(...) ?? fallback is dead code - GetString never returns null, it returns \"[key]\". " +
            "Use GetStringOrDefault(key, fallback) so the fallback actually fires. Offenders: "
            + string.Join(", ", offenders));
    }

    [Fact]
    public void TheGuardActuallyMatchesTheShapeItClaimsTo()
    {
        // Without this, a regex that silently matched nothing would report a permanent clean bill.
        DeadFallback.IsMatch(@"_localizationService.GetString(""Button_Cancel"") ?? ""Cancel""")
            .Should().BeTrue();
        DeadFallback.IsMatch(@"localizationService?.GetString(key) ?? fallback")
            .Should().BeTrue();
    }

    [Fact]
    public void TheGuardLeavesUnrelatedGetStringCallsAlone()
    {
        // JsonElement.GetString() really can return null; those ?? operands are load-bearing.
        DeadFallback.IsMatch(@"doc.RootElement.GetProperty(""tag_name"").GetString() ?? ""v0.0.0""")
            .Should().BeFalse();
        DeadFallback.IsMatch(@"je.GetString() ?? """"")
            .Should().BeFalse();
    }
}

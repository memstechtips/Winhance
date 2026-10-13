using System.Globalization;
using System.Text;
using Winhance.Infrastructure.Features.Common.Services;
using Winhance.Infrastructure.Features.Common.Utilities;
using Winhance.Infrastructure.Features.WimUtil.Services;
using Winhance.TestSupport;
using Xunit;
using Xunit.Abstractions;

namespace Winhance.Infrastructure.Tests.WimUtil;

// A no-op under the gate: a file is checked only while docs/autounattend-to-check.xml exists. It is the
// same check WIMUtil runs on a picked file, reachable from the SSH worker, which has no verb for a path.
// Run: drop the file, then winhance-harness AnswerFileCheck, then read docs/autounattend-to-check.txt.
public class AnswerFileCheckTests(ITestOutputHelper output)
{
    private static readonly string DocsDir = Path.Combine(RepoPaths.SolutionDir(), "docs");

    [Fact]
    public async Task Check_the_dropped_answer_file()
    {
        var xmlPath = Path.Combine(DocsDir, "autounattend-to-check.xml");
        if (!File.Exists(xmlPath))
        {
            output.WriteLine("No docs/autounattend-to-check.xml; nothing checked.");
            return;
        }

        var files = new FileSystemService();
        var report = await new AnswerFileValidator(files, new PowerShellRunner(files)).ValidateAsync(xmlPath);

        var text = new StringBuilder();
        text.AppendLine("verdict: " + report.Verdict);
        text.AppendLine("findings: " + report.Findings.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var finding in report.Findings)
            text.AppendLine($"[{finding.Severity}] {finding.Rule} | {finding.Location} | {finding.Detail}");

        await File.WriteAllTextAsync(Path.Combine(DocsDir, "autounattend-to-check.txt"), text.ToString());
        output.WriteLine(text.ToString());
    }
}

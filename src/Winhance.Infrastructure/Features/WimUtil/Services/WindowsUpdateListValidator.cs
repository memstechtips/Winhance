using System.Text.RegularExpressions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.WimUtil.Models;

namespace Winhance.Infrastructure.Features.WimUtil.Services;

// Security boundary: the list is fetched from a public branch, so every allowed value here is a constant and
// nothing in the list can widen it. Winhance only ever downloads from Microsoft's catalog download host.
internal static class WindowsUpdateListValidator
{
    private const int SupportedSchema = 1;
    private const string UrlPrefix = "https://catalog.sf.dl.delivery.mp.microsoft.com/";
    private const string Host = "catalog.sf.dl.delivery.mp.microsoft.com";
    private const string TargetRole = "target";
    private const string CheckpointRole = "checkpoint";

    private static readonly Regex KbPattern = new(@"^KB[0-9]{7}\z", RegexOptions.CultureInvariant);
    private static readonly Regex PathPattern = new(@"^/filestreamingservice/files/[0-9a-f-]{36}/public/[a-z0-9.\-_]+\.msu\z", RegexOptions.CultureInvariant);
    private static readonly Regex NamePattern = new(@"^windows1[01]\.0-kb([0-9]{7})-(x64|arm64)_([0-9a-f]{40})\.msu\z", RegexOptions.CultureInvariant);
    private static readonly Regex Sha1Pattern = new(@"^[0-9a-f]{40}\z", RegexOptions.CultureInvariant);
    private static readonly Regex Sha256Pattern = new(@"^[0-9a-f]{64}\z", RegexOptions.CultureInvariant);

    public static IReadOnlyList<WindowsUpdateBuild> Validate(WindowsUpdateList list, ILogService logService)
    {
        if (list.Schema != SupportedSchema)
        {
            logService.LogWarning($"Windows update list has schema {list.Schema}; only schema {SupportedSchema} is read.");
            return [];
        }

        var valid = new List<WindowsUpdateBuild>();
        foreach (var build in list.Builds ?? [])
        {
            var kept = new List<WindowsUpdate>();
            string? buildProblem = null;
            try
            {
                buildProblem = CheckBuild(build);
                if (buildProblem is null)
                {
                    foreach (var update in build.Updates)
                    {
                        string? problem = TryCheckUpdate(build, update);
                        if (problem is null)
                            kept.Add(update);
                        else
                            logService.LogWarning($"Dropped update {update.Kb} for build {build.Build} {build.Arch}: {problem}.");
                    }
                }
            }
            catch (Exception ex)
            {
                buildProblem = $"malformed entry ({ex.Message})";
                kept.Clear();
            }

            if (buildProblem is not null)
                logService.LogWarning($"Dropped build {build?.Build}: {buildProblem}.");
            else if (kept.Count > 0)
                valid.Add(build with { Updates = kept });
        }

        return valid;
    }

    private static string? CheckBuild(WindowsUpdateBuild build)
    {
        if (build.Build <= 0)
            return "build number is not positive";

        if (build.Arch is not ("x64" or "arm64"))
            return "architecture is not x64 or arm64";

        return null;
    }

    private static string? TryCheckUpdate(WindowsUpdateBuild build, WindowsUpdate update)
    {
        try
        {
            return CheckUpdate(build, update);
        }
        catch (Exception ex)
        {
            return $"malformed entry ({ex.Message})";
        }
    }

    private static string? CheckUpdate(WindowsUpdateBuild build, WindowsUpdate update)
    {
        if (!KbPattern.IsMatch(update.Kb))
            return "KB is not in the form KB1234567";

        if (update.TargetUbr <= 0)
            return "target UBR is not positive";

        var files = update.Files;
        if (files.Count == 0)
            return "no files";

        if (files.Any(f => f.Role is not (CheckpointRole or TargetRole)))
            return "a file has a role other than checkpoint or target";

        if (files.Count(f => f.Role == TargetRole) != 1 || files[^1].Role != TargetRole)
            return "there is not exactly one target, last";

        if (files[^1].Kb != update.Kb)
            return "the target file's KB differs from the update's KB";

        foreach (var file in files)
        {
            string? problem = CheckFile(build, file);
            if (problem is not null)
                return $"file {file.FileName}: {problem}";
        }

        return null;
    }

    private static string? CheckFile(WindowsUpdateBuild build, WindowsUpdateFile file)
    {
        if (file.Url is not { } url
            || !url.StartsWith(UrlPrefix, StringComparison.Ordinal)
            || url.AsSpan(UrlPrefix.Length).IndexOfAny('?', '#') >= 0
            || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || uri.Host != Host
            || uri.UserInfo.Length > 0
            || uri.Query.Length > 0
            || uri.Fragment.Length > 0
            || !PathPattern.IsMatch(uri.AbsolutePath))
        {
            return "the URL is not an https catalog download URL";
        }

        string lastSegment = uri.AbsolutePath[(uri.AbsolutePath.LastIndexOf('/') + 1)..];
        if (file.FileName != lastSegment)
            return "the file name differs from the URL's last segment";

        var name = NamePattern.Match(file.FileName);
        if (!name.Success)
            return "the file name is not a Windows update name";

        if (name.Groups[2].Value != build.Arch)
            return "the file name's architecture differs from the build's";

        if ("KB" + name.Groups[1].Value != file.Kb)
            return "the file name's KB differs from the file's KB";

        if (!Sha1Pattern.IsMatch(file.Sha1) || name.Groups[3].Value != file.Sha1)
            return "SHA1 is not 40 lower-case hex or differs from the file name";

        if (!Sha256Pattern.IsMatch(file.Sha256))
            return "SHA256 is not 64 lower-case hex";

        if (file.Size <= 0)
            return "size is not positive";

        return null;
    }
}

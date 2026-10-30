using System.Text.Json;

namespace Winhance.Core.Features.SoftwareApps.Models;

public enum InstallScope { Default, User, Machine }

public enum InstallArchitecture { Default, X64, X86, Arm64 }

public sealed record InstallOptions
{
    public InstallScope Scope { get; init; }
    public InstallArchitecture Architecture { get; init; }
    public bool Interactive { get; init; }

    public static readonly InstallOptions Default = new();
}

// FollowsDefaults: the per-app dialog's "use the default options" switch; Options is what the dialog shows either way.
public sealed record InstallOptionsChoice(bool FollowsDefaults, InstallOptions Options);

public sealed record InstallOptionsStore
{
    // A file, argument or saved preference can hold an explicit null, which JSON would write over the initializer.
    private InstallOptions _defaults = InstallOptions.Default;
    private Dictionary<string, InstallOptions> _apps = new(StringComparer.Ordinal);

    public InstallOptions Defaults { get => _defaults; init => _defaults = value ?? InstallOptions.Default; }
    public Dictionary<string, InstallOptions> Apps { get => _apps; init => _apps = value ?? new(StringComparer.Ordinal); }

    // What a file holding these apps needs: their own options, and the defaults. Null when nothing differs from
    // letting each installer decide, so a file that changes nothing carries nothing.
    public InstallOptionsStore? For(IEnumerable<string> appIds)
    {
        var ids = appIds.ToHashSet(StringComparer.Ordinal);
        var apps = Apps.Where(app => ids.Contains(app.Key)).ToDictionary(app => app.Key, app => app.Value, StringComparer.Ordinal);
        return apps.Count == 0 && Defaults == InstallOptions.Default ? null : new InstallOptionsStore { Defaults = Defaults, Apps = apps };
    }

    // The --install-options value an answer file starts Winhance with. Base64, so the JSON needs no quoting in the
    // PowerShell literal or on the command line.
    public string ToArgument() => Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(this));

    public static InstallOptionsStore? FromArgument(string? argument)
    {
        if (string.IsNullOrEmpty(argument))
            return null;
        try
        {
            return JsonSerializer.Deserialize<InstallOptionsStore>(Convert.FromBase64String(argument));
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return null;
        }
    }
}

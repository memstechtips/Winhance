using System.Globalization;
using Winhance.Core.Features.Common.Interfaces;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Utilities;

// The merged manifest `winget download` writes beside an installer, reduced to what runs it silently. WIMUtil writes
// the same shape for an installer the user added, so one reader covers both.
internal sealed record WinGetManifest(string InstallerType, string? Silent, string? Custom, string? Scope, IReadOnlySet<int> SuccessCodes)
{
    private static readonly HashSet<string> RunnableTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "msi", "wix", "burn", "inno", "nullsoft", "exe",
    };

    private static readonly HashSet<string> SuccessResponses = new(StringComparer.OrdinalIgnoreCase)
    {
        "rebootRequiredToFinish", "rebootRequiredForInstall", "rebootInitiated", "alreadyInstalled",
    };

    // winget allows installer fields at the document root too, as the default for each Installers entry.
    internal static WinGetManifest? TryRead(string yaml)
    {
        var stream = new YamlStream();
        try
        {
            stream.Load(new StringReader(yaml));
        }
        catch (YamlException)
        {
            return null;
        }

        if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is not YamlMappingNode root)
            return null;

        var installer = Node(root, "Installers") is YamlSequenceNode { Children.Count: > 0 } installers ? installers.Children[0] : null;

        var installerType = (Text(installer, "InstallerType") ?? Text(root, "InstallerType"))?.ToLowerInvariant();
        if (installerType == null)
            return null;

        var silent = Text(installer, "InstallerSwitches", "Silent") ?? Text(root, "InstallerSwitches", "Silent");
        var custom = Text(installer, "InstallerSwitches", "Custom") ?? Text(root, "InstallerSwitches", "Custom");
        var scope = (Text(installer, "Scope") ?? Text(root, "Scope"))?.ToLowerInvariant();

        return new WinGetManifest(
            installerType,
            silent ?? DefaultSilent(installerType),
            custom,
            scope,
            SuccessCodesFor(installerType, installer, root));
    }

    internal static string Write(string installerType, string silent)
    {
        var installer = new YamlMappingNode
        {
            { new YamlScalarNode("InstallerType"), new YamlScalarNode(installerType) },
            { new YamlScalarNode("InstallerSwitches"), new YamlMappingNode { { new YamlScalarNode("Silent"), new YamlScalarNode(silent) } } },
        };
        var root = new YamlMappingNode
        {
            { new YamlScalarNode("ManifestType"), new YamlScalarNode("merged") },
            { new YamlScalarNode("Installers"), new YamlSequenceNode { installer } },
        };

        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        new YamlStream(new YamlDocument(root)).Save(writer, false);
        return writer.ToString();
    }

    internal static bool IsRunnable(string installerType) => RunnableTypes.Contains(installerType);

    internal static bool IsManifest(string path) =>
        string.Equals(Path.GetExtension(path), ".yaml", StringComparison.OrdinalIgnoreCase);

    internal static string? InstallerIn(IFileSystemService files, string folder)
    {
        if (!files.DirectoryExists(folder))
            return null;

        var installers = files.GetFiles(folder).Where(file => !IsManifest(file)).ToList();
        return installers.Count == 1 ? installers[0] : null;
    }

    internal (string FileName, string Arguments)? CommandFor(string installerPath)
    {
        if (!IsRunnable(InstallerType) || string.IsNullOrWhiteSpace(Silent))
            return null;

        var switches = string.IsNullOrWhiteSpace(Custom) ? Silent : $"{Silent} {Custom}";
        if (InstallerType is not ("msi" or "wix"))
            return (installerPath, switches);

        // An msi whose own tables leave ALLUSERS unset installs for the running user alone.
        var allUsers = Scope == "machine" ? " ALLUSERS=1" : string.Empty;
        return ("msiexec.exe", $"/i \"{installerPath}\" {switches}{allUsers}");
    }

    // winget's own defaults for a manifest that names no Silent switch (GetDefaultKnownSwitches, ManifestCommon.cpp).
    private static string? DefaultSilent(string installerType) => installerType switch
    {
        "msi" or "wix" or "burn" => "/quiet /norestart",
        "nullsoft" => "/S",
        "inno" => "/SP- /VERYSILENT /SUPPRESSMSGBOXES /NORESTART",
        _ => null,
    };

    private static HashSet<int> SuccessCodesFor(string installerType, YamlNode? installer, YamlNode root)
    {
        var codes = new HashSet<int> { 0 };
        if (installerType is "msi" or "wix" or "burn")
        {
            // Windows Installer's "succeeded, restart required" and "succeeded, restart started".
            codes.Add(3010);
            codes.Add(1641);
        }

        foreach (var node in new[] { installer, root })
        {
            foreach (var entry in Entries(node, "InstallerSuccessCodes"))
            {
                if (ExitCode(Text(entry)) is { } code)
                    codes.Add(code);
            }

            foreach (var entry in Entries(node, "ExpectedReturnCodes"))
            {
                if (SuccessResponses.Contains(Text(entry, "ReturnResponse") ?? string.Empty)
                    && ExitCode(Text(entry, "InstallerReturnCode")) is { } code)
                {
                    codes.Add(code);
                }
            }
        }

        return codes;
    }

    private static IEnumerable<YamlNode> Entries(YamlNode? node, string key) =>
        Node(node, key) is YamlSequenceNode entries ? entries.Children : Array.Empty<YamlNode>();

    // A DWORD exit code above int.MaxValue reaches .NET as its negative signed form.
    private static int? ExitCode(string? text) =>
        long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code) ? unchecked((int)code) : null;

    private static YamlNode? Node(YamlNode? node, params string[] path)
    {
        foreach (var key in path)
        {
            node = node is YamlMappingNode mapping
                ? mapping.Children.FirstOrDefault(pair => string.Equals((pair.Key as YamlScalarNode)?.Value, key, StringComparison.OrdinalIgnoreCase)).Value
                : null;
        }

        return node;
    }

    private static string? Text(YamlNode? node, params string[] path) =>
        Node(node, path) is YamlScalarNode { Value: { } value } && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
}

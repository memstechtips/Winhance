using Microsoft.Win32;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.TestSupport;

public sealed class FakeOptionProvider : IOptionProvider
{
    public const string TargetKey = "Key";
    public const string ValuePath = @"HKEY_CURRENT_USER\Software\Winhance\Keyed";
    public const string ValueName = "Selected";

    public static readonly IReadOnlyList<DynamicOption> Offered =
    [
        new DynamicOption("Alpha zone", "alpha"),
        new DynamicOption("Beta zone", "beta"),
        new DynamicOption("Gamma zone", "gamma"),
    ];

    private static readonly string[] Paths = [ValuePath];

    public IReadOnlyList<OptionSource> Sources { get; } = Enum.GetValues<OptionSource>();

    public IReadOnlyList<DynamicOption> Options(Setting setting, IDetectionContext context) => Offered;

    public string? CurrentKey(Setting setting, IDetectionContext context) => context.GetValue(ValuePath, ValueName) as string;

    public bool Accepts(Setting setting, string key) => Offered.Any(o => o.Value == key);

    public string? ValueFor(OptionValue value, string key) =>
        throw new ArgumentOutOfRangeException(nameof(value), value, "The fake keyed setting writes only its key.");

    public Effect? EffectFor(Setting setting, string key) => null;

    // Any source the validator lets stand without a registry Path; the fake registry ignores which.
    public static Setting SettingFor(string id = "fake-keyed") => new()
    {
        Id = id,
        Display = new() { Name = TestKeys.Of(id), Description = TestKeys.Of($"{id} description") },
        Targets = new Target[] { new RegTarget(TargetKey, Paths, ValueName, RegistryValueKind.String) },
        Options = new(OptionSource.RegionalFormats),
    };

    // CurrentValue 0 is the index placeholder CatalogSettingStateProvider puts on every keyed setting.
    public static SettingStateResult StateOn(string? key) => new()
    {
        Success = true,
        CurrentValue = 0,
        Outcome = SettingDetectionOutcome.Resolved,
        DynamicOptions = Offered,
        DynamicSelection = key,
    };
}

public sealed class FakeOptionProviderRegistry(IOptionProvider? provider = null) : IOptionProviderRegistry
{
    private readonly IOptionProvider _provider = provider ?? new FakeOptionProvider();

    public IOptionProvider For(OptionSource source) => _provider;
}

public sealed class FakeDetectionContext : IPrefetchableDetectionContext
{
    private readonly Dictionary<string, object?> _values = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, string[]> _subKeys = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, string> _folders = new(StringComparer.Ordinal);

    public WinBuild CurrentBuild { get; init; } = new(26100);

    public FakeDetectionContext Set(string keyPath, string valueName, object? value)
    {
        _values[$@"{NormalizeHive(keyPath)}\{valueName}"] = value;
        return this;
    }

    public FakeDetectionContext Sub(string keyPath, params string[] names)
    {
        _subKeys[NormalizeHive(keyPath)] = names;
        return this;
    }

    private HashSet<string>? _files;

    public FakeDetectionContext Folder(byte[] itemList, string path)
    {
        _folders[Convert.ToBase64String(itemList)] = path;
        return this;
    }

    public string? ShellFolderPath(byte[] itemList) =>
        _folders.TryGetValue(Convert.ToBase64String(itemList), out var path) ? path : null;

    // Until a test names the files this PC has, every path exists.
    public FakeDetectionContext Existing(params string[] paths)
    {
        _files ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in paths) _files.Add(file);
        return this;
    }

    public bool FileExists(string path) => _files is null || _files.Contains(path);

    public ISystemDetectionContextFactory AsFactory() =>
        Mock.Of<ISystemDetectionContextFactory>(f => f.Create() == this);

    public Task PrefetchAsync(IReadOnlyCollection<Setting> settings) => Task.CompletedTask;

    public object? GetValue(string keyPath, string? valueName) =>
        _values.TryGetValue($@"{NormalizeHive(keyPath)}\{valueName}", out var value) ? value : null;

    // A copy: a context can be a shared static fixture, and a caller sorting the array in place would reorder it.
    public string[] GetSubKeyNames(string keyPath) =>
        _subKeys.TryGetValue(NormalizeHive(keyPath), out var names) ? [.. names] : Array.Empty<string>();

    public bool KeyExists(string keyPath)
    {
        var path = NormalizeHive(keyPath);
        return _subKeys.ContainsKey(path)
            || _values.Keys.Any(stored => stored.StartsWith($@"{path}\", StringComparison.OrdinalIgnoreCase));
    }

    public string? PrimaryDnsV4OfActiveAdapter() => null;

    public IReadOnlyList<string> DnsV4ServersOfActiveAdapter() => Array.Empty<string>();

    public bool IsSystemRestoreEnabled() => false;

    public bool? ScheduledTaskEnabled(string taskPath) => null;

    public int? PowerCfgValue(string subgroupGuid, string settingGuid, PowerContext context) => null;

    public string? ActivePowerPlanGuid() => null;

    private static string NormalizeHive(string keyPath)
    {
        var parts = keyPath.Split('\\', 2);
        if (parts.Length < 2)
            return keyPath;

        var hive = parts[0].ToUpperInvariant() switch
        {
            "HKEY_CURRENT_USER" or "HKCU" => "HKCU",
            "HKEY_LOCAL_MACHINE" or "HKLM" => "HKLM",
            "HKEY_USERS" or "HKU" => "HKU",
            "HKEY_CLASSES_ROOT" or "HKCR" => "HKCR",
            var other => other,
        };
        return $@"{hive}\{parts[1]}";
    }
}

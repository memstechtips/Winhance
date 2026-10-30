using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.Common.Models;

public class WinhanceConfigFile
{
    public string Version { get; set; } = "2.0";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Saved configs only, so a user with several PCs can tell them apart; an answer file leaves it out.
    public string? ComputerName { get; set; }

    // The Windows build Builder made the file for, so a reopen shows the same settings. Absent means every version.
    public int? TargetBuild { get; set; }
    public ConfigSection WindowsApps { get; set; } = new ConfigSection();
    public ConfigSection ExternalApps { get; set; } = new ConfigSection();

    // How the External Apps install. Absent in older files, and then the applying PC's own install options are used.
    public InstallOptionsStore? AppInstallOptions { get; set; }
    public FeatureGroupSection Customize { get; set; } = new FeatureGroupSection();
    public FeatureGroupSection Optimize { get; set; } = new FeatureGroupSection();
    public FeatureGroupSection Autounattend { get; set; } = new FeatureGroupSection();
}

// Mutable by design: IsIncluded is toggled after construction and Features is assigned from deserialized JSON.
public class FeatureGroupSection
{
    public bool IsIncluded { get; set; } = false;
    public IReadOnlyDictionary<string, ConfigSection> Features { get; set; } = new Dictionary<string, ConfigSection>();
}

// Mutable by design: set during construction/deserialization.
public class ConfigSection
{
    public bool IsIncluded { get; set; } = false;
    public IReadOnlyList<ConfigurationItem> Items { get; set; } = new List<ConfigurationItem>();
}

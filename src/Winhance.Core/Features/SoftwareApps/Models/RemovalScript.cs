using System;
using System.IO;

namespace Winhance.Core.Features.SoftwareApps.Models;

/// <summary>
/// Represents a script for removing applications and preventing their reinstallation.
/// </summary>
public class RemovalScript
{
    /// <summary>
    /// Gets or sets the name of the script.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the content of the script.
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the scheduled task to create for running the script.
    /// </summary>
    public string TargetScheduledTaskName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the script should run on system startup.
    /// </summary>
    public bool RunOnStartup { get; init; }

    /// <summary>
    /// Gets the path where the script will be saved.
    /// </summary>
    public string ScriptPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        "Winhance",
        "Scripts",
        $"{Name}.ps1");
}

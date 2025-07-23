using System;
using System.IO;

namespace Winhance.Core.Features.SoftwareApps.Models;

/// <summary>
/// Represents a handler for special applications that require custom removal processes.
/// </summary>
public class SpecialAppHandler
{
    /// <summary>
    /// Gets or sets the unique identifier for the special handler type.
    /// </summary>
    public string HandlerType { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name of the application.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the application.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the script content for removing the application.
    /// </summary>
    public string RemovalScriptContent { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the scheduled task to create for preventing reinstallation.
    /// </summary>
    public string ScheduledTaskName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the application is currently installed.
    /// </summary>
    public bool IsInstalled { get; set; }

    /// <summary>
    /// Gets the path where the removal script will be saved.
    /// Note: This property is deprecated. Use IScriptPathService.GetScriptPath() instead.
    /// </summary>
    [Obsolete("Use IScriptPathService.GetScriptPath() for dynamic path resolution")]
    public string ScriptPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Winhance",
            "Scripts",
            $"{HandlerType}Removal.ps1"
        );

    /// <summary>
    /// Gets a collection of predefined special app handlers.
    /// </summary>
    /// <returns>A collection of special app handlers.</returns>
    public static SpecialAppHandler[] GetPredefinedHandlers()
    {
        return new[]
        {
            new SpecialAppHandler
            {
                HandlerType = "Edge",
                DisplayName = "Microsoft Edge",
                Description = "Microsoft's web browser (requires special removal process)",
                RemovalScriptContent = EdgeRemovalScript.GetScript(),
                ScheduledTaskName = "Winhance\\EdgeRemoval",
            },
            new SpecialAppHandler
            {
                HandlerType = "OneDrive",
                DisplayName = "OneDrive",
                Description =
                    "Microsoft's cloud storage service (requires special removal process)",
                RemovalScriptContent = OneDriveRemovalScript.GetScript(),
                ScheduledTaskName = "Winhance\\OneDriveRemoval",
            },
            new SpecialAppHandler
            {
                HandlerType = "OneNote",
                DisplayName = "Microsoft OneNote",
                Description =
                    "Microsoft's note-taking application (requires special removal process)",
                RemovalScriptContent = OneNoteRemovalScript.GetScript(),
                ScheduledTaskName = "Winhance\\OneNoteRemoval",
            },
        };
    }
}

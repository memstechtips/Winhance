using System.Collections.Generic;

namespace Winhance.Core.Features.Common.Models;

/// <summary>
/// Represents a request for user confirmation with optional context data.
/// This generic model can be used across all features that require user confirmation.
/// </summary>
public sealed record ConfirmationRequest
{
    /// <summary>
    /// Gets the confirmation message to display to the user.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets the title of the confirmation dialog.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Gets the optional checkbox text. If null, no checkbox is shown.
    /// </summary>
    public string? CheckboxText { get; init; }

    /// <summary>
    /// Gets the optional list of items to render in a scrollable list inside the dialog.
    /// When null or empty, no list is rendered.
    /// </summary>
    public IReadOnlyList<string>? Items { get; init; } = null;

    /// <summary>
    /// Gets whether the optional checkbox starts checked. Defaults to true.
    /// </summary>
    public bool CheckboxInitiallyChecked { get; init; } = true;

    /// <summary>
    /// Gets the text for the confirm (primary) button. Defaults to "OK".
    /// </summary>
    public string ConfirmButtonText { get; init; } = "OK";

    /// <summary>
    /// Gets the text for the cancel (close) button. Defaults to "Cancel".
    /// </summary>
    public string CancelButtonText { get; init; } = "Cancel";
}

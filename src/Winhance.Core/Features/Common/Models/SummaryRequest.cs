namespace Winhance.Core.Features.Common.Models;

public enum SummaryTone
{
    Neutral,
    Success,
    Warning,
    Error,
}

public enum SummaryIcon
{
    WriteFile,
    Download,
    CopyFiles,
    Check,
    Status,
}

// Status picks its glyph from the row's tone, so a result row reads at a glance without its own icon choice.
public sealed record SummaryRow(SummaryIcon Icon, string Title, string? Detail = null, string? Code = null, SummaryTone Tone = SummaryTone.Neutral);

public sealed record SummarySection(string? Heading, IReadOnlyList<SummaryRow> Rows);

public sealed record SummaryVerdict(SummaryTone Tone, string Title, string? Message = null);

public sealed record SummaryRequest
{
    public string Title { get; init; } = string.Empty;

    public string? Message { get; init; }

    public SummaryVerdict? Verdict { get; init; }

    public IReadOnlyList<SummarySection> Sections { get; init; } = [];

    // Empty makes it a report with a single Close button; set, it asks and returns the answer.
    public string ConfirmButtonText { get; init; } = string.Empty;

    // Empty means the localized Close for a report, Cancel for a question.
    public string CancelButtonText { get; init; } = string.Empty;

    // Set to offer Copy to Clipboard, which copies this text and leaves the dialog open.
    public string? CopyText { get; init; }

    public string? CheckboxText { get; init; }
}

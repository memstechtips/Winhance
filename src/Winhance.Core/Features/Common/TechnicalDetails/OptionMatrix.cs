namespace Winhance.Core.Features.Common.TechnicalDetails;

public sealed record MatrixChip(string Text, string Tooltip)
{
    // Only the setting's name (LinkText) is the link; the prose around it is not somewhere to go.
    public string LinkSettingId { get; init; } = string.Empty;
    public string LinkText { get; init; } = string.Empty;

    public bool HasLink => LinkSettingId.Length > 0 && LinkText.Length > 0;
}

public enum MatrixColumnKind
{
    Value,

    Task,

    Power,

    Script,

    RegFile,
}

public enum MatrixGroupKind
{
    Registry,
    ScheduledTask,
    Power,

    PowerPlan,

    AlsoRuns,
}

// A registry target may carry several (a mirror); showing all of them is the only honest answer to "where does this go".
public sealed record MatrixPath(string Full, string Label = "")
{
    public bool HasLabel => Label.Length > 0;

    // Hive abbreviated so it doesn't widen the columns beneath it; Full is what the tooltip shows and regedit opens.
    public string Display => RegistryPathFormatter.Abbreviate(Full);
}

public sealed record MatrixColumn
{
    public required string Header { get; init; }
    public string TypeName { get; init; } = string.Empty;
    public MatrixColumnKind Kind { get; init; } = MatrixColumnKind.Value;

    public IReadOnlyList<MatrixChip> Chips { get; init; } = [];

    public string HeaderTooltip { get; init; } = string.Empty;

    public bool HasType => TypeName.Length > 0;
    public bool HasChips => Chips.Count > 0;
}

// Why the table is a custom panel: a data-driven number of columns grouped under a spanning header can't be
// expressed as a DataTemplate.
public sealed record MatrixColumnGroup
{
    public required string Label { get; init; }

    public MatrixGroupKind Kind { get; init; } = MatrixGroupKind.Registry;

    public string Description { get; init; } = string.Empty;

    // More than one = a mirror: the same value goes to every path, so listing them all is what makes the header truthful.
    public IReadOnlyList<MatrixPath> Paths { get; init; } = [];

    public required int StartColumn { get; init; }
    public required int ColumnSpan { get; init; }

    // The command that opens Paths lives on the control: a localized string is legitimate Core data, an ICommand is not.
    public string OpenRegeditTooltip { get; init; } = string.Empty;

    public bool HasPaths => Paths.Count > 0;

    // A scheduled task's path is a task-scheduler path regedit cannot open; offering the button there was a dead end.
    public bool CanOpenRegedit => Kind == MatrixGroupKind.Registry && HasPaths;
}

// Not a Target - a Target holds a value per option and belongs in the grid; these fire whichever option you pick.
public sealed record MatrixNote(string Label, string Detail)
{
    public string Scope { get; init; } = string.Empty;
    public bool HasScope => Scope.Length > 0;
}

// Inside the table rather than a section under it: splitting it out made the panel a table plus a pile of blocks again.
public sealed record MatrixCodeBlock(
    string Heading,
    string Label,
    string Body,
    CodeKind Kind,
    // One line under the heading saying what this section of blocks actually does to the machine.
    // Repeated on every block of a section; the view draws it once, beside the heading.
    string Description = "");

public sealed record MatrixCell(string Text, bool IsCheck = false)
{
    public bool HasText => Text.Length > 0;
    public static readonly MatrixCell Empty = new(string.Empty);
    public static readonly MatrixCell Check = new(string.Empty, IsCheck: true);
}

public sealed record MatrixOption
{
    public required string Label { get; init; }

    public IReadOnlyList<MatrixCell> Cells { get; init; } = [];

    public bool IsCurrent { get; init; }
    public bool IsRecommended { get; init; }
    public bool IsWindowsDefault { get; init; }

    // Empty when the role applies whatever the power state - every non-power setting, and a power setting whose
    // contexts agree. A power setting has one value per option but a separate choice per context, so two options
    // can each be current; hence a qualifier on the badge rather than an AC and a DC column, which would repeat the
    // same value in every row.
    public string CurrentContext { get; init; } = string.Empty;
    public string RecommendedContext { get; init; } = string.Empty;
    public string DefaultContext { get; init; } = string.Empty;
}

public sealed record OptionMatrix
{
    public IReadOnlyList<MatrixColumnGroup> Groups { get; init; } = [];
    public IReadOnlyList<MatrixColumn> Columns { get; init; } = [];
    public IReadOnlyList<MatrixOption> Options { get; init; } = [];

    public string OptionHeader { get; init; } = string.Empty;

    public string RoleHeader { get; init; } = string.Empty;

    // The panel puts a path, a value name and a type on screen; without captions the reader has to infer which is which.
    public string PathLabel { get; init; } = string.Empty;
    public string ValueNameLabel { get; init; } = string.Empty;
    public string ValueTypeLabel { get; init; } = string.Empty;
    public string TaskLabel { get; init; } = string.Empty;

    public string SettingLabel { get; init; } = string.Empty;
    public string SettingDescription { get; init; } = string.Empty;

    // Belong to the SETTING, not to any one option (ApplyBehavior hangs off Setting), so they sit in the setting's
    // own cell rather than a column that would repeat on every row.
    public IReadOnlyList<MatrixChip> Requirements { get; init; } = [];

    public IReadOnlyList<MatrixNote> Notes { get; init; } = [];
    public string NotesHeading { get; init; } = string.Empty;

    public string NotesDetailHeader { get; init; } = string.Empty;
    public bool HasNotes => Notes.Count > 0;

    public IReadOnlyList<MatrixCodeBlock> CodeBlocks { get; init; } = [];
    public bool HasCode => CodeBlocks.Count > 0;

    // Present only when detection matched no option; a matched option's current marker already says what is on the system.
    public string ReadingLabel { get; init; } = string.Empty;
    public IReadOnlyList<MatrixCell> ReadingCells { get; init; } = [];
    public bool HasReading => ReadingLabel.Length > 0;

    public string CurrentLabel { get; init; } = string.Empty;
    public string RecommendedLabel { get; init; } = string.Empty;
    public string DefaultLabel { get; init; } = string.Empty;
    public string RecommendedTooltip { get; init; } = string.Empty;
    public string DefaultTooltip { get; init; } = string.Empty;
    public string CurrentTooltip { get; init; } = string.Empty;

    public string AccessibleSummary
    {
        get
        {
            var lines = Options.Select(option =>
            {
                var cells = string.Join(", ", Columns.Select((column, i) =>
                {
                    var cell = i < option.Cells.Count ? option.Cells[i] : MatrixCell.Empty;
                    var value = cell.IsCheck ? CurrentLabel : cell.Text;
                    return $"{column.Header} {value}";
                }));
                var roles = new List<string>(2);
                if (option.IsRecommended) roles.Add(RecommendedLabel);
                if (option.IsWindowsDefault) roles.Add(DefaultLabel);
                var suffix = roles.Count > 0 ? $" ({string.Join(", ", roles)})" : string.Empty;
                var current = option.IsCurrent ? $", {CurrentLabel}" : string.Empty;
                return $"{option.Label}: {cells}{suffix}{current}";
            });
            var where = string.Join(". ", Groups.Where(g => g.HasPaths)
                .Select(g => $"{g.Label}: {string.Join(", ", g.Paths.Select(p => p.Full))}"));
            var reading = HasReading
                ? $" {ReadingLabel}: {string.Join(", ", ReadingCells.Select(c => c.Text))}"
                : string.Empty;
            return $"{string.Join(". ", lines)}. {where}.{reading}";
        }
    }
}


public enum CodeKind { PowerShell, RegFile }



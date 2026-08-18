namespace Winhance.Core.Features.Common.TechnicalDetails;

/// <summary>A short metadata tag with the explanation shown on hover.</summary>
public sealed record MatrixChip(string Text, string Tooltip)
{
    /// <summary>
    /// When set, the chip names another setting and <see cref="LinkText"/> — the part of
    /// <see cref="Text"/> holding that setting's name — becomes a link to it. Only the name is the
    /// link: "Requires:" and "(set automatically)" around it are prose, not somewhere to go.
    /// </summary>
    public string LinkSettingId { get; init; } = string.Empty;
    public string LinkText { get; init; } = string.Empty;

    public bool HasLink => LinkSettingId.Length > 0 && LinkText.Length > 0;
}

/// <summary>What a column documents, which decides how its cells render and what captions it carries.</summary>
public enum MatrixColumnKind
{
    /// <summary>A registry value — the cell shows what the option writes there.</summary>
    Value,

    /// <summary>A scheduled task — the cell shows Enabled/Disabled. Named a task, not a value.</summary>
    Task,

    /// <summary>A powercfg value — the cell shows what the option writes to the active power plan.</summary>
    Power,

    /// <summary>The option also runs a PowerShell script — the cell shows a check or nothing.</summary>
    Script,

    /// <summary>The option also imports a .reg file — the cell shows a check or nothing.</summary>
    RegFile,
}

/// <summary>
/// What a group of columns documents. Drives the caption vocabulary, the one-line description, and
/// whether the paths can be opened — only the registry has a launcher to open them with.
/// </summary>
public enum MatrixGroupKind
{
    Registry,
    ScheduledTask,
    Power,

    /// <summary>A whole power scheme, selected rather than written value by value.</summary>
    PowerPlan,

    /// <summary>The script / .reg columns, which write nowhere the user can be sent.</summary>
    AlsoRuns,
}

/// <summary>
/// One destination a group writes to. A registry target may carry several — that is a mirror, and
/// showing both is the only honest answer to "where does this go".
/// </summary>
/// <param name="Full">The destination itself: a registry path, a task path, or one of the two GUIDs a
/// powercfg value is addressed by.</param>
/// <param name="Label">What this destination is called — "Path" for the registry, "Subgroup" and
/// "Setting" for the two GUIDs a powercfg value is addressed by.</param>
public sealed record MatrixPath(string Full, string Label = "")
{
    public bool HasLabel => Label.Length > 0;

    /// <summary>Hive abbreviated so it doesn't widen the columns beneath it. <see cref="Full"/> is
    /// what the tooltip shows and what the Registry Editor opens.</summary>
    public string Display => RegistryPathFormatter.Abbreviate(Full);
}

/// <summary>One column of the option matrix.</summary>
public sealed record MatrixColumn
{
    public required string Header { get; init; }
    public string TypeName { get; init; } = string.Empty;
    public MatrixColumnKind Kind { get; init; } = MatrixColumnKind.Value;

    /// <summary>Metadata about this specific value: Group Policy, written-not-read, mirrored keys...</summary>
    public IReadOnlyList<MatrixChip> Chips { get; init; } = [];

    /// <summary>Explains an unusual header, e.g. why the default value has no name.</summary>
    public string HeaderTooltip { get; init; } = string.Empty;

    public bool HasType => TypeName.Length > 0;
    public bool HasChips => Chips.Count > 0;
}

/// <summary>
/// A spanning header over the columns that share one destination — a registry path, the scheduled
/// task, or the "also runs" pair. This is why the table is a custom panel: a data-driven number of
/// columns grouped under a header that spans them can't be expressed as a DataTemplate.
/// </summary>
public sealed record MatrixColumnGroup
{
    /// <summary>The mechanism, e.g. "Registry" — says WHERE, for a reader who doesn't know DWORD means registry.</summary>
    public required string Label { get; init; }

    public MatrixGroupKind Kind { get; init; } = MatrixGroupKind.Registry;

    /// <summary>One line saying what Winhance does with these locations — read, write, or both.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Where this group writes. More than one means a mirror: the same value goes to every path, so
    /// listing them all is what makes the header truthful. Empty for groups with no destination.
    /// </summary>
    public IReadOnlyList<MatrixPath> Paths { get; init; } = [];

    public required int StartColumn { get; init; }
    public required int ColumnSpan { get; init; }

    /// <summary>
    /// What the button that opens <see cref="Paths"/> is called. The command it runs is a UI concern
    /// and lives on the control: a localized string is legitimate Core data, an ICommand is not.
    /// </summary>
    public string OpenRegeditTooltip { get; init; } = string.Empty;

    public bool HasPaths => Paths.Count > 0;

    /// <summary>
    /// Only the registry has somewhere to send the user. A scheduled task's path is a task-scheduler
    /// path, which regedit cannot open — offering the button there was a dead end.
    /// </summary>
    public bool CanOpenRegedit => Kind == MatrixGroupKind.Registry && HasPaths;
}

/// <summary>
/// Something that happens on apply which has no column to live in: a fixed registry write, a native
/// power call, a wallpaper. Not a Target -- a Target holds a value per option and belongs in the
/// grid. These fire whichever option you pick, so they are listed under it.
/// </summary>
public sealed record MatrixNote(string Label, string Detail)
{
    /// <summary>Which Windows builds this one applies to, when a setting carries more than one.</summary>
    public string Scope { get; init; } = string.Empty;
    public bool HasScope => Scope.Length > 0;
}

/// <summary>
/// A script or .reg payload, and the option that runs it. Inside the table rather than in a section
/// under it: it is one more thing this setting does, and splitting it out made the panel a table
/// plus a pile of blocks again.
/// </summary>
public sealed record MatrixCodeBlock(
    string Heading,
    string Label,
    string Body,
    CodeKind Kind,
    // One line under the heading saying what this section of blocks actually does to the machine.
    // Repeated on every block of a section; the view draws it once, beside the heading.
    string Description = "");

/// <summary>One cell: either a written value, or a check mark for the script / .reg columns.</summary>
public sealed record MatrixCell(string Text, bool IsCheck = false)
{
    public bool HasText => Text.Length > 0;
    public static readonly MatrixCell Empty = new(string.Empty);
    public static readonly MatrixCell Check = new(string.Empty, IsCheck: true);
}

/// <summary>One row of the option matrix: an authored option and what it writes per column.</summary>
public sealed record MatrixOption
{
    public required string Label { get; init; }

    /// <summary>Positionally aligned with <see cref="OptionMatrix.Columns"/>.</summary>
    public IReadOnlyList<MatrixCell> Cells { get; init; } = [];

    public bool IsCurrent { get; init; }
    public bool IsRecommended { get; init; }
    public bool IsWindowsDefault { get; init; }

    /// <summary>
    /// Which power context a role applies to, e.g. "on battery". Empty when it applies whatever the
    /// power state — which is every non-power setting, and a power setting whose contexts agree.
    ///
    /// A power setting holds one value per option but a separate choice per context, so two options
    /// can each be current: one plugged in, one on battery. That is why these qualify the badge
    /// rather than splitting the table into an AC column and a DC column, which would repeat the
    /// same value in every row and put the part that actually differs nowhere.
    /// </summary>
    public string CurrentContext { get; init; } = string.Empty;
    public string RecommendedContext { get; init; } = string.Empty;
    public string DefaultContext { get; init; } = string.Empty;
}

/// <summary>
/// The headline block: options as rows, what they write as columns, grouped by destination. Replaces
/// the old separate option list and per-key value list, which stated Current/Recommended/Default
/// twice and left the numbers in a different block from the names that give them meaning.
/// </summary>
public sealed record OptionMatrix
{
    public IReadOnlyList<MatrixColumnGroup> Groups { get; init; } = [];
    public IReadOnlyList<MatrixColumn> Columns { get; init; } = [];
    public IReadOnlyList<MatrixOption> Options { get; init; } = [];

    /// <summary>Header over the option-name column.</summary>
    public string OptionHeader { get; init; } = string.Empty;

    /// <summary>Header over the badge column: which option is recommended, which is the Windows default.</summary>
    public string RoleHeader { get; init; } = string.Empty;

    /// <summary>
    /// Captions naming what the table is showing. The panel puts a path, a value name and a type on
    /// screen; without these the reader has to infer which of the three each one is.
    /// </summary>
    public string PathLabel { get; init; } = string.Empty;
    public string ValueNameLabel { get; init; } = string.Empty;
    public string ValueTypeLabel { get; init; } = string.Empty;
    public string TaskLabel { get; init; } = string.Empty;

    /// <summary>
    /// What the table as a whole is, shown in the otherwise-empty cell above the Option and Role
    /// columns. This used to be a section header floating above the table.
    /// </summary>
    public string SettingLabel { get; init; } = string.Empty;
    public string SettingDescription { get; init; } = string.Empty;

    /// <summary>
    /// What applying this setting needs or triggers: a restart, a reboot, another setting being on.
    /// These belong to the SETTING, not to any one option -- ApplyBehavior hangs off Setting, not
    /// SettingState -- so they sit in the setting's own cell rather than in a column that would
    /// repeat itself on every row.
    /// </summary>
    public IReadOnlyList<MatrixChip> Requirements { get; init; } = [];

    /// <summary>Side effects with no column of their own, listed under the grid.</summary>
    public IReadOnlyList<MatrixNote> Notes { get; init; } = [];
    public string NotesHeading { get; init; } = string.Empty;

    /// <summary>Caption over the second column of the notes block.</summary>
    public string NotesDetailHeader { get; init; } = string.Empty;
    public bool HasNotes => Notes.Count > 0;

    /// <summary>Scripts and .reg payloads, grouped under their heading by the view.</summary>
    public IReadOnlyList<MatrixCodeBlock> CodeBlocks { get; init; } = [];
    public bool HasCode => CodeBlocks.Count > 0;

    /// <summary>
    /// Present only when detection matched no option (Custom / Malformed / Undetermined). When an
    /// option IS matched, the current marker on that row already says what's on the system.
    /// </summary>
    public string ReadingLabel { get; init; } = string.Empty;
    public IReadOnlyList<MatrixCell> ReadingCells { get; init; } = [];
    public bool HasReading => ReadingLabel.Length > 0;

    public string CurrentLabel { get; init; } = string.Empty;
    public string RecommendedLabel { get; init; } = string.Empty;
    public string DefaultLabel { get; init; } = string.Empty;
    public string RecommendedTooltip { get; init; } = string.Empty;
    public string DefaultTooltip { get; init; } = string.Empty;
    public string CurrentTooltip { get; init; } = string.Empty;

    /// <summary>Screen-reader text for the whole table.</summary>
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


/// <summary>Which syntax a <see cref="MatrixCodeBlock"/> holds, so the view can style it.</summary>
public enum CodeKind { PowerShell, RegFile }



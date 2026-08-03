using System;
using System.Collections.Generic;
using System.Windows.Input;
using FluentIcons.Common;
using FluentIcons.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.TechnicalDetails;

namespace Winhance.UI.Features.Common.Controls;

/// <summary>
/// Builds the option matrix's cells and hands them to <see cref="TechnicalDetailsTable"/> to lay out.
/// Cells are created in code because the column count is per-setting — a DataTemplate cannot express
/// "N columns where N comes from the data".
///
/// Every visual comes from a named Style, never a brush looked up here: a brush read out of
/// Application.Resources is captured once and would not follow a light/dark switch.
/// </summary>
public sealed partial class OptionMatrixView : UserControl
{
    /// <summary>The option name and its role badges stay put while the value columns scroll.</summary>
    private const int FrozenColumns = 2;

    /// <summary>
    /// Three header rows. The mechanism is named once and spans every column it owns; the paths
    /// split beneath it, one cell per destination; the value names split again beneath those. Two
    /// registry paths used to mean two full copies of "Registry" and its description side by side.
    /// </summary>
    private const int MechanismRow = 0;
    private const int PathRow = 1;
    private const int ColumnHeaderRow = 2;
    private const int FirstOptionRow = 3;

    /// <summary>Matches the Software &amp; Apps table, so the two read as the same control.</summary>
    private const double OuterCornerRadius = 4;

    /// <summary>Clearance under a code block's last line so the scrollbar doesn't sit on it.</summary>
    private const double CodeScrollbarGutter = 12;

    /// <summary>
    /// Reserved for the "this is your current setting" marker: the 15px icon plus the gap after it.
    /// Held open on every row, including rows with no marker, so the labels line up.
    /// </summary>
    private const double CurrentMarkerGutter = 21;

    /// <summary>
    /// Where the table ends. Both the grid lines and the rounded corners are per-cell decisions
    /// that need this before the first cell is built, so <see cref="Rebuild"/> sets them up front.
    /// </summary>
    private int _lastColumn;
    private int _lastRow;

    /// <summary>
    /// Whether the grid is the last thing inside the box. Code blocks sit under it but inside the
    /// same border, and then the grid's bottom edge is an internal boundary rather than the box's
    /// own -- so the last row keeps both its bottom line and its square corners.
    /// </summary>
    private bool _gridIsLastElement = true;

    public OptionMatrixView()
    {
        InitializeComponent();
        Table.LayoutMeasured += OnTableMeasured;
    }

    public static readonly DependencyProperty MatrixProperty =
        DependencyProperty.Register(
            nameof(Matrix),
            typeof(OptionMatrix),
            typeof(OptionMatrixView),
            new PropertyMetadata(null, OnMatrixChanged));

    public OptionMatrix? Matrix
    {
        get => (OptionMatrix?)GetValue(MatrixProperty);
        set => SetValue(MatrixProperty, value);
    }

    private static void OnMatrixChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((OptionMatrixView)d).Rebuild();

    /// <summary>
    /// Opens the Registry Editor at the path a button sits beside. It reaches the buttons through
    /// the control rather than through the matrix so Core carries no ICommand: the model says WHERE
    /// a group writes, and the control says what clicking does about it.
    /// </summary>
    public static readonly DependencyProperty RegeditCommandProperty =
        DependencyProperty.Register(
            nameof(RegeditCommand),
            typeof(ICommand),
            typeof(OptionMatrixView),
            new PropertyMetadata(null, OnRegeditCommandChanged));

    public ICommand? RegeditCommand
    {
        get => (ICommand?)GetValue(RegeditCommandProperty);
        set => SetValue(RegeditCommandProperty, value);
    }

    /// <summary>
    /// The buttons are built once, inside Rebuild, so a command that arrives after the matrix has
    /// to rebuild them -- which of the two bindings lands first is not ours to control, and a
    /// button built against a null command would stay dead for the life of the card.
    /// </summary>
    private static void OnRegeditCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((OptionMatrixView)d).Rebuild();

    private static Style Named(string key) => (Style)Application.Current.Resources[key];

    private void Rebuild()
    {
        Table.Children.Clear();
        if (Matrix is not { } matrix || !HasAnythingToShow(matrix))
        {
            Table.ColumnCount = 0;
            // Cleared here as well as at the top of AddCodeBlocks, which this return never reaches:
            // rebinding from a matrix with scripts to one without left the old scripts on screen.
            CodeHost.Children.Clear();
            return;
        }

        // Column 0 is the option name, column 1 its badges; the data columns follow.
        Table.ColumnCount = FrozenColumns + matrix.Columns.Count;
        // With no value columns the table IS the two frozen ones, and freezing both leaves
        // nothing able to move: a note detail that is a full registry path then runs past the
        // right edge of the card with no way to reach its tail. Pinning the label alone makes
        // the detail column the scrolling region, which is what the scrollbar already assumes.
        Table.FrozenColumnCount = matrix.Columns.Count == 0 ? 1 : FrozenColumns;

        _lastColumn = Table.ColumnCount - 1;
        // Everything under the grid: a heading plus a row per note, then per code block a heading
        // when it changes, a label row and the body. Counted with the same expressions that place
        // them, so the two cannot drift and leave the last row's borders on the wrong cells.
        var noteRows = matrix.HasNotes ? matrix.Notes.Count + 1 : 0;
        var rowsBelowHeaders = matrix.Options.Count + (matrix.HasReading ? 1 : 0) + noteRows;

        // A setting with no columns and no options has no column header row and no option rows, so
        // the setting cell is the entire grid. GridLines and Corners are handed a cell's FIRST row,
        // which for that cell is MechanismRow even though it also spans the path row.
        _lastRow = rowsBelowHeaders > 0
            ? FirstOptionRow + rowsBelowHeaders - 1
            : (HasColumnHeaderRow(matrix) ? ColumnHeaderRow : MechanismRow);

        // The code blocks sit under the grid, inside the same box but outside the column model, so
        // the grid's bottom edge is only the box's own when nothing follows it.
        _gridIsLastElement = !matrix.HasCode;

        AddGroupHeaders(matrix);
        AddColumnHeaders(matrix);

        for (int i = 0; i < matrix.Options.Count; i++)
            AddOptionRow(matrix, matrix.Options[i], FirstOptionRow + i);

        if (matrix.HasReading)
            AddReadingRow(matrix, FirstOptionRow + matrix.Options.Count);

        if (matrix.HasNotes)
            AddNotes(matrix, FirstOptionRow + matrix.Options.Count + (matrix.HasReading ? 1 : 0));

        AddCodeBlocks(matrix);
    }

    /// <summary>
    /// A matrix with no columns still renders when it carries notes, code or requirement chips -
    /// that is the whole shape of a script-only setting. Only a genuinely empty one is dropped.
    /// </summary>
    private static bool HasAnythingToShow(OptionMatrix m) =>
        m.Columns.Count > 0 || m.HasNotes || m.HasCode || m.Requirements.Count > 0;

    /// <summary>
    /// Whether there is anything for the Option and Role headers to head. With neither columns nor
    /// options they are a header band over two empty columns, which reads as a table that lost its
    /// rows rather than as a setting documented by its scripts.
    /// </summary>
    private static bool HasColumnHeaderRow(OptionMatrix m) =>
        m.Columns.Count > 0 || m.Options.Count > 0;

    /// <summary>
    /// The scripts and .reg payloads a setting runs, under the grid and outside its column model.
    /// Each body gets a scroller of its own: a script line is many times wider than any column, and
    /// letting it size a column left one column enormous and empty. Wrapping is off inside the
    /// scroller so indentation survives -- it is code, and reflowed code is harder to read than
    /// scrolled code.
    /// </summary>
    /// <summary>Heading plus its one-line description, stacked the way the Options header cell
    /// already stacks them. Falls back to the bare heading when a section has no description.</summary>
    private FrameworkElement HeadingContent(string heading, string description)
    {
        var label = new TextBlock { Text = heading, Style = Named("TechDetail.Table.GroupLabel") };
        if (string.IsNullOrEmpty(description)) return label;

        var stack = new StackPanel();
        stack.Children.Add(label);
        stack.Children.Add(new TextBlock
        {
            Text = description,
            Style = Named("TechDetail.Table.GroupDescription"),
        });
        return stack;
    }

    private void AddCodeBlocks(OptionMatrix matrix)
    {
        CodeHost.Children.Clear();
        if (!matrix.HasCode) return;

        var heading = string.Empty;
        foreach (var block in matrix.CodeBlocks)
        {
            if (block.Heading != heading)
            {
                heading = block.Heading;
                CodeHost.Children.Add(new Border
                {
                    Style = Named("TechDetail.Table.HeaderBand"),
                    // The first band needs no top line: the grid's own last row draws it now, and
                    // two 1px lines a pixel apart read as a smudge. A second band still needs one --
                    // what sits above it is a code Border with a bottom margin and no line at all.
                    BorderThickness = new Thickness(0, CodeHost.Children.Count == 0 ? 0 : 1, 0, 1),
                    Child = HeadingContent(heading, block.Description),
                });
            }
            else if (CodeHost.Children.Count > 0)
            {
                // A heading band already carries a top line, so the first group under one needs no
                // divider - and the last group needs none either, because the box's own border
                // closes it.
                CodeHost.Children.Add(new Border { Style = Named("TechDetail.Table.CodeSeparator") });
            }

            CodeHost.Children.Add(new TextBlock
            {
                Text = block.Label,
                Style = Named("TechDetail.Table.OptionLabel"),
                Margin = new Thickness(12, 8, 12, 4),
            });

            var body = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollMode = ScrollMode.Enabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollMode = ScrollMode.Disabled,
                Content = new TextBlock
                {
                    Text = block.Body,
                    Style = Named("TechDetail.CodeText"),
                    TextWrapping = TextWrapping.NoWrap,

                    // A gutter for the scrollbar to live in. WinUI draws it over the bottom of the
                    // viewport, which on a one-line script is exactly where the script is -- you
                    // cannot read the line you are dragging the bar to reveal.
                    Margin = new Thickness(0, 0, 0, CodeScrollbarGutter),
                },
            };

            CodeHost.Children.Add(new Border
            {
                Style = Named(block.Kind == CodeKind.PowerShell
                    ? "TechDetail.CodeBlock.PowerShell"
                    : "TechDetail.CodeBlock.RegContent"),
                Margin = new Thickness(12, 0, 12, 8),
                Child = body,
            });
        }
    }

    /// <summary>
    /// What else applying does, under the grid rather than in a section below the table. Each spans
    /// the full width: these are not per-option facts, so they have no column to sit in.
    /// </summary>
    private int AddNotes(OptionMatrix matrix, int firstRow)
    {
        var width = Table.ColumnCount;
        var row = firstRow;

        // Where the label/detail pair sits. With value columns the label covers the two frozen ones
        // and the detail covers the rest. With none, the table IS those two frozen columns, so the
        // pair splits across them: placing the detail at FrozenColumns would put it one past the
        // last column, where the layout arranges it at zero size and every note's text vanishes.
        var split = matrix.Columns.Count == 0;
        var labelSpan = split ? 1 : FrozenColumns;
        var detailColumn = split ? 1 : FrozenColumns;
        var detailSpan = split ? 1 : Math.Max(1, width - FrozenColumns);

        Place(new Border
        {
            Style = Named("TechDetail.Table.HeaderBand"),
            Child = new TextBlock { Text = matrix.NotesHeading, Style = Named("TechDetail.Table.GroupLabel") },
        }, 0, row, labelSpan);

        Place(new Border
        {
            Style = Named("TechDetail.Table.HeaderBand"),
            Child = new TextBlock { Text = matrix.NotesDetailHeader, Style = Named("TechDetail.Table.HeaderCaption") },
        }, detailColumn, row, detailSpan);
        row++;

        foreach (var note in matrix.Notes)
        {
            // Two cells on the table's own boundary: what it does over Option and Role, where it
            // does it over the value columns. One wide run of text alongside a grid reads as a
            // caption stuck under the table rather than part of it.
            var name = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
            name.Children.Add(new TextBlock { Text = note.Label, Style = Named("TechDetail.Table.OptionLabel") });
            if (note.HasScope)
                name.Children.Add(new TextBlock { Text = note.Scope, Style = Named("TechDetail.Table.HeaderCaption") });
            Place(new Border { Style = Named("TechDetail.Table.Cell"), Child = name }, 0, row, labelSpan);

            Place(new Border
            {
                Style = Named("TechDetail.Table.Cell"),
                Child = new TextBlock { Text = note.Detail, Style = Named("TechDetail.Table.Value") },
            }, detailColumn, row, detailSpan);

            row++;
        }

        return row;
    }

    /// <summary>
    /// The spanning row: each destination sits directly above the values it owns, with the button
    /// that opens it. This is what replaced the separate "where this lives" block.
    /// </summary>
    private void AddGroupHeaders(OptionMatrix matrix)
    {
        // Spans both header bands: what this table is doesn't change between the mechanism row and
        // the path row, and a blank cell under it would draw a grid line across nothing.
        Place(SettingCell(matrix), 0, MechanismRow, FrozenColumns, rowSpan: 2);

        // Consecutive groups sharing a mechanism are named once, over all of their columns together.
        foreach (var run in ConsecutiveByKind(matrix.Groups))
        {
            var first = run[0];
            var content = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };

            content.Children.Add(new TextBlock
            {
                Text = first.Label,
                Style = Named("TechDetail.Table.GroupLabel"),
            });
            if (first.Description.Length > 0)
            {
                content.Children.Add(new TextBlock
                {
                    Text = first.Description,
                    Style = Named("TechDetail.Table.GroupDescription"),
                });
            }

            var span = run.Sum(g => g.ColumnSpan);
            var band = new Border { Style = Named("TechDetail.Table.HeaderBand"), Child = content };

            // A power plan has no path, so there is nothing to split beneath it. Covering both rows
            // beats leaving an empty cell with a grid line drawn across it.
            var rowSpan = run.Any(g => g.HasPaths) ? 1 : 2;
            Place(band, FrozenColumns + first.StartColumn, MechanismRow, span, rowSpan);
        }

        // The paths split beneath the mechanism: one cell per group, each naming its own destination.
        foreach (var group in matrix.Groups)
        {
            if (!group.HasPaths) continue;
            var content = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };

            // One line per destination. A mirrored value is written to all of them, so listing them
            // all -- each with its own way in -- is the only honest answer to "where does this go".
            foreach (var path in group.Paths)
                content.Children.Add(PathLine(group, path));

            var band = new Border { Style = Named("TechDetail.Table.HeaderBand"), Child = content };
            Place(band, FrozenColumns + group.StartColumn, PathRow, group.ColumnSpan);
        }
    }

    /// <summary>
    /// Runs of adjacent groups that share a mechanism. Adjacent only: the mechanism cell spans a
    /// contiguous block of columns, so two registry groups separated by a scheduled task stay two
    /// headings rather than one heading reaching across the task in between.
    /// </summary>
    private static List<List<MatrixColumnGroup>> ConsecutiveByKind(IReadOnlyList<MatrixColumnGroup> groups)
    {
        var runs = new List<List<MatrixColumnGroup>>();
        foreach (var group in groups)
        {
            var last = runs.Count > 0 ? runs[^1] : null;
            if (last is not null
                && last[^1].Kind == group.Kind
                && last[^1].StartColumn + last[^1].ColumnSpan == group.StartColumn)
            {
                last.Add(group);
            }
            else
            {
                runs.Add([group]);
            }
        }
        return runs;
    }

    /// <summary>
    /// The cell above Option and Role: what this table is, and what applying it needs. It used to be
    /// empty, with a section header floating above the table saying the first half of it.
    /// </summary>
    private static Border SettingCell(OptionMatrix matrix)
    {
        var content = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };

        if (matrix.SettingLabel.Length > 0)
            content.Children.Add(new TextBlock
            {
                Text = matrix.SettingLabel,
                Style = Named("TechDetail.Table.GroupLabel"),
            });
        if (matrix.SettingDescription.Length > 0)
            content.Children.Add(new TextBlock
            {
                Text = matrix.SettingDescription,
                Style = Named("TechDetail.Table.GroupDescription"),
            });

        if (matrix.Requirements.Count > 0)
        {
            var chips = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                Margin = new Thickness(0, 4, 0, 0),
            };
            foreach (var requirement in matrix.Requirements) chips.Children.Add(Chip(requirement));
            content.Children.Add(chips);
        }

        return new Border { Style = Named("TechDetail.Table.HeaderBand"), Child = content };
    }

    /// <summary>
    /// One destination: what it is called, where it is, and the button that opens it. An instance
    /// method because the button's command is a dependency property on this control now.
    /// </summary>
    private FrameworkElement PathLine(MatrixColumnGroup group, MatrixPath path)
    {
        var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };

        if (path.HasLabel)
            line.Children.Add(new TextBlock
            {
                Text = path.Label,
                Style = Named("TechDetail.Table.HeaderCaption"),
            });

        var text = new TextBlock { Text = path.Display, Style = Named("TechDetail.Table.GroupPath") };
        // Shown with the hive abbreviated so it doesn't widen the columns beneath it; the full form
        // has to stay reachable, and a tooltip costs no width.
        ToolTipService.SetToolTip(text, path.Full);
        AutomationProperties.SetName(text, path.Full);
        line.Children.Add(text);

        // Only the registry has a launcher. A scheduled-task path and a powercfg GUID have nothing
        // regedit can open, and a button that goes nowhere is worse than no button.
        if (group.CanOpenRegedit)
        {
            var open = new Button
            {
                // The control's own command rather than the model's: an ICommand slot on a Core
                // record was the single System.Windows reference in the whole of Winhance.Core.
                Command = RegeditCommand,
                CommandParameter = path.Full,
                Style = Named("TechDetail.Table.RegeditButton"),
                Content = new Grid
                {
                    Width = 14,
                    Height = 14,
                    Children = { new FontIcon { Glyph = "", FontSize = 12 } },
                },
            };
            // The model can no longer know whether a command exists - the ICommand slot moved
            // off the Core record - so a consumer that binds Matrix and forgets RegeditCommand
            // would render a button that looks live and does nothing. Every call site binds it
            // today; this is the guard for the next one. A command arriving later rebuilds these
            // buttons, so the disable is not sticky.
            if (RegeditCommand is null) open.IsEnabled = false;
            ToolTipService.SetToolTip(open, group.OpenRegeditTooltip);
            AutomationProperties.SetName(open, $"{group.OpenRegeditTooltip}: {path.Full}");
            line.Children.Add(open);
        }

        return line;
    }

    private void AddColumnHeaders(OptionMatrix matrix)
    {
        if (!HasColumnHeaderRow(matrix)) return;

        Place(FrozenHeader(matrix.OptionHeader), 0, ColumnHeaderRow);
        Place(FrozenHeader(matrix.RoleHeader), 1, ColumnHeaderRow);

        for (int i = 0; i < matrix.Columns.Count; i++)
        {
            var column = matrix.Columns[i];
            var stack = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };

            // The caption names what the header is, and sits BESIDE it rather than above it: a
            // caption on its own line reads as another column heading. Script / .reg columns are
            // already named by their group, so they get no caption.
            var nameCaption = column.Kind switch
            {
                MatrixColumnKind.Value => matrix.ValueNameLabel,
                MatrixColumnKind.Task => matrix.TaskLabel,
                _ => string.Empty,
            };
            stack.Children.Add(CaptionedLine(nameCaption, column.Header, "TechDetail.Table.HeaderText"));

            if (column.HasType)
            {
                // Only a registry value has a value TYPE. A powercfg column's TypeName is its unit,
                // which the group header has already introduced, so it stands on its own.
                var typeCaption = column.Kind == MatrixColumnKind.Value ? matrix.ValueTypeLabel : string.Empty;
                stack.Children.Add(CaptionedLine(typeCaption, column.TypeName, "TechDetail.Table.HeaderType"));
            }
            if (column.HasChips)
            {
                var chips = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(0, 2, 0, 0) };
                foreach (var chip in column.Chips) chips.Children.Add(Chip(chip));
                stack.Children.Add(chips);
            }

            var cell = new Border { Style = Named("TechDetail.Table.HeaderCell"), Child = stack };
            if (column.HeaderTooltip.Length > 0) ToolTipService.SetToolTip(cell, column.HeaderTooltip);
            Place(cell, FrozenColumns + i, ColumnHeaderRow);
        }
    }

    private void AddOptionRow(OptionMatrix matrix, MatrixOption option, int row)
    {
        // The marker gets a fixed-width column of its own rather than sitting in the flow, so every
        // option label starts at the same x whether or not that option is the current one. In a
        // StackPanel the marker pushed its own label right and left every other label behind.
        // Centred vertically because the row is taller than its text.
        var name = new Grid
        {
            VerticalAlignment = VerticalAlignment.Center,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(CurrentMarkerGutter) },
                new ColumnDefinition { Width = GridLength.Auto },
            },
        };

        if (option.IsCurrent)
        {
            var marker = new FluentIcon
            {
                Icon = Icon.CheckmarkCircle,
                IconVariant = IconVariant.Color,
                FontSize = 15,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
            };
            // A power setting can be on one option plugged in and another on battery, so the marker
            // has to say which -- two rows can both be current.
            var current = Qualified(matrix.CurrentLabel, option.CurrentContext);
            ToolTipService.SetToolTip(marker,
                option.CurrentContext.Length > 0 ? $"{matrix.CurrentTooltip} ({option.CurrentContext})" : matrix.CurrentTooltip);
            AutomationProperties.SetName(marker, current);
            name.Children.Add(marker);
        }

        var label = new TextBlock { Text = option.Label, Style = Named("TechDetail.Table.OptionLabel") };
        Grid.SetColumn(label, 1);
        name.Children.Add(label);

        Place(new Border { Style = Named("TechDetail.Table.Cell"), Child = name }, 0, row);

        var badges = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Center,
        };
        if (option.IsRecommended)
            badges.Children.Add(Badge(
                Qualified(matrix.RecommendedLabel, option.RecommendedContext),
                matrix.RecommendedTooltip, recommended: true));
        if (option.IsWindowsDefault)
            badges.Children.Add(Badge(
                Qualified(matrix.DefaultLabel, option.DefaultContext),
                matrix.DefaultTooltip, recommended: false));
        Place(new Border { Style = Named("TechDetail.Table.Cell"), Child = badges }, 1, row);

        for (int i = 0; i < matrix.Columns.Count; i++)
        {
            var cell = i < option.Cells.Count ? option.Cells[i] : MatrixCell.Empty;
            Place(ValueCell(cell, matrix, current: option.IsCurrent), FrozenColumns + i, row);
        }
    }

    /// <summary>The live readings, shown only when detection matched no option.</summary>
    private void AddReadingRow(OptionMatrix matrix, int row)
    {
        var label = new TextBlock { Text = matrix.ReadingLabel, Style = Named("TechDetail.Table.ReadingLabel") };
        Place(new Border { Style = Named("TechDetail.Table.Cell"), Child = label }, 0, row, FrozenColumns);

        for (int i = 0; i < matrix.Columns.Count; i++)
        {
            var cell = i < matrix.ReadingCells.Count ? matrix.ReadingCells[i] : MatrixCell.Empty;
            Place(ValueCell(cell, matrix, current: true), FrozenColumns + i, row);
        }
    }

    private static Border ValueCell(MatrixCell cell, OptionMatrix matrix, bool current)
    {
        FrameworkElement content;
        if (cell.IsCheck)
        {
            var check = new FluentIcon
            {
                Icon = Icon.CheckmarkCircle,
                IconVariant = IconVariant.Color,
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
            };
            AutomationProperties.SetName(check, matrix.CurrentLabel);
            content = check;
        }
        else
        {
            content = new TextBlock
            {
                Text = cell.Text,
                Style = Named(current ? "TechDetail.Table.ValueCurrent" : "TechDetail.Table.Value"),
            };
        }
        return new Border { Style = Named("TechDetail.Table.Cell"), Child = content };
    }

    /// <summary>
    /// "Recommended" when it holds whatever the power state, "Recommended (On Battery)" when the
    /// contexts disagree. Non-power settings never carry a context and read unchanged.
    /// </summary>
    private static string Qualified(string label, string context) =>
        context.Length > 0 ? $"{label} ({context})" : label;

    private static Border Badge(string text, string tooltip, bool recommended)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        if (recommended)
            content.Children.Add(new FontIcon { Glyph = "", Style = Named("TechDetail.Table.BadgeRecommendedIcon") });
        else
            content.Children.Add(new PathIcon
            {
                Data = (Microsoft.UI.Xaml.Media.Geometry)Microsoft.UI.Xaml.Markup.XamlBindingHelper.ConvertValue(
                    typeof(Microsoft.UI.Xaml.Media.Geometry),
                    (string)Application.Current.Resources["BadgeDefaultIconPath"]),
                Style = Named("TechDetail.Table.BadgeDefaultIcon"),
            });
        content.Children.Add(new TextBlock
        {
            Text = text,
            Style = Named(recommended ? "TechDetail.Table.BadgeRecommendedText" : "TechDetail.Table.BadgeDefaultText"),
        });

        var badge = new Border
        {
            Style = Named(recommended ? "BadgeRecommendedStyle" : "BadgeDefaultStyle"),
            Child = content,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ToolTipService.SetToolTip(badge, tooltip);
        return badge;
    }

    /// <summary>
    /// A caption and the thing it names, on one line. Side by side rather than stacked, because a
    /// caption on its own line reads as a heading in its own right.
    /// </summary>
    private static FrameworkElement CaptionedLine(string caption, string text, string textStyle)
    {
        var value = new TextBlock { Text = text, Style = Named(textStyle) };
        if (caption.Length == 0) return value;

        var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        line.Children.Add(new TextBlock { Text = caption, Style = Named("TechDetail.Table.HeaderCaption") });
        line.Children.Add(value);
        return line;
    }

    /// <summary>
    /// A header over one of the frozen columns. Centred vertically because this row is as tall as
    /// the value headers beside it, which carry a caption, a name and a type -- left to itself the
    /// text sits against the top grid line while everything around it is centred.
    /// </summary>
    private static Border FrozenHeader(string text) => new()
    {
        Style = Named("TechDetail.Table.HeaderCell"),
        Child = new TextBlock
        {
            Text = text,
            Style = Named("TechDetail.Table.HeaderText"),
            VerticalAlignment = VerticalAlignment.Center,
        },
    };

    private static Border Chip(MatrixChip chip)
    {
        var border = new Border
        {
            Style = Named("TechDetail.Chip"),
            Child = ChipContent(chip),
        };
        ToolTipService.SetToolTip(border, chip.Tooltip);
        AutomationProperties.SetName(border, $"{chip.Text}. {chip.Tooltip}");
        return border;
    }

    /// <summary>
    /// Plain text, unless the chip names another setting -- then just that name is a link and the
    /// prose around it stays prose. Splitting the rendered text rather than storing three fragments
    /// keeps one source of truth for the sentence, and falls back to plain text if the name is not
    /// found inside it (a translation that reorders the sentence, say).
    /// </summary>
    private static FrameworkElement ChipContent(MatrixChip chip)
    {
        var at = chip.HasLink ? chip.Text.IndexOf(chip.LinkText, StringComparison.Ordinal) : -1;
        if (at < 0) return new TextBlock { Text = chip.Text, Style = Named("TechDetail.ChipText") };

        var line = new StackPanel { Orientation = Orientation.Horizontal };
        if (at > 0)
            line.Children.Add(new TextBlock
            {
                Text = chip.Text[..at],
                Style = Named("TechDetail.ChipText"),
            });

        var link = new HyperlinkButton
        {
            Content = new TextBlock { Text = chip.LinkText, Style = Named("TechDetail.ChipLinkText") },
            Style = Named("TechDetail.ChipLink"),
        };
        link.Click += (_, _) => App.Services?.GetService<IEventBus>()?.Publish(
            new SettingLinkRequestedEvent { SettingId = chip.LinkSettingId, SettingName = chip.LinkText });
        AutomationProperties.SetName(link, chip.LinkText);
        line.Children.Add(link);

        var tail = at + chip.LinkText.Length;
        if (tail < chip.Text.Length)
            line.Children.Add(new TextBlock
            {
                Text = chip.Text[tail..],
                Style = Named("TechDetail.ChipText"),
            });

        return line;
    }

    private void Place(FrameworkElement element, int column, int row, int columnSpan = 1, int rowSpan = 1)
    {
        element.Tag = new TableCellInfo
        {
            Column = column,
            Row = row,
            ColumnSpan = columnSpan,
            RowSpan = rowSpan,
        };

        // Set on the instance rather than in the Style: every cell wants a different combination,
        // and a local value is what lets one Style still carry the brush and the metrics.
        if (element is Border cell)
        {
            cell.BorderThickness = GridLines(column, row, columnSpan);
            cell.CornerRadius = Corners(column, row, columnSpan);
        }

        Table.Children.Add(element);
    }

    /// <summary>
    /// Grid lines sit on each cell's right and bottom edge. The rightmost column drops its, because
    /// the box's own border already draws that edge and two 1px lines a pixel apart read as a smudge
    /// rather than as a line. The bottom row drops its only when the grid IS the last thing in the
    /// box: with code blocks under it that edge is an internal boundary, and suppressing it there is
    /// what left the last options row hanging open.
    /// </summary>
    private Thickness GridLines(int column, int row, int columnSpan) =>
        new(0, 0, IsLastColumn(column, columnSpan) ? 0 : 1,
            row >= _lastRow && _gridIsLastElement ? 0 : 1);   // L,T,R,B

    /// <summary>
    /// Rounds only the four cells in the table's corners, to the same radius the Software &amp; Apps
    /// table uses. A cell can be both first and last when the table is one column wide, so each
    /// corner is decided on its own rather than by picking a single case.
    /// </summary>
    private CornerRadius Corners(int column, int row, int columnSpan)
    {
        bool first = column == 0;
        bool last = IsLastColumn(column, columnSpan);
        bool top = row == MechanismRow;
        bool bottom = _gridIsLastElement && row >= _lastRow;

        // topLeft, topRight, bottomRight, bottomLeft
        return new CornerRadius(
            top && first ? OuterCornerRadius : 0,
            top && last ? OuterCornerRadius : 0,
            bottom && last ? OuterCornerRadius : 0,
            bottom && first ? OuterCornerRadius : 0);
    }

    /// <summary>A spanning header reaches the edge when its LAST column does, not its first.</summary>
    private bool IsLastColumn(int column, int columnSpan) => column + columnSpan - 1 >= _lastColumn;

    private void OnTableMeasured(object? sender, EventArgs e)
    {
        // ViewportWidth, not ActualWidth: this runs inside the measure pass, where ActualWidth still
        // holds the previous layout's value -- zero on the first one, which made every table look
        // like it overflowed and put a scrollbar under tables that fit.
        var viewport = Table.ViewportWidth;
        var frozen = Table.FrozenWidth;
        var max = Math.Max(0, Table.TotalColumnWidth - viewport);
        var scrollable = Math.Max(0, viewport - frozen);

        HorizontalScroll.Maximum = max;

        // Sized and positioned against the scrolling region only. The frozen columns never move, so
        // a track that spanned them would offer to scroll something that cannot scroll. The extra
        // pixel is the table border the track has to clear.
        HorizontalScroll.ViewportSize = scrollable;
        HorizontalScroll.LargeChange = Math.Max(24, scrollable);
        HorizontalScroll.Margin = new Thickness(frozen + 1, 2, 1, 0);

        // Overflow alone is not enough. If the frozen columns already fill the viewport there
        // is no track to draw: the bar comes out zero-wide, positioned past the right edge, and
        // still reserves the height it would have taken -- a gap under the table with nothing
        // in it, between the grid and whatever follows.
        HorizontalScroll.Visibility = max > 0.5 && scrollable > 0.5
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (Table.HorizontalOffset > max) Table.HorizontalOffset = max;
    }

    private void OnScroll(object sender, ScrollEventArgs e) => Table.HorizontalOffset = e.NewValue;

    /// <summary>Shift+wheel scrolls sideways, matching how tables behave elsewhere in Windows.</summary>
    protected override void OnPointerWheelChanged(PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        var shift = point.Properties.IsHorizontalMouseWheel
            || (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift)
                & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;
        if (!shift || HorizontalScroll.Visibility != Visibility.Visible)
        {
            base.OnPointerWheelChanged(e);
            return;
        }

        var delta = point.Properties.MouseWheelDelta;
        var next = Math.Clamp(Table.HorizontalOffset - delta, 0, HorizontalScroll.Maximum);
        Table.HorizontalOffset = next;
        HorizontalScroll.Value = next;
        e.Handled = true;
    }
}

using FluentIcons.Common;
using FluentIcons.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Helpers;
using Winhance.UI.Features.Common.Utilities;

namespace Winhance.UI.Features.Common.Dialogs;

// The rows follow Files' summary dialogs (icon, name, secondary line) and the verdict is an InfoBar, as
// PowerToys does it: a plain list of strings, or a terminal, can't show which line is the good news.
internal class SummaryDialogBuilder(ILocalizationService localization)
{
    private CheckBox? _checkBox;

    public bool CheckboxChecked => _checkBox?.IsChecked == true;

    public ContentDialog Build(XamlRoot xamlRoot, SummaryRequest request)
    {
        var content = new StackPanel
        {
            Spacing = 12,
            Width = Math.Clamp(xamlRoot.Size.Width - 96, 320, 680),
        };

        if (!string.IsNullOrEmpty(request.Message))
            content.Children.Add(new TextBlock { Text = request.Message, TextWrapping = TextWrapping.Wrap });

        if (request.Verdict is { } verdict)
        {
            content.Children.Add(new InfoBar
            {
                IsOpen = true,
                IsClosable = false,
                Severity = verdict.Tone switch
                {
                    SummaryTone.Success => InfoBarSeverity.Success,
                    SummaryTone.Warning => InfoBarSeverity.Warning,
                    SummaryTone.Error => InfoBarSeverity.Error,
                    _ => InfoBarSeverity.Informational,
                },
                Title = verdict.Title,
                Message = verdict.Message ?? string.Empty,
            });
        }

        if (request.Sections.Count > 0)
        {
            var sections = new StackPanel { Spacing = 16 };
            foreach (var section in request.Sections)
                sections.Children.Add(BuildSection(section));

            content.Children.Add(new ScrollViewer
            {
                Content = sections,
                // The dialog does not scroll its own content, so the list gives up the height of everything else
                // in the window or the checkbox below it is squeezed flat (measured on a 722 DIP window).
                MaxHeight = Math.Max(160, xamlRoot.Size.Height - 300
                    - (request.Verdict is null ? 0 : 72)
                    - (string.IsNullOrEmpty(request.CheckboxText) ? 0 : 44)),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(0, 0, 16, 0),
            });
        }

        if (!string.IsNullOrEmpty(request.CheckboxText))
        {
            _checkBox = new CheckBox { Content = request.CheckboxText };
            content.Children.Add(_checkBox);
        }

        var asks = !string.IsNullOrEmpty(request.ConfirmButtonText);
        var dialog = new ContentDialog
        {
            Title = request.Title,
            Content = content,
            PrimaryButtonText = asks ? request.ConfirmButtonText : string.Empty,
            CloseButtonText = !string.IsNullOrEmpty(request.CancelButtonText)
                ? request.CancelButtonText
                : localization.GetString(asks ? "Button_Cancel" : "Button_Close"),
            DefaultButton = asks ? ContentDialogButton.Primary : ContentDialogButton.Close,
        };
        dialog.Resources["ContentDialogMaxWidth"] = 760d;

        if (request.CopyText is { } copyText)
        {
            dialog.SecondaryButtonText = localization.GetString("Button_CopyToClipboard");

            // Copying must not dismiss: on a question, a dismissal would read as the answer.
            dialog.SecondaryButtonClick += (_, args) =>
            {
                args.Cancel = true;
                var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
                package.SetText(copyText);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
            };
        }

        return dialog;
    }

    private static StackPanel BuildSection(SummarySection section)
    {
        var panel = new StackPanel { Spacing = 4 };
        if (!string.IsNullOrEmpty(section.Heading))
        {
            panel.Children.Add(new TextBlock
            {
                Text = section.Heading,
                Style = CodeBuiltStyle.Named("BodyStrongTextBlockStyle"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 4),
            });
        }

        foreach (var row in section.Rows)
            panel.Children.Add(BuildRow(row));

        return panel;
    }

    private static Border BuildRow(SummaryRow row)
    {
        var icon = new FluentIcon
        {
            Icon = GlyphFor(row),
            IconVariant = IconVariant.Regular,
            FontSize = 20,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 1, 0, 0),
        };
        if (ToneStyle(row.Tone) is { } style)
            icon.Style = CodeBuiltStyle.Named(style);

        var text = new StackPanel { Spacing = 2 };
        text.Children.Add(new TextBlock { Text = row.Title, TextWrapping = TextWrapping.Wrap });
        if (!string.IsNullOrEmpty(row.Detail))
            text.Children.Add(SecondaryText(row.Detail));
        if (!string.IsNullOrEmpty(row.Code))
        {
            var code = SecondaryText(row.Code);
            code.FontFamily = TerminalLineRenderer.MonoFont;
            text.Children.Add(code);
        }

        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(text, 1);
        grid.Children.Add(icon);
        grid.Children.Add(text);

        return new Border
        {
            Child = grid,
            Padding = new Thickness(12, 10, 12, 10),
            CornerRadius = new CornerRadius(4),
            Style = CodeBuiltStyle.Named("CodeBuilt.CardSurface"),
        };
    }

    private static TextBlock SecondaryText(string text) => new()
    {
        Text = text,
        Style = CodeBuiltStyle.Named("CodeBuilt.CaptionSecondaryText"),
        TextWrapping = TextWrapping.Wrap,
        IsTextSelectionEnabled = true,
    };

    private static Icon GlyphFor(SummaryRow row) => row.Icon switch
    {
        SummaryIcon.WriteFile => Icon.DocumentSave,
        SummaryIcon.Download => Icon.ArrowDownload,
        SummaryIcon.CopyFiles => Icon.FolderOpen,
        SummaryIcon.Check => Icon.ShieldTask,
        _ => row.Tone switch
        {
            SummaryTone.Success => Icon.CheckmarkCircle,
            SummaryTone.Warning => Icon.Warning,
            SummaryTone.Error => Icon.ErrorCircle,
            _ => Icon.Info,
        },
    };

    private static string? ToneStyle(SummaryTone tone) => tone switch
    {
        SummaryTone.Success => "CodeBuilt.SuccessIcon",
        SummaryTone.Warning => "CodeBuilt.CautionIcon",
        SummaryTone.Error => "CodeBuilt.CriticalIcon",
        _ => null,
    };
}

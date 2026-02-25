using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Utilities;

namespace Winhance.UI.Features.Common.Dialogs;

/// <summary>
/// Encapsulates the terminal rendering, sizing, live update subscription, and clipboard logic
/// for the task output dialog. Extracted from <see cref="Services.DialogService.ShowTaskOutputDialogAsync"/>
/// to keep DialogService focused on dialog lifecycle management.
/// </summary>
internal class TaskOutputDialogBuilder
{
    private readonly ILocalizationService _localization;
    private readonly ITaskProgressService _taskProgressService;

    // Live update state
    private bool _isSubscribed;
    private bool _lastLineWasProgress;
    private int _lastLineRunCount = 1;
    private EventHandler<TaskProgressDetail>? _liveHandler;

    // UI elements needed for live updates
    private Paragraph _paragraph = null!;
    private ScrollViewer _scrollViewer = null!;
    private List<string> _allLines = null!;

    public TaskOutputDialogBuilder(ILocalizationService localization, ITaskProgressService taskProgressService)
    {
        _localization = localization;
        _taskProgressService = taskProgressService;
    }

    /// <summary>
    /// Builds the ContentDialog with terminal rendering UI. The caller is responsible for
    /// calling ConfigureDialog, StartLiveUpdates (if needed), ShowAsync, and StopLiveUpdates.
    /// </summary>
    /// <param name="xamlRoot">The XamlRoot for sizing calculations.</param>
    /// <param name="title">The dialog title.</param>
    /// <param name="logMessages">Initial log messages to display.</param>
    public ContentDialog Build(XamlRoot xamlRoot, string title, IReadOnlyList<string> logMessages)
    {
        // Mutable list of all lines -- snapshot + live additions.
        // Used by Copy to Clipboard at click time.
        _allLines = new List<string>(logMessages);

        // Build a single RichTextBlock with one Paragraph containing Runs.
        // Unlike individual TextBlocks, RichTextBlock renders block characters
        // with consistent line height -- no overlapping artifacts.
        _paragraph = new Paragraph();
        foreach (var line in logMessages)
        {
            foreach (var run in TerminalLineRenderer.CreateLineRuns(line))
                _paragraph.Inlines.Add(run);
        }

        var richTextBlock = new RichTextBlock
        {
            FontFamily = TerminalLineRenderer.MonoFont,
            FontSize = 12,
            Foreground = TerminalLineRenderer.DefaultBrush,
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true
        };
        richTextBlock.Blocks.Add(_paragraph);

        _scrollViewer = new ScrollViewer
        {
            Content = richTextBlock,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(14, 10, 14, 10)
        };

        // Auto-scroll to bottom on initial load
        _scrollViewer.Loaded += (_, _) =>
            _scrollViewer.ChangeView(null, _scrollViewer.ScrollableHeight, null, true);

        var container = new Border
        {
            Child = _scrollViewer,
            Background = new SolidColorBrush(TerminalLineRenderer.TerminalBackground),
            CornerRadius = new CornerRadius(6),
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x3E, 0x3E, 0x3E)),
            BorderThickness = new Thickness(1)
        };

        var dialog = new ContentDialog
        {
            Title = title,
            Content = container,
            CloseButtonText = _localization.GetString("Button_Close") ?? "Close",
            SecondaryButtonText = _localization.GetString("Button_CopyToClipboard") ?? "Copy to Clipboard",
            DefaultButton = ContentDialogButton.Close
        };

        // Blow out WinUI's built-in ContentDialog size caps so that the
        // SizeChanged handler below can drive actual content dimensions.
        dialog.Resources["ContentDialogMaxWidth"] = 8192;
        dialog.Resources["ContentDialogMaxHeight"] = 4096;

        dialog.SizeChanged += (_, _) =>
        {
            if (dialog.Content is FrameworkElement content && xamlRoot.Size.Width > 0)
            {
                double winWidth = xamlRoot.Size.Width;
                double winHeight = xamlRoot.Size.Height;

                // 90% of window width, floor 600px, minus dialog chrome padding (~48px)
                content.Width = Math.Min(Math.Max(600, winWidth * 0.90) - 48, 8192);

                // 70% of window height, floor 300px, minus title+buttons chrome (~120px)
                content.Height = Math.Min(Math.Max(300, winHeight * 0.70) - 120, 4096);
            }
        };

        // Copy to Clipboard -- build text dynamically to include live lines
        dialog.SecondaryButtonClick += (_, _) =>
        {
            var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
            dataPackage.SetText(string.Join("\n", _allLines));
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
        };

        return dialog;
    }

    /// <summary>
    /// Subscribes to live progress events and appends/replaces terminal lines in real-time.
    /// Should be called after ConfigureDialog and before ShowAsync if the task is running.
    /// </summary>
    public void StartLiveUpdates(DispatcherQueue dispatcherQueue)
    {
        if (!_taskProgressService.IsTaskRunning)
            return;

        _isSubscribed = true;

        _liveHandler = (_, detail) =>
        {
            if (string.IsNullOrEmpty(detail.TerminalOutput))
                return;

            var line = detail.TerminalOutput;
            var isProgress = detail.IsProgressIndicator;

            dispatcherQueue.TryEnqueue(() =>
            {
                // Always remove the last progress line
                // before adding ANY new line. This handles:
                //   progress -> progress: replacement (progress bar filling)
                //   progress -> permanent: cleanup (stale progress/spinner removed)
                //   permanent -> permanent: normal append
                //   permanent -> progress: normal append
                if (_lastLineWasProgress && _paragraph.Inlines.Count > 0)
                {
                    for (int r = 0; r < _lastLineRunCount && _paragraph.Inlines.Count > 0; r++)
                        _paragraph.Inlines.RemoveAt(_paragraph.Inlines.Count - 1);
                    _allLines.RemoveAt(_allLines.Count - 1);
                }
                else if (isProgress && _allLines.Count > 0
                    && TerminalLineRenderer.LooksLikeProgressBar(_allLines[_allLines.Count - 1]))
                {
                    // First progress bar sometimes arrives as a permanent line
                    // (winget's initial render uses \n before switching to \r).
                    // Detect and remove it so it doesn't duplicate.
                    for (int r = 0; r < _lastLineRunCount && _paragraph.Inlines.Count > 0; r++)
                        _paragraph.Inlines.RemoveAt(_paragraph.Inlines.Count - 1);
                    _allLines.RemoveAt(_allLines.Count - 1);
                }

                _allLines.Add(line);
                var runs = TerminalLineRenderer.CreateLineRuns(line);
                foreach (var run in runs)
                    _paragraph.Inlines.Add(run);
                _lastLineRunCount = runs.Length;
                _lastLineWasProgress = isProgress;

                // Auto-scroll only if the user is near the bottom;
                // if they scrolled up, leave the view where they put it.
                _scrollViewer.UpdateLayout();
                var isNearBottom = _scrollViewer.VerticalOffset
                    >= _scrollViewer.ScrollableHeight - 20;
                if (isNearBottom)
                    _scrollViewer.ChangeView(null, _scrollViewer.ScrollableHeight, null, true);
            });

            // Unsubscribe only when the overall task has actually stopped
            // (not on per-item completion signals like IsCompletion/Progress==100,
            // which fire for each item in a queued batch).
            if (!_taskProgressService.IsTaskRunning)
            {
                if (_isSubscribed)
                {
                    _isSubscribed = false;
                    _taskProgressService.ProgressUpdated -= _liveHandler;
                }
            }
        };

        _taskProgressService.ProgressUpdated += _liveHandler;
    }

    /// <summary>
    /// Unsubscribes from live progress events. Should be called in a finally block after ShowAsync.
    /// </summary>
    public void StopLiveUpdates()
    {
        if (_isSubscribed && _liveHandler != null)
        {
            _isSubscribed = false;
            _taskProgressService.ProgressUpdated -= _liveHandler;
        }
    }
}

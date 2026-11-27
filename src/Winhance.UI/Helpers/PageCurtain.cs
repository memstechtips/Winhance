using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Controls;
using Winhance.UI.Features.Common.Helpers;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Helpers;

// Building a page holds the UI thread, so a click on a cold page would sit on the old one and the new one would
// appear in pieces. The frame hides behind the rocket first, which the compositor keeps flying while the page is
// built, and shows again once the page has laid itself out.
internal sealed class PageCurtain
{
    // A page that never reports ready is shown anyway rather than leaving the frame hidden.
    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(15);

    private readonly Frame _frame;
    private readonly RocketLoader _loader;
    private readonly ILogService _logService;
    private readonly ColdPages _coldPages = new();
    private int _navigation;

    public PageCurtain(Frame frame, RocketLoader loader, ILogService logService)
    {
        _frame = frame;
        _loader = loader;
        _logService = logService;
        _frame.Navigated += OnNavigated;
    }

    // Only the latest call navigates: a click made while an earlier page waits for its loader to paint replaces it.
    // Completes once the page is opened or replaced, not when it is shown.
    public Task NavigateAsync(string tag, Action navigate)
    {
        var navigation = ++_navigation;
        if (!_coldPages.IsCold(tag))
        {
            navigate();
            Reveal();
            return Task.CompletedTask;
        }

        Cover();
        var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        OpenBehindCurtainAsync(navigation, navigate, opened).FireAndForget(_logService);
        return opened.Task;
    }

    private async Task OpenBehindCurtainAsync(int navigation, Action navigate, TaskCompletionSource opened)
    {
        try
        {
            await RenderFrames.NextAsync();
            await RenderFrames.NextAsync();
            if (navigation != _navigation)
                return;

            navigate();
            opened.TrySetResult();
            if (_frame.Content is IPageReadiness page)
                await Task.WhenAny(page.WhenReadyAsync(), Task.Delay(ReadyTimeout));

            await RenderFrames.NextAsync();
            await RenderFrames.NextAsync();
        }
        finally
        {
            opened.TrySetResult();
            if (navigation == _navigation)
                Reveal();
        }
    }

    // Abandons a navigation still waiting, for a click back to the page already shown.
    public void Cancel()
    {
        _navigation++;
        Reveal();
    }

    private void OnNavigated(object sender, NavigationEventArgs e) =>
        _coldPages.Arrived(NavigationRouter.PageTypeNameToTag.GetValueOrDefault(e.SourcePageType.Name));

    private void Cover()
    {
        _frame.Opacity = 0;
        _frame.IsHitTestVisible = false;
        _loader.IsActive = true;
    }

    private void Reveal()
    {
        _frame.Opacity = 1;
        _frame.IsHitTestVisible = true;
        _loader.IsActive = false;
    }
}

using Microsoft.UI.Xaml.Media;

namespace Winhance.UI.Features.Common.Helpers;

internal static class RenderFrames
{
    // Rendering fires just before a frame is drawn, so the second await in a row means the frame after this one,
    // with whatever was changed before the first, is on screen.
    public static Task NextAsync()
    {
        var frame = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnRendering(object? sender, object e)
        {
            CompositionTarget.Rendering -= OnRendering;
            frame.SetResult();
        }
        CompositionTarget.Rendering += OnRendering;
        return frame.Task;
    }
}

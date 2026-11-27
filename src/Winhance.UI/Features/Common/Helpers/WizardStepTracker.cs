namespace Winhance.UI.Features.Common.Helpers;

// Which step the page is showing, from where the steps sit in the scrolled content.
internal static class WizardStepTracker
{
    // A step counts as current once its top passes this line, a quarter of the way down the viewport.
    private const double AnchorFraction = 0.25;
    private const double MaxAnchor = 160;

    // A step lower on the page than the scroller can ever reach would otherwise never become current,
    // so the bottom of the scroll belongs to the last step.
    public static int CurrentStep(IReadOnlyList<double> stepTops, double offset, double viewport, double scrollable)
    {
        if (stepTops.Count == 0 || scrollable <= 0)
            return 1;

        if (offset >= scrollable - 1)
            return stepTops.Count;

        var line = offset + Math.Min(viewport * AnchorFraction, MaxAnchor);
        var current = 1;
        for (var i = 0; i < stepTops.Count; i++)
        {
            if (stepTops[i] <= line)
                current = i + 1;
        }
        return current;
    }
}

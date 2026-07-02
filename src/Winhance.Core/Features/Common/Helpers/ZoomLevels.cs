using System;

namespace Winhance.Core.Features.Common.Helpers;

/// <summary>
/// Pure math for the app UI zoom feature: a 100%–175% range in 10% steps.
/// No UI dependencies, so it is fully unit-testable.
/// </summary>
public static class ZoomLevels
{
    public const double Min = 1.0;
    public const double Max = 1.75;
    public const double Step = 0.10;
    public const double Default = 1.0;

    /// <summary>Clamps a factor into the [Min, Max] range (NaN -> Min).</summary>
    public static double Clamp(double factor)
    {
        if (double.IsNaN(factor) || factor < Min) return Min;
        if (factor > Max) return Max;
        return factor;
    }

    /// <summary>Snaps a factor to the nearest 10% grid stop, then clamps.</summary>
    public static double SnapToStep(double factor)
    {
        if (double.IsNaN(factor)) return Min;
        var steps = Math.Round((factor - Min) / Step);
        return Clamp(Min + steps * Step);
    }

    /// <summary>One step up from the (snapped) factor, capped at Max.</summary>
    public static double Next(double factor) => Clamp(SnapToStep(factor) + Step);

    /// <summary>One step down from the (snapped) factor, floored at Min.</summary>
    public static double Previous(double factor) => Clamp(SnapToStep(factor) - Step);
}

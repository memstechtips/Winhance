namespace Winhance.Core.Features.Common.Helpers;

public static class ZoomLevels
{
    public const double Min = 1.0;
    public const double Max = 1.75;
    public const double Step = 0.10;
    public const double Default = 1.0;

    // NaN clamps to Min.
    public static double Clamp(double factor)
    {
        if (double.IsNaN(factor) || factor < Min) return Min;
        if (factor > Max) return Max;
        return factor;
    }

    public static double SnapToStep(double factor)
    {
        if (double.IsNaN(factor)) return Min;
        var steps = Math.Round((factor - Min) / Step);
        return Clamp(Min + steps * Step);
    }

    public static double Next(double factor) => Clamp(SnapToStep(factor) + Step);

    public static double Previous(double factor) => Clamp(SnapToStep(factor) - Step);
}

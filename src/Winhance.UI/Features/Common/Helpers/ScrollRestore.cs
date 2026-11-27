namespace Winhance.UI.Features.Common.Helpers;

// One restore of a saved offset, stepped once a frame. It ends when the offset is reached, when the extent has held
// still for SettleFrames, or after MaxFrames, so an offset the page can no longer reach (a taller window, a collapsed
// step) is never chased. Every offset it hands out is remembered, so a view change it did not cause can be told apart.
internal sealed class ScrollRestore(double target, double startOffset)
{
    internal const int SettleFrames = 5;
    internal const int MaxFrames = 60;

    private readonly List<double> _offsets = [startOffset];
    private double _lastScrollable = double.NaN;
    private int _frames;
    private int _stillFrames;

    public double Target => target;

    public bool IsDone { get; private set; }

    public double NextOffset(double scrollable)
    {
        _frames++;
        // An extent of zero is content that has not arrived yet, not content that stopped growing.
        _stillFrames = scrollable > 0 && scrollable == _lastScrollable ? _stillFrames + 1 : 0;
        _lastScrollable = scrollable;
        IsDone = scrollable >= target || _stillFrames >= SettleFrames || _frames >= MaxFrames;

        var offset = Math.Clamp(target, 0, Math.Max(0, scrollable));
        _offsets.Add(offset);
        return offset;
    }

    public bool CausedOffset(double offset) => _offsets.Exists(o => Math.Abs(o - offset) < 1);
}

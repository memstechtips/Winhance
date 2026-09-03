using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.AdvancedTools.Services;

// One redrawing percent bar with a speed readout, shared by the media copy and the ISO write.
// CopyFileEx and IMAPI2 both call back thousands of times per gigabyte; only a whole-percent
// change or a fresh speed sample is worth pushing at the UI.
internal sealed class ByteProgressReporter(
    IProgress<TaskProgressDetail>? progress,
    TransferRate rate,
    Func<int, string> statusForPercent)
{
    private int _lastPercent = -1;

    public void Report(long done, long total, string label)
    {
        var percent = total <= 0 ? 0 : (int)Math.Min(100, done * 100 / total);
        var freshRate = rate.Update(done);
        if (percent == _lastPercent && !freshRate)
        {
            return;
        }

        _lastPercent = percent;

        var speed = rate.ToString();
        progress?.Report(new TaskProgressDetail
        {
            Progress = percent,
            StatusText = statusForPercent(percent),
            TerminalOutput = speed.Length == 0 ? label : $"{label} ({speed})",
            IsProgressIndicator = true
        });
    }
}

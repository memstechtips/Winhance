namespace Winhance.UI.Helpers;

// Back returns through the normal page open, so a page in another mode still asks before it switches. That open
// lands here as an ordinary navigation, which is told apart from a forward one by the tag Back asked for.
internal sealed class NavHistory
{
    private const int Limit = 20;

    private readonly List<string> _left = [];
    private string? _current;
    private string? _returningTo;

    public bool CanGoBack => _left.Count > 0;

    public string? BeginBack() => _returningTo = CanGoBack ? _left[^1] : null;

    // Arrived pops the entry when the page opens, so one still pending here was never reached: the user declined the
    // mode switch, or the target was already showing and the router skipped the navigation. Either way it is dropped,
    // or every later Back would land on it again and get no further.
    public void EndBack()
    {
        if (_returningTo is not null)
            _left.RemoveAt(_left.Count - 1);
        _returningTo = null;
    }

    public void Arrived(string? tag)
    {
        if (tag is null || tag == _current) return;

        if (tag == _returningTo)
        {
            _left.RemoveAt(_left.Count - 1);
            _returningTo = null;
        }
        else if (_current is not null)
        {
            _left.Add(_current);
            if (_left.Count > Limit)
                _left.RemoveAt(0);
        }

        _current = tag;
    }
}

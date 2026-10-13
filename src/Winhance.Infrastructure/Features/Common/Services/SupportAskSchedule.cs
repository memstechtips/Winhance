using System.Globalization;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services;

internal class SupportAskSchedule(IUserPreferencesService prefs, TimeProvider timeProvider) : ISupportAskSchedule
{
    private static readonly TimeSpan AskInterval = TimeSpan.FromHours(24);

    public async Task<bool> ShouldAskOnExitAsync()
    {
        var now = timeProvider.GetUtcNow();

        // An asked-at time in the future means the clock was set back; ask rather than stay silent until it catches up.
        var askedAt = await ReadAsync(UserPreferenceKeys.SupportAskedAt).ConfigureAwait(false);
        return askedAt == null || askedAt > now || now - askedAt.Value >= AskInterval;
    }

    public Task MarkAskedAsync() =>
        WriteAsync(UserPreferenceKeys.SupportAskedAt, timeProvider.GetUtcNow());

    private async Task<DateTimeOffset?> ReadAsync(string key)
    {
        var raw = await prefs.GetPreferenceAsync(key, string.Empty).ConfigureAwait(false);
        return DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var value)
            ? value
            : null;
    }

    private Task WriteAsync(string key, DateTimeOffset value) =>
        prefs.SetPreferenceAsync(key, value.ToString("O", CultureInfo.InvariantCulture));
}

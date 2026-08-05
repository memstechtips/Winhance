using Moq;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.TestSupport;

/// <summary>
/// Keeps a mocked <see cref="ILocalizationService"/>'s two lookups consistent with each other.
///
/// Moq auto-implements every interface member, so an unstubbed TryGetString silently answers
/// "missing" for every key - which turns each existing GetString stub into a no-op at run time
/// with no compiler warning. Call <see cref="MirrorTryGetString"/> once in a fixture and both
/// methods answer from the same stubs.
/// </summary>
public static class LocalizationMock
{
    private delegate bool TryGetStringReturns(string key, out string value);

    /// <summary>
    /// Makes TryGetString answer from whatever GetString is stubbed to return, treating null,
    /// empty and the real service's "[key]" miss-marker as absent. Per-key GetString stubs added
    /// after this call are picked up too - the lookup happens when the mock is called, not now.
    /// </summary>
    public static Mock<ILocalizationService> MirrorTryGetString(this Mock<ILocalizationService> mock)
    {
        mock.Setup(l => l.TryGetString(It.IsAny<string>(), out It.Ref<string>.IsAny))
            .Returns(new TryGetStringReturns((string key, out string value) =>
            {
                var result = mock.Object.GetString(key);
                bool found = !string.IsNullOrEmpty(result) && result != $"[{key}]";
                value = found ? result : string.Empty;
                return found;
            }));

        return mock;
    }

    /// <summary>Declares one key absent, whatever GetString says about it. Overrides
    /// <see cref="MirrorTryGetString"/> for that key - in Moq the last matching setup wins.</summary>
    public static Mock<ILocalizationService> MissingKey(this Mock<ILocalizationService> mock, string key)
    {
        mock.Setup(l => l.TryGetString(key, out It.Ref<string>.IsAny))
            .Returns(new TryGetStringReturns((string _, out string value) =>
            {
                value = string.Empty;
                return false;
            }));

        return mock;
    }

    /// <summary>Declares one key present with an exact value, bypassing the "[key]" heuristic - the
    /// way to test that a translation which legitimately looks like the miss-marker is still used.</summary>
    public static Mock<ILocalizationService> PresentKey(
        this Mock<ILocalizationService> mock, string key, string value)
    {
        mock.Setup(l => l.GetString(key)).Returns(value);
        mock.Setup(l => l.TryGetString(key, out It.Ref<string>.IsAny))
            .Returns(new TryGetStringReturns((string _, out string outValue) =>
            {
                outValue = value;
                return true;
            }));

        return mock;
    }
}

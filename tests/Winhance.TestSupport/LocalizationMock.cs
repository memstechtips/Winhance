using Moq;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.TestSupport;

// Moq auto-implements every member, so an unstubbed TryGetString answers "missing" for every key - which turns
// each GetString stub into a no-op at run time with no compiler warning.
public static class LocalizationMock
{
    private delegate bool TryGetStringReturns(string key, out string value);

    // Treats null, empty and the real service's "[key]" miss-marker as absent; per-key stubs added after this call
    // are picked up too - the lookup happens when the mock is called.
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

    // Overrides MirrorTryGetString for that key - in Moq the last matching setup wins.
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

    // Bypasses the "[key]" heuristic - the way to test that a translation which legitimately looks like the miss-marker is still used.
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

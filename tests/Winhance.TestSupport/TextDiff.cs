namespace Winhance.TestSupport;

public static class TextDiff
{
    public static string NormalizeLineEndings(string text) => text.Replace("\r\n", "\n");

    public static string FirstDifference(string expected, string actual, string expectedLabel, string actualLabel)
    {
        var left = expected.Split('\n');
        var right = actual.Split('\n');
        for (var i = 0; i < Math.Max(left.Length, right.Length); i++)
        {
            var e = i < left.Length ? left[i] : "<end>";
            var a = i < right.Length ? right[i] : "<end>";
            if (e != a)
                return $"line {i + 1} differs. {expectedLabel}: {e.Trim()} | {actualLabel}: {a.Trim()}";
        }

        return "no line differs";
    }
}

using System.Text;

namespace Winhance.UI.Features.SoftwareApps.ViewModels;

// RFC 4180: a value holding a comma, a quote or a line break is quoted, with its quotes doubled.
internal static class AppListCsv
{
    public static string Build<T>(IReadOnlyList<(string Header, Func<T, string> Value)> columns, IEnumerable<T> rows)
    {
        var csv = new StringBuilder();
        csv.Append(string.Join(',', columns.Select(column => Quote(column.Header)))).Append("\r\n");
        foreach (var row in rows)
            csv.Append(string.Join(',', columns.Select(column => Quote(column.Value(row))))).Append("\r\n");
        return csv.ToString();
    }

    private static string Quote(string value) =>
        value.AsSpan().IndexOfAny(",\"\r\n") < 0 ? value : "\"" + value.Replace("\"", "\"\"") + "\"";
}

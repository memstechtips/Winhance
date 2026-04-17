using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Winhance.IntegrationTests.Localization;

public class LocalizationJsonValidityTests
{
    private static readonly string LocalizationFolder = FindLocalizationFolder();

    private static string FindLocalizationFolder()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Winhance.sln")))
        {
            dir = dir.Parent;
        }

        if (dir == null)
            throw new DirectoryNotFoundException(
                "Could not find repository root (Winhance.sln) from " + AppContext.BaseDirectory);

        return Path.Combine(dir.FullName, "src", "Winhance.UI", "Features", "Common", "Localization");
    }

    public static IEnumerable<object[]> AllJsonFiles()
    {
        foreach (var file in Directory.GetFiles(LocalizationFolder, "*.json"))
        {
            yield return [Path.GetFileName(file)];
        }
    }

    [Theory]
    [MemberData(nameof(AllJsonFiles))]
    public void LocalizationFile_ShouldBeValidJson(string fileName)
    {
        var filePath = Path.Combine(LocalizationFolder, fileName);
        var json = File.ReadAllText(filePath);

        var act = () => JsonDocument.Parse(json);

        act.Should().NotThrow<JsonException>(
            because: $"{fileName} must be valid JSON");
    }

    [Theory]
    [MemberData(nameof(AllJsonFiles))]
    public void LocalizationFile_ShouldHaveFlatStringKeyValueStructure(string fileName)
    {
        var filePath = Path.Combine(LocalizationFolder, fileName);
        var json = File.ReadAllText(filePath);
        using var doc = JsonDocument.Parse(json);

        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Object,
            because: $"{fileName} root must be a JSON object");

        foreach (var property in doc.RootElement.EnumerateObject())
        {
            property.Value.ValueKind.Should().Be(JsonValueKind.String,
                because: $"{fileName} key \"{property.Name}\" must have a string value");
        }
    }

    [Theory]
    [MemberData(nameof(AllJsonFiles))]
    public void LocalizationFile_ShouldHaveSameKeysAsEnglish(string fileName)
    {
        if (fileName == "en.json")
            return;

        var enPath = Path.Combine(LocalizationFolder, "en.json");
        var enKeys = GetKeys(enPath);

        var filePath = Path.Combine(LocalizationFolder, fileName);
        var fileKeys = GetKeys(filePath);

        var missingKeys = enKeys.Except(fileKeys).ToList();
        var extraKeys = fileKeys.Except(enKeys).ToList();

        missingKeys.Should().BeEmpty(
            because: $"{fileName} is missing keys that exist in en.json");
        extraKeys.Should().BeEmpty(
            because: $"{fileName} has extra keys not present in en.json");
    }

    [Theory]
    [MemberData(nameof(AllJsonFiles))]
    public void LocalizationFile_ShouldNotHaveEmptyValues(string fileName)
    {
        var filePath = Path.Combine(LocalizationFolder, fileName);
        var json = File.ReadAllText(filePath);
        using var doc = JsonDocument.Parse(json);

        foreach (var property in doc.RootElement.EnumerateObject())
        {
            var value = property.Value.GetString();
            value.Should().NotBeNullOrWhiteSpace(
                because: $"{fileName} key \"{property.Name}\" should not be empty");
        }
    }

    [Theory]
    [MemberData(nameof(AllJsonFiles))]
    public void LocalizationFile_ShouldNotContainUnicodeEscapes(string fileName)
    {
        var filePath = Path.Combine(LocalizationFolder, fileName);
        var lines = File.ReadAllLines(filePath);
        var pattern = new Regex(@"\\u[0-9a-fA-F]{4}");

        var offenders = lines
            .Select((line, idx) => (Line: line, Number: idx + 1))
            .Where(x => pattern.IsMatch(x.Line))
            .Select(x => $"line {x.Number}: {x.Line.Trim()}")
            .ToList();

        offenders.Should().BeEmpty(
            because: $"{fileName} must not contain \\uXXXX unicode escape sequences — write the actual character directly (e.g. & instead of \\u0026, ê instead of \\u00ea). The only allowed escape is \\n for newlines.");
    }

    [Theory]
    [MemberData(nameof(AllJsonFiles))]
    public void LocalizationFile_ShouldNotContainEscapedDoubleQuotes(string fileName)
    {
        var filePath = Path.Combine(LocalizationFolder, fileName);
        var lines = File.ReadAllLines(filePath);
        var pattern = new Regex(@"\\""");

        var offenders = lines
            .Select((line, idx) => (Line: line, Number: idx + 1))
            .Where(x => pattern.IsMatch(x.Line))
            .Select(x => $"line {x.Number}: {x.Line.Trim()}")
            .ToList();

        offenders.Should().BeEmpty(
            because: $"{fileName} must not contain escaped ASCII double quotes (\\\") inside string values — use single quotes (') or typographic curly quotes (\u201C \u201D \u201E) directly instead.");
    }

    private static HashSet<string> GetKeys(string filePath)
    {
        var json = File.ReadAllText(filePath);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.EnumerateObject()
            .Select(p => p.Name)
            .ToHashSet();
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Winhance.UI.Features.Common.Constants;

internal static class ConfigFileConstants
{
    public const string FileExtension = ".winhance";
    public const string FileFilter = "Winhance Configuration Files";
    public const string FilePattern = "*.winhance";

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

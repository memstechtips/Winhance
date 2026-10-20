using System.Text.Json;
using System.Text.Json.Serialization;

namespace Winhance.Core.Features.Common.Constants;

public static class ConfigFileConstants
{
    public const string FileExtension = ".winhance";
    public const string FileFilter = "Winhance Configuration Files";
    public const string FilePattern = "*.winhance";
    public const string ConfigOrAnswerFileFilter = "Winhance Configuration or Answer Files";
    public const string ConfigOrAnswerFilePattern = "*.winhance;*.xml";

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

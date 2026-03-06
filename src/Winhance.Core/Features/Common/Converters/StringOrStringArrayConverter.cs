using System.Text.Json;
using System.Text.Json.Serialization;

namespace Winhance.Core.Features.Common.Converters;

/// <summary>
/// Converts a JSON value that may be either a single string or a string array
/// into a string[]. This handles backward compatibility for config files that
/// serialized AppxPackageName as a plain string instead of a string array.
/// </summary>
public class StringOrStringArrayConverter : JsonConverter<string[]?>
{
    public override string[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();
            return value != null ? new[] { value } : null;
        }

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var list = new List<string>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                    break;

                if (reader.TokenType == JsonTokenType.String)
                {
                    var item = reader.GetString();
                    if (item != null)
                        list.Add(item);
                }
            }
            return list.ToArray();
        }

        throw new JsonException($"Unexpected token type {reader.TokenType} when reading string array.");
    }

    public override void Write(Utf8JsonWriter writer, string[]? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartArray();
        foreach (var item in value)
        {
            writer.WriteStringValue(item);
        }
        writer.WriteEndArray();
    }
}

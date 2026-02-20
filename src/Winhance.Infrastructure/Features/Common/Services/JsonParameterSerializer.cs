using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services
{
    public class JsonParameterSerializer : IParameterSerializer
    {
        private readonly JsonSerializerOptions _options = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };

        public int MaxParameterSize { get; set; } = 1024 * 1024; // 1MB default
        public bool UseCompression { get; set; } = true;

        public string? Serialize(object parameter)
        {
            if (parameter == null) return null;

            var json = JsonSerializer.Serialize(parameter, _options);

            if (UseCompression && json.Length > 1024) // Compress if over 1KB
            {
                var bytes = Encoding.UTF8.GetBytes(json);
                using var output = new MemoryStream();
                using (var gzip = new GZipStream(output, CompressionMode.Compress))
                {
                    gzip.Write(bytes, 0, bytes.Length);
                }
                return Convert.ToBase64String(output.ToArray());
            }

            return json;
        }

        // Corrected signature to match IParameterSerializer
        public object? Deserialize(Type targetType, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            try
            {
                if (IsCompressed(value)) // Use 'value' parameter
                {
                    var bytes = Convert.FromBase64String(value); // Use 'value' parameter
                    using var input = new MemoryStream(bytes);
                    using var output = new MemoryStream();
                    using (var gzip = new GZipStream(input, CompressionMode.Decompress))
                    {
                        gzip.CopyTo(output);
                    }
                    var json = Encoding.UTF8.GetString(output.ToArray());
                    return JsonSerializer.Deserialize(json, targetType, _options);
                }

                return JsonSerializer.Deserialize(value, targetType, _options); // Use 'value' parameter
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException("Failed to deserialize parameter", ex);
            }
            catch (Exception ex) when (ex is FormatException || ex is InvalidDataException)
            {
                throw new InvalidOperationException("Invalid compressed parameter format", ex);
            }
        }

        public T Deserialize<T>(string serialized)
        {
            if (string.IsNullOrWhiteSpace(serialized)) return default!;

            try
            {
                if (IsCompressed(serialized))
                {
                    var bytes = Convert.FromBase64String(serialized);
                    using var input = new MemoryStream(bytes);
                    using var output = new MemoryStream();
                    using (var gzip = new GZipStream(input, CompressionMode.Decompress))
                    {
                        gzip.CopyTo(output);
                    }
                    var json = Encoding.UTF8.GetString(output.ToArray());
                    return JsonSerializer.Deserialize<T>(json, _options)!;
                }

                return JsonSerializer.Deserialize<T>(serialized, _options)!;
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"Failed to deserialize parameter to type {typeof(T).Name}", ex);
            }
            catch (Exception ex) when (ex is FormatException || ex is InvalidDataException)
            {
                throw new InvalidOperationException("Invalid compressed parameter format", ex);
            }
        }

        private bool IsCompressed(string input)
        {
            if (string.IsNullOrEmpty(input)) return false;
            try
            {
                // Simple check - compressed data is base64 and starts with H4sI (GZip magic number)
                return input.Length > 4 &&
                       input.StartsWith("H4sI") &&
                       input.Length % 4 == 0;
            }
            catch
            {
                return false;
            }
        }
    }
}

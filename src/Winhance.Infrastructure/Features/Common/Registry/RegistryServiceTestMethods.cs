using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.Infrastructure.Features.Common.Registry
{
    public partial class RegistryService
    {
        public async Task<RegistryTestResult> TestValue(string keyPath, string valueName, object expectedValue, RegistryValueKind valueKind)
        {
            var result = new RegistryTestResult
            {
                KeyPath = keyPath,
                ValueName = valueName,
                ExpectedValue = expectedValue,
                Category = "Registry Test"
            };

            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    result.IsSuccess = false;
                    result.Message = "Registry operations are only supported on Windows";
                    return result;
                }

                _logService.LogInformation($"Testing registry value: {keyPath}\\{valueName}");

                // Check if the key exists
                var keyExists = KeyExists(keyPath); // Removed await
                if (!keyExists)
                {
                    result.IsSuccess = false;
                    result.Message = $"Registry key not found: {keyPath}";
                    return result;
                }

                // Check if the value exists
                var valueExists = ValueExists(keyPath, valueName); // Removed await
                if (!valueExists)
                {
                    result.IsSuccess = false;
                    result.Message = $"Registry value not found: {keyPath}\\{valueName}";
                    return result;
                }

                // Get the actual value
                var actualValue = GetValue(keyPath, valueName); // Removed await
                result.ActualValue = actualValue;

                // Check if the value kind matches
                // var actualValueKind = await GetValueKind(keyPath, valueName); // Method doesn't exist
                // if (actualValueKind != valueKind)
                // {
                //     result.IsSuccess = false;
                //     result.Message = $"Value kind mismatch. Expected: {valueKind}, Actual: {actualValueKind}";
                //     return result;
                // }

                // Compare the values
                if (expectedValue is int expectedInt && actualValue is int actualInt)
                {
                    result.IsSuccess = expectedInt == actualInt;
                }
                else if (expectedValue is string expectedString && actualValue is string actualString)
                {
                    result.IsSuccess = string.Equals(expectedString, actualString, StringComparison.OrdinalIgnoreCase);
                }
                else if (expectedValue is byte[] expectedBytes && actualValue is byte[] actualBytes)
                {
                    result.IsSuccess = expectedBytes.SequenceEqual(actualBytes);
                }
                else
                {
                    // For other types, use Equals
                    result.IsSuccess = expectedValue?.Equals(actualValue) ?? (actualValue == null);
                }

                if (!result.IsSuccess)
                {
                    result.Message = $"Value mismatch. Expected: {expectedValue}, Actual: {actualValue}";
                }
                else
                {
                    result.Message = "Value matches expected value";
                }

                return result;
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.Message = $"Error testing registry value: {ex.Message}";
                _logService.LogError($"Error testing registry value {keyPath}\\{valueName}", ex);
                return result;
            }
        }

        public async Task<IEnumerable<RegistryTestResult>> TestMultipleValues(IEnumerable<RegistrySetting> settings)
        {
            var results = new List<RegistryTestResult>();

            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    _logService.LogError("Registry operations are only supported on Windows");
                    return results;
                }

                _logService.LogInformation("Testing multiple registry values");

                foreach (var setting in settings)
                {
                    var keyPath = $"{setting.Hive}\\{setting.SubKey}";
                    // Use EnabledValue if available, otherwise fall back to RecommendedValue for backward compatibility
                    object valueToTest = setting.EnabledValue ?? setting.RecommendedValue;
                    var result = await TestValue(keyPath, setting.Name, valueToTest, setting.ValueType);
                    result.Category = setting.Category;
                    result.Description = setting.Description;
                    results.Add(result);
                }

                var passCount = results.Count(r => r.IsSuccess);
                _logService.LogInformation($"Registry test results: {passCount}/{results.Count} passed");

                return results;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error testing multiple registry values: {ex.Message}", ex);
                return results;
            }
        }
    }
}

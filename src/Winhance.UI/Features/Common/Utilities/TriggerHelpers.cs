namespace Winhance.UI.Features.Common.Utilities;

/// <summary>
/// Helper functions for use with StateTrigger x:Bind in XAML.
/// These replace WPF DataTrigger patterns that don't exist in WinUI 3.
/// </summary>
/// <example>
/// <![CDATA[
/// <StateTrigger IsActive="{x:Bind local:TriggerHelpers.Not(IsEnabled), Mode=OneWay}"/>
/// <StateTrigger IsActive="{x:Bind local:TriggerHelpers.IsEqual(Status, 'Running'), Mode=OneWay}"/>
/// ]]>
/// </example>
public static class TriggerHelpers
{
    /// <summary>
    /// Returns the inverse of a boolean value.
    /// </summary>
    public static bool Not(bool value) => !value;

    /// <summary>
    /// Returns true if both values are equal.
    /// </summary>
    public static bool IsEqual(object? value, object? compareTo) => Equals(value, compareTo);

    /// <summary>
    /// Returns true if values are not equal.
    /// </summary>
    public static bool IsNotEqual(object? value, object? compareTo) => !Equals(value, compareTo);

    /// <summary>
    /// Returns true if the count is zero.
    /// </summary>
    public static bool IsZero(int count) => count == 0;

    /// <summary>
    /// Returns true if the count is greater than zero.
    /// </summary>
    public static bool IsGreaterThanZero(int count) => count > 0;

    /// <summary>
    /// Returns true if the value is greater than the threshold.
    /// </summary>
    public static bool IsGreaterThan(int value, int threshold) => value > threshold;

    /// <summary>
    /// Returns true if the value is less than the threshold.
    /// </summary>
    public static bool IsLessThan(int value, int threshold) => value < threshold;

    /// <summary>
    /// Returns true if the value is greater than or equal to the threshold.
    /// </summary>
    public static bool IsGreaterThanOrEqual(int value, int threshold) => value >= threshold;

    /// <summary>
    /// Returns true if the value is less than or equal to the threshold.
    /// </summary>
    public static bool IsLessThanOrEqual(int value, int threshold) => value <= threshold;

    /// <summary>
    /// Returns true if both conditions are true (AND logic).
    /// </summary>
    public static bool And(bool a, bool b) => a && b;

    /// <summary>
    /// Returns true if any condition is true (OR logic).
    /// </summary>
    public static bool Or(bool a, bool b) => a || b;

    /// <summary>
    /// Returns true if the string is null or empty.
    /// </summary>
    public static bool IsNullOrEmpty(string? value) => string.IsNullOrEmpty(value);

    /// <summary>
    /// Returns true if the string is not null or empty.
    /// </summary>
    public static bool IsNotNullOrEmpty(string? value) => !string.IsNullOrEmpty(value);

    /// <summary>
    /// Returns true if the string is null, empty, or whitespace.
    /// </summary>
    public static bool IsNullOrWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value);

    /// <summary>
    /// Returns true if the value is null.
    /// </summary>
    public static bool IsNull(object? value) => value == null;

    /// <summary>
    /// Returns true if the value is not null.
    /// </summary>
    public static bool IsNotNull(object? value) => value != null;

    /// <summary>
    /// Returns true if the string matches the expected value (case-sensitive).
    /// </summary>
    public static bool StringEquals(string? value, string expected) =>
        string.Equals(value, expected, StringComparison.Ordinal);

    /// <summary>
    /// Returns true if the string matches the expected value (case-insensitive).
    /// </summary>
    public static bool StringEqualsIgnoreCase(string? value, string expected) =>
        string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
}

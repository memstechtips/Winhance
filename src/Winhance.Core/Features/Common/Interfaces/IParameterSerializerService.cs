namespace Winhance.Core.Interfaces.Services
{
    /// <summary>
    /// Service for serializing and deserializing navigation parameters.
    /// </summary>
    public interface IParameterSerializerService
    {
        /// <summary>
        /// Serializes an object to a string.
        /// </summary>
        /// <param name="value">The object to serialize.</param>
        /// <returns>The serialized string.</returns>
        string Serialize(object value);

        /// <summary>
        /// Deserializes a string to an object of the specified type.
        /// </summary>
        /// <typeparam name="T">The type to deserialize to.</typeparam>
        /// <param name="value">The string to deserialize.</param>
        /// <returns>The deserialized object.</returns>
        T Deserialize<T>(string value);

        /// <summary>
        /// Deserializes a string to an object of the specified type.
        /// </summary>
        /// <param name="type">The type to deserialize to.</param>
        /// <param name="value">The string to deserialize.</param>
        /// <returns>The deserialized object.</returns>
        object Deserialize(System.Type type, string value);
    }
}
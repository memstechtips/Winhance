using System;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Interface for serializing and deserializing navigation parameters.
    /// </summary>
    public interface IParameterSerializer
    {
        /// <summary>
        /// Serializes an object to a string representation.
        /// </summary>
        /// <param name="parameter">The object to serialize.</param>
        /// <returns>A string representation of the object.</returns>
        string Serialize(object parameter);

        /// <summary>
        /// Deserializes a string to an object of the specified type.
        /// </summary>
        /// <param name="targetType">The type to deserialize to.</param>
        /// <param name="value">The string to deserialize.</param>
        /// <returns>The deserialized object.</returns>
        object Deserialize(Type targetType, string value);

        /// <summary>
        /// Deserializes a string to an object of the specified type.
        /// </summary>
        /// <typeparam name="T">The type to deserialize to.</typeparam>
        /// <param name="serialized">The string to deserialize.</param>
        /// <returns>The deserialized object.</returns>
        T Deserialize<T>(string serialized);
    }
}
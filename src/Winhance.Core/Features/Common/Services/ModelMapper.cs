using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Winhance.Core.Features.Common.Services
{
    /// <summary>
    /// Interface for mapping between different model types.
    /// </summary>
    public interface IModelMapper
    {
        /// <summary>
        /// Maps a source object to a destination type.
        /// </summary>
        /// <typeparam name="TSource">The source type.</typeparam>
        /// <typeparam name="TDestination">The destination type.</typeparam>
        /// <param name="source">The source object.</param>
        /// <returns>The mapped destination object.</returns>
        TDestination Map<TSource, TDestination>(TSource source) where TDestination : new();

        /// <summary>
        /// Maps a collection of source objects to a collection of destination objects.
        /// </summary>
        /// <typeparam name="TSource">The source type.</typeparam>
        /// <typeparam name="TDestination">The destination type.</typeparam>
        /// <param name="source">The collection of source objects.</param>
        /// <returns>The collection of mapped destination objects.</returns>
        IEnumerable<TDestination> MapCollection<TSource, TDestination>(IEnumerable<TSource> source) where TDestination : new();
    }

    /// <summary>
    /// Service for mapping between different model types.
    /// </summary>
    public class ModelMapper : IModelMapper
    {
        /// <inheritdoc/>
        public TDestination Map<TSource, TDestination>(TSource source) where TDestination : new()
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var destination = new TDestination();
            MapProperties(source, destination);
            return destination;
        }

        /// <inheritdoc/>
        public IEnumerable<TDestination> MapCollection<TSource, TDestination>(IEnumerable<TSource> source) where TDestination : new()
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return source.Select(item => Map<TSource, TDestination>(item));
        }

        private static void MapProperties<TSource, TDestination>(TSource source, TDestination destination)
        {
            var sourceProperties = typeof(TSource).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var destinationProperties = typeof(TDestination).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .ToDictionary(p => p.Name, p => p);

            foreach (var sourceProperty in sourceProperties)
            {
                if (destinationProperties.TryGetValue(sourceProperty.Name, out var destinationProperty))
                {
                    if (destinationProperty.CanWrite && 
                        (destinationProperty.PropertyType == sourceProperty.PropertyType || 
                         IsAssignableOrConvertible(sourceProperty.PropertyType, destinationProperty.PropertyType)))
                    {
                        var value = sourceProperty.GetValue(source);
                        
                        if (value != null && sourceProperty.PropertyType != destinationProperty.PropertyType)
                        {
                            value = Convert.ChangeType(value, destinationProperty.PropertyType);
                        }
                        
                        destinationProperty.SetValue(destination, value);
                    }
                }
            }
        }

        private static bool IsAssignableOrConvertible(Type sourceType, Type destinationType)
        {
            return destinationType.IsAssignableFrom(sourceType) || 
                   (sourceType.IsPrimitive && destinationType.IsPrimitive) ||
                   (sourceType == typeof(string) && destinationType.IsPrimitive) ||
                   (destinationType == typeof(string) && sourceType.IsPrimitive);
        }
    }
}
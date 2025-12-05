using System;
using MahApps.Metro.IconPacks;

namespace Winhance.WPF.Features.Common.Models
{
    public record FeatureMetadata(
        string Id,                  // From FeatureIds
        string LocalizationKey,     // From StringKeys
        PackIconMaterialKind Icon,  // The specific icon enum
        Type ViewModelType,         // The actual ViewModel class
        Type ViewType,              // The actual View class
        string Category,            // "Customize", "Optimize", etc.
        int SortOrder
    );
}

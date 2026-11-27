namespace Winhance.Core.Features.Common.Models;

// IconKey names a FeatureIcons.xaml resource in Winhance.UI.
public sealed record FeatureDefinition(
    string Id,
    string DefaultName,
    string Category,
    string IconKey
);

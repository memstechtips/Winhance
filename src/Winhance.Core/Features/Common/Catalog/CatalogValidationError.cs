namespace Winhance.Core.Features.Common.Catalog;

/// <summary>One catalog-authoring rule violation found by <see cref="CatalogValidator"/>.</summary>
public sealed record CatalogValidationError(string SettingId, string Message);

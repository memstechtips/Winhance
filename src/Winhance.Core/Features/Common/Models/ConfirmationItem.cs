namespace Winhance.Core.Features.Common.Models;

public sealed record ConfirmationItem(string Name, bool HasInstabilityWarning = false, bool CannotBeReinstalled = false);

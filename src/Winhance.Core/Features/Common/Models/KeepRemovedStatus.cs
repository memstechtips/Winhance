namespace Winhance.Core.Features.Common.Models;

public sealed record KeepRemovedStatus(DateTime? LastCheck, string LastResult, IReadOnlyList<string> RemovedAgain, DateTime? LastRemovedAgain);

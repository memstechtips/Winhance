namespace Winhance.Core.Features.Common.Models;

public sealed record KeptApp(string Id, string Name, DateTime? RemovedOn, int ReturnCount, string Category = "");

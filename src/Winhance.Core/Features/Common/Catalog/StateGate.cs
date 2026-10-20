using Winhance.Core.Features.Common.Localization;

namespace Winhance.Core.Features.Common.Catalog;

public sealed record StateGate(string OtherId, IReadOnlyList<LocKey> States);

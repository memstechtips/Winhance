using System.Collections.Generic;

namespace Winhance.Core.Features.Common.Catalog;

/// <summary>Outcome of executing an apply plan (best-effort): how many ops ran, how many failed, and the
/// failure messages.</summary>
public sealed record ApplyResult(int Total, int Failed, IReadOnlyList<string> Failures)
{
    public bool AllSucceeded => Failed == 0;
}

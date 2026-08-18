namespace Winhance.Core.Features.Common.Catalog;

public sealed record ApplyResult(int Total, int Failed, IReadOnlyList<string> Failures)
{
    public bool AllSucceeded => Failed == 0;
}

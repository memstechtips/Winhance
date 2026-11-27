namespace Winhance.Core.Features.Common.Catalog;

// A list of tick boxes whose entries are only known at runtime. The choice is the ticked keys; none saved means every
// entry is ticked.
public sealed record CheckList(CheckListSource Source);

public enum CheckListSource
{
    // The editions in the install image on the media a WIMUtil session is building, keyed by image name.
    MediaEditions,
}

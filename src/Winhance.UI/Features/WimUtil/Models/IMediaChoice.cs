using FluentIcons.Common;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.WimUtil.Models;

namespace Winhance.UI.Features.WimUtil.Models;

// A step 2 to 4 option that only records what the user wants. Create applies it to the working folder, so nothing
// there changes until then.
public interface IMediaChoice
{
    // True when the user chose it and it is not on the media yet.
    bool NeedsApply { get; }

    // Names the card and the progress task Create shows while this choice is applied.
    string ApplyTitleKey { get; }

    Icon ApplyIcon { get; }

    // The image-mode servicing step that builds in what this choice puts on the media, if any.
    ServicingPhase? ServicedIn { get; }

    // Null on success, otherwise the localized reason it failed. Cancelling throws OperationCanceledException.
    Task<string?> ApplyAsync(IProgress<TaskProgressDetail> progress, CancellationToken cancellationToken);
}

using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IConfigReviewDiffService
{
    ConfigReviewDiff? GetDiffForSetting(string settingId);

    void SetSettingApproval(string settingId, bool approved);

    void SetActionApproval(string settingId, bool approved);

    IReadOnlyList<ConfigReviewDiff> GetApprovedDiffs();

    void RegisterDiff(ConfigReviewDiff diff);

    int TotalChanges { get; }

    int ApprovedChanges { get; }

    int ReviewedChanges { get; }

    int TotalConfigItems { get; }

    event EventHandler? ApprovalCountChanged;
}

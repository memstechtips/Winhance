using Winhance.Core.Features.WimUtil.Models;

namespace Winhance.Core.Features.WimUtil.Interfaces;

public interface IAnswerFileValidator
{
    Task<AnswerFileReport> ValidateAsync(string xmlPath, CancellationToken cancellationToken = default);
}

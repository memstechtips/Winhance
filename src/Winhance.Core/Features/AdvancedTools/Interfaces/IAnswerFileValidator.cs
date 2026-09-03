using Winhance.Core.Features.AdvancedTools.Models;

namespace Winhance.Core.Features.AdvancedTools.Interfaces;

public interface IAnswerFileValidator
{
    Task<AnswerFileReport> ValidateAsync(string xmlPath, CancellationToken cancellationToken = default);
}

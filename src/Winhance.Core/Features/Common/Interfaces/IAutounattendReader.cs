using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

// Config is null for a file Winhance made before it started carrying its choices.
public sealed record WinhanceAnswerFile(string Version, WinhanceConfigFile? Config);

// The twin of IAutounattendWriter. Answers null for a file Winhance did not make.
public interface IAutounattendReader
{
    Task<WinhanceAnswerFile?> ReadAsync(string path);
}

namespace Winhance.Core.Features.Common.Interfaces;

// Writes the content to a temp file and runs reg.exe import; under OTS it writes to and runs as the interactive
// user so HKCU entries land in the standard user's hive. Empty content is a no-op. Throws on a file-system or
// process EXCEPTION; a non-zero reg.exe exit is logged, not thrown.
public interface IRegImportService
{
    Task RunRegImportAsync(string regContent);
}

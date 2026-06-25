using System.Threading.Tasks;

namespace Winhance.Core.Features.Common.Interfaces;

/// <summary>Imports .reg content by writing it to a temp file and running reg.exe import, handling OTS elevation
/// (writes to and runs as the interactive user so HKCU entries land in the standard user's hive). Empty content is
/// a no-op. Throws on a file-system / process EXCEPTION (mirroring the old apply); a non-zero reg.exe exit is
/// logged, not thrown. This is the .reg-import dance the old SettingOperationExecutor runs, factored out so the new
/// catalog effect-runner (WindowsStateWriter.RunEffect) imports identically; the old inline copy is retired at
/// teardown.</summary>
public interface IRegImportService
{
    Task RunRegImportAsync(string regContent);
}

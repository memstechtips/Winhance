using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Selections;

namespace Winhance.Core.Features.Common.Interfaces;

// Path in, bytes out - no picker, no dialogs; the twin of IAutounattendWriter. Returns the bytes written, so a caller
// that hashes the file hashes exactly what is on disk.
public interface IConfigFileWriter
{
    Task<byte[]> WriteAsync(SelectionSet set, CatalogScope scope, string outputPath);
}

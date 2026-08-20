using Winhance.Core.Features.Common.Catalog;

namespace Winhance.Core.Features.Common.Selections;

// Path in, bytes out - no picker, no dialogs; the twin of IConfigFileWriter. Returns the written path.
public interface IAutounattendWriter
{
    Task<string> WriteAsync(SelectionSet set, CatalogScope scope, string outputPath);
}

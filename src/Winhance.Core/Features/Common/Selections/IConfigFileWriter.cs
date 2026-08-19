using Winhance.Core.Features.Common.Catalog;

namespace Winhance.Core.Features.Common.Selections;

// Path in, bytes out - no picker, no dialogs; the twin of IAutounattendWriter.
public interface IConfigFileWriter
{
    Task WriteAsync(SelectionSet set, CatalogScope scope, string outputPath);
}

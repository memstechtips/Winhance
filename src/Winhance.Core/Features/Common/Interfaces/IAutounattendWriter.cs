using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Selections;

namespace Winhance.Core.Features.Common.Interfaces;

// Path in, bytes out - no picker, no dialogs; the twin of IConfigFileWriter. Returns the written path.
public interface IAutounattendWriter
{
    Task<string> WriteAsync(SelectionSet set, CatalogScope scope, string outputPath);
}

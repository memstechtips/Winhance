using System.Collections.Generic;

namespace Winhance.Core.Features.Common.Interfaces
{
    public interface IScriptDetectionService
    {
        bool AreRemovalScriptsPresent();
        IEnumerable<ScriptInfo> GetActiveScripts();
    }

    public class ScriptInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
    }
}

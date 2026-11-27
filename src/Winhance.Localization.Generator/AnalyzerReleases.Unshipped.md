; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
WINLOC001 | Localization | Warning | Two localization keys map to one identifier; the shorter key wins
WINLOC002 | Localization | Error | en.json was not supplied to the generator as an AdditionalFiles entry
WINLOC003 | Localization | Error | en.json is not a flat JSON object of string values

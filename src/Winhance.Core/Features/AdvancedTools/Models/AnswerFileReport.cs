namespace Winhance.Core.Features.AdvancedTools.Models;

public enum AnswerFileSeverity
{
    Warning,
    Error
}

// The UI localizes the rule name; Location and Detail stay verbatim: a path, a line, a parser's
// own message.
public enum AnswerFileRule
{
    FileUnreadable,
    NotWellFormed,
    WrongRoot,
    UnknownPass,
    ComponentAttributes,
    CommandListPlacement,
    OrderInvalid,
    OrderDuplicate,
    CommandEmpty,
    CommandTooLong,
    InlineQuote,
    PowerShellParse,
    ParserUnavailable,
    ScriptNotCarried,
    ScriptPathUnknown,
    RegistryRoot,
    ExtractorMissing,
    FilePathNotAbsolute,
    XmlFileNotWellFormed,
    RegFileSyntax,
    FileEmpty,
    AnsiLossy,
    VbScriptDeprecated,
    UnknownFileType
}

public sealed record AnswerFileFinding(AnswerFileRule Rule, AnswerFileSeverity Severity, string Location, string Detail);

public enum AnswerFileVerdict
{
    Clean,
    MayFail,
    WillFail
}

// The verdict is derived, never stored, and nothing gates on it: the user always decides.
public sealed record AnswerFileReport(IReadOnlyList<AnswerFileFinding> Findings)
{
    public AnswerFileVerdict Verdict =>
        Findings.Any(f => f.Severity == AnswerFileSeverity.Error) ? AnswerFileVerdict.WillFail
        : Findings.Count > 0 ? AnswerFileVerdict.MayFail
        : AnswerFileVerdict.Clean;
}

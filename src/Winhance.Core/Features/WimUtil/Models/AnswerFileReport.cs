namespace Winhance.Core.Features.WimUtil.Models;

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
    UnknownFileType,
    ProductKeyEmpty,
    NoAdministratorAccount,
    AutoLogonWithoutLogonCount,
    AutoLogonNotAdministrator,
    AccountNameInvalid,
    DuplicateAccountName
}

public sealed record AnswerFileFinding(AnswerFileRule Rule, AnswerFileSeverity Severity, string Location, string Detail);

public enum AnswerFileVerdict
{
    Clean,
    MayFail,
    WillFail
}

// Imported files are advisory; generated files must pass the strict check before use.
public sealed record AnswerFileReport(IReadOnlyList<AnswerFileFinding> Findings)
{
    public bool IsWinhanceGenerated { get; init; }

    public AnswerFileVerdict Verdict =>
        Findings.Any(f => f.Severity == AnswerFileSeverity.Error) ? AnswerFileVerdict.WillFail
        : Findings.Count > 0 ? AnswerFileVerdict.MayFail
        : AnswerFileVerdict.Clean;

    public void ThrowIfInvalidGenerated()
    {
        var blocking = Findings.Where(f => f.Severity == AnswerFileSeverity.Error || f.Rule == AnswerFileRule.ParserUnavailable).ToList();
        if (blocking.Count > 0)
            throw new InvalidOperationException(string.Join(Environment.NewLine, blocking.Select(f => $"{f.Location}: {f.Detail}")));
    }
}

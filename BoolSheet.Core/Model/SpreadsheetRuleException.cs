namespace BoolSheet.Core.Model;

/// <summary>
/// The user asked for something the spreadsheet rules forbid: deleting a referenced row,
/// exceeding the size limits, deleting the last row... The message is shown to the user (in Ukrainian).
/// </summary>
/// <remarks>
/// A dedicated type, so the UI catches exactly these cases and real bugs
/// (for example an <see cref="InvalidOperationException"/>) are not hidden behind a polite message.
/// </remarks>
public sealed class SpreadsheetRuleException : Exception
{
    public SpreadsheetRuleException(string message)
        : base(message)
    {
    }
}

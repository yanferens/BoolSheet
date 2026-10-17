namespace BoolSheet.Core.Ast;

/// <summary>
/// An error raised while evaluating a syntactically valid expression:
/// division by zero, a reference to a cell with an error, a circular reference, etc.
/// The message is shown to the user, so it is written in Ukrainian.
/// </summary>
public sealed class EvaluationException : Exception
{
    public EvaluationException(string message)
        : base(message)
    {
    }
}

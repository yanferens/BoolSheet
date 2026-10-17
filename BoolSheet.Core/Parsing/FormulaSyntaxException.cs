namespace BoolSheet.Core.Parsing;

/// <summary>Thrown when an expression is syntactically invalid. Details are in <see cref="Result"/>.</summary>
public sealed class FormulaSyntaxException : Exception
{
    public FormulaSyntaxException(ValidationResult result)
        : base(result.ErrorMessage)
    {
        Result = result;
    }

    public ValidationResult Result { get; }
}

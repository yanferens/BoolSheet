namespace BoolSheet.Core.Parsing;

/// <summary>The outcome of a syntax check of an expression.</summary>
public sealed class ValidationResult
{
    private ValidationResult(bool isValid, string errorMessage, int errorPosition)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
        ErrorPosition = errorPosition;
    }

    public static ValidationResult Valid { get; } = new(true, string.Empty, -1);

    public bool IsValid { get; }

    /// <summary>A user-facing error description (in Ukrainian); empty if there is no error.</summary>
    public string ErrorMessage { get; }

    /// <summary>Zero-based index of the offending character; -1 if there is no error.</summary>
    public int ErrorPosition { get; }

    public static ValidationResult Failure(string message, int position) => new(false, message, position);
}

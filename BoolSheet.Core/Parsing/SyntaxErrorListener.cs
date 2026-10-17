using Antlr4.Runtime;

namespace BoolSheet.Core.Parsing;

/// <summary>
/// Captures ANTLR lexer and parser errors instead of printing them to the console,
/// turns them into user-facing Ukrainian messages and keeps the first error with its position.
/// </summary>
internal sealed class SyntaxErrorListener : IAntlrErrorListener<int>, IAntlrErrorListener<IToken>
{
    private readonly string _expression;

    public SyntaxErrorListener(string expression)
    {
        _expression = expression;
    }

    public ValidationResult? FirstError { get; private set; }

    // Lexer error: a character that is not part of the language (e.g. '&' or ';').
    public void SyntaxError(
        TextWriter output,
        IRecognizer recognizer,
        int offendingSymbol,
        int line,
        int charPositionInLine,
        string msg,
        RecognitionException e)
    {
        int position = ToIndex(line, charPositionInLine);
        string symbol = position >= 0 && position < _expression.Length
            ? _expression[position].ToString()
            : "?";

        string message = position >= 0 && position < _expression.Length && char.IsLetter(_expression[position])
            ? $"Невідоме слово на позиції {position + 1}: адреса клітинки — одна літера A–Z і номер рядка (A1, Z100)"
            : $"Недопустимий символ '{symbol}' на позиції {position + 1}";

        Report(message, position);
    }

    // Parser error: the tokens are valid on their own but appear in the wrong order.
    public void SyntaxError(
        TextWriter output,
        IRecognizer recognizer,
        IToken offendingSymbol,
        int line,
        int charPositionInLine,
        string msg,
        RecognitionException e)
    {
        int position = ToIndex(line, charPositionInLine);

        string message = offendingSymbol.Type == TokenConstants.EOF
            ? $"Вираз обривається на позиції {position + 1}: бракує операнда або закривної дужки"
            : $"Неочікуваний елемент '{offendingSymbol.Text}' на позиції {position + 1}";

        Report(message, position);
    }

    private void Report(string message, int position)
    {
        // The first error is the most informative one; later errors are usually its consequences.
        FirstError ??= ValidationResult.Failure(message, position);
    }

    // ANTLR reports a 1-based line and a 0-based column; convert them to an index in the whole expression.
    private int ToIndex(int line, int column)
    {
        int index = 0;
        for (int currentLine = 1; currentLine < line && index < _expression.Length; index++)
        {
            if (_expression[index] == '\n')
            {
                currentLine++;
            }
        }

        return index + column;
    }
}

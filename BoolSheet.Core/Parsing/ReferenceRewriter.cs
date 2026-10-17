using System.Text;
using Antlr4.Runtime;
using BoolSheet.Core.Grammar;
using BoolSheet.Core.Model;

namespace BoolSheet.Core.Parsing;

/// <summary>
/// Finds and rewrites cell references in expression text.
/// Works on lexer tokens rather than the parse tree, so it also handles expressions
/// with syntax errors and leaves the rest of the text (spacing, case of keywords) untouched.
/// </summary>
public static class ReferenceRewriter
{
    public static IReadOnlyList<CellAddress> GetReferences(string expression)
    {
        var references = new List<CellAddress>();

        foreach (IToken token in Tokenize(expression))
        {
            if (token.Type == BoolSheetLexer.CELL && CellAddress.TryParse(token.Text, out CellAddress address))
            {
                references.Add(address);
            }
        }

        return references;
    }

    /// <summary>Replaces every cell reference with <paramref name="map"/>(reference).</summary>
    public static string Rewrite(string expression, Func<CellAddress, CellAddress> map)
    {
        var builder = new StringBuilder(expression.Length);
        int copiedUpTo = 0;

        foreach (IToken token in Tokenize(expression))
        {
            if (token.Type != BoolSheetLexer.CELL || !CellAddress.TryParse(token.Text, out CellAddress address))
            {
                continue;
            }

            CellAddress mapped = map(address);
            if (mapped == address)
            {
                continue;
            }

            builder.Append(expression, copiedUpTo, token.StartIndex - copiedUpTo);
            builder.Append(mapped.ToString());
            copiedUpTo = token.StopIndex + 1;
        }

        builder.Append(expression, copiedUpTo, expression.Length - copiedUpTo);
        return builder.ToString();
    }

    private static IList<IToken> Tokenize(string expression)
    {
        var lexer = new BoolSheetLexer(new AntlrInputStream(expression));
        lexer.RemoveErrorListeners();   // invalid characters are simply skipped here
        return lexer.GetAllTokens();
    }
}

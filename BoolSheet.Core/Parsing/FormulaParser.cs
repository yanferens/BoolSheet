using System.Diagnostics.CodeAnalysis;
using Antlr4.Runtime;
using BoolSheet.Core.Ast;
using BoolSheet.Core.Grammar;

namespace BoolSheet.Core.Parsing;

/// <summary>
/// Handles an expression in two stages:
/// 1) <see cref="ValidateSyntax"/> checks the syntax and locates the first error;
/// 2) <see cref="Parse"/> builds the expression tree, which is then evaluated via <see cref="INode.Evaluate"/>.
/// </summary>
public sealed class FormulaParser
{
    public ValidationResult ValidateSyntax(string expression)
    {
        TryParse(expression, out _, out ValidationResult result);
        return result;
    }

    /// <exception cref="FormulaSyntaxException">The expression is syntactically invalid.</exception>
    public INode Parse(string expression)
    {
        if (!TryParse(expression, out INode? tree, out ValidationResult result))
        {
            throw new FormulaSyntaxException(result);
        }

        return tree;
    }

    /// <summary>
    /// Validates and builds the tree in a single pass.
    /// Returns false and leaves <paramref name="tree"/> null if the expression is invalid.
    /// </summary>
    public bool TryParse(
        string expression,
        [NotNullWhen(true)] out INode? tree,
        out ValidationResult result)
    {
        tree = null;

        if (string.IsNullOrWhiteSpace(expression))
        {
            result = ValidationResult.Failure("Порожній вираз", 0);
            return false;
        }

        // The parser and the tree are recursive: limit the size before parsing so that
        // a pasted or loaded monster expression cannot overflow the stack.
        if (CheckLimits(expression) is { } limitError)
        {
            result = limitError;
            return false;
        }

        var errors = new SyntaxErrorListener(expression);

        var lexer = new BoolSheetLexer(new AntlrInputStream(expression));
        lexer.RemoveErrorListeners();   // drop the default console listener
        lexer.AddErrorListener(errors);

        var parser = new BoolSheetParser(new CommonTokenStream(lexer));
        parser.RemoveErrorListeners();
        parser.AddErrorListener(errors);

        BoolSheetParser.FormulaContext formula = parser.formula();

        if (errors.FirstError is not null)
        {
            result = errors.FirstError;
            return false;
        }

        tree = new AstBuilder().Visit(formula);
        result = ValidationResult.Valid;
        return true;
    }

    private static ValidationResult? CheckLimits(string expression)
    {
        if (expression.Length > Limits.MaxExpressionLength)
        {
            return ValidationResult.Failure(
                $"Вираз задовгий: {expression.Length} символів (максимум {Limits.MaxExpressionLength})",
                Limits.MaxExpressionLength);
        }

        int depth = 0;
        for (int i = 0; i < expression.Length; i++)
        {
            if (expression[i] == '(' && ++depth > Limits.MaxNestingDepth)
            {
                return ValidationResult.Failure(
                    $"Забагато вкладених дужок (максимум {Limits.MaxNestingDepth}) на позиції {i + 1}", i);
            }

            if (expression[i] == ')')
            {
                depth--;
            }
        }

        return null;
    }
}

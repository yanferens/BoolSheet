using System.Globalization;
using System.Numerics;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using BoolSheet.Core.Ast;
using BoolSheet.Core.Grammar;

namespace BoolSheet.Core.Parsing;

/// <summary>
/// Walks the ANTLR parse tree and builds the expression tree (<see cref="INode"/>) from it.
/// Only called for syntactically valid expressions.
/// </summary>
internal sealed class AstBuilder : BoolSheetBaseVisitor<INode>
{
    public override INode VisitFormula(BoolSheetParser.FormulaContext context)
        => Visit(context.expression());

    public override INode VisitNotExpr(BoolSheetParser.NotExprContext context)
        => new UnaryNotNode(Visit(context.expression()));

    public override INode VisitCompareExpr(BoolSheetParser.CompareExprContext context)
        => Visit(context.comparison());

    public override INode VisitComparison(BoolSheetParser.ComparisonContext context)
    {
        INode left = Visit(context.additive(0));

        // No comparison operator: a plain arithmetic expression.
        if (context.ChildCount == 1)
        {
            return left;
        }

        var op = (ITerminalNode)context.GetChild(1);
        INode right = Visit(context.additive(1));

        return new ComparisonOpNode(left, right, ToComparisonOperator(op.Symbol.Type));
    }

    public override INode VisitAdditive(BoolSheetParser.AdditiveContext context)
        => FoldLeft(context);

    public override INode VisitMultiplicative(BoolSheetParser.MultiplicativeContext context)
        => FoldLeft(context);

    public override INode VisitNumberAtom(BoolSheetParser.NumberAtomContext context)
        => new BigIntNumberNode(BigInteger.Parse(context.NUMBER().GetText(), CultureInfo.InvariantCulture));

    public override INode VisitCellAtom(BoolSheetParser.CellAtomContext context)
        => new CellRefNode(context.CELL().GetText());

    public override INode VisitParenAtom(BoolSheetParser.ParenAtomContext context)
        => Visit(context.expression());

    // "not" inside a comparison or arithmetic: A1 = not B1, 1 + not 0.
    public override INode VisitNotAtom(BoolSheetParser.NotAtomContext context)
        => new UnaryNotNode(Visit(context.atom()));

    // Negative numbers: -5, -A1, -(2 + 3).
    public override INode VisitNegateAtom(BoolSheetParser.NegateAtomContext context)
        => new NegateNode(Visit(context.atom()));

    /// <summary>
    /// The rule's children alternate: operand, operator, operand, operator, ...
    /// They are combined left to right, so 10 - 3 - 2 = (10 - 3) - 2.
    /// </summary>
    private INode FoldLeft(ParserRuleContext context)
    {
        INode result = Visit(context.GetChild(0));

        for (int i = 1; i + 1 < context.ChildCount; i += 2)
        {
            var op = (ITerminalNode)context.GetChild(i);
            INode right = Visit(context.GetChild(i + 1));
            result = new BinaryOpNode(result, right, ToBinaryOperator(op.Symbol.Type));
        }

        return result;
    }

    private static BinaryOperator ToBinaryOperator(int tokenType) => tokenType switch
    {
        BoolSheetParser.PLUS => BinaryOperator.Add,
        BoolSheetParser.MINUS => BinaryOperator.Subtract,
        BoolSheetParser.MUL => BinaryOperator.Multiply,
        BoolSheetParser.SLASH => BinaryOperator.Divide,
        BoolSheetParser.DIV => BinaryOperator.Div,
        BoolSheetParser.MOD => BinaryOperator.Mod,
        _ => throw new InvalidOperationException($"Unknown arithmetic token type: {tokenType}"),
    };

    private static ComparisonOperator ToComparisonOperator(int tokenType) => tokenType switch
    {
        BoolSheetParser.EQ => ComparisonOperator.Equal,
        BoolSheetParser.NEQ => ComparisonOperator.NotEqual,
        BoolSheetParser.LT => ComparisonOperator.Less,
        BoolSheetParser.GT => ComparisonOperator.Greater,
        BoolSheetParser.LE => ComparisonOperator.LessOrEqual,
        BoolSheetParser.GE => ComparisonOperator.GreaterOrEqual,
        _ => throw new InvalidOperationException($"Unknown comparison token type: {tokenType}"),
    };
}

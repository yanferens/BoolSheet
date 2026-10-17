using System.Numerics;
using BoolSheet.Core.Ast;
using BoolSheet.Core.Parsing;

namespace BoolSheet.Core.Model;

/// <summary>
/// A non-empty cell: the expression typed by the user, its parsed tree
/// and the result of the last evaluation. Empty cells are not stored at all.
/// </summary>
public sealed class Cell
{
    private INode? _tree;
    private ValidationResult _syntax = ValidationResult.Valid;

    internal Cell(CellAddress address, string expression, FormulaParser parser)
    {
        Address = address;
        SetExpression(expression, parser);
    }

    public CellAddress Address { get; private set; }

    /// <summary>The expression exactly as typed (shown in "expression" display mode).</summary>
    public string Expression { get; private set; } = string.Empty;

    /// <summary>The evaluated value (shown in "value" display mode). 0 if the cell has an error.</summary>
    public BigInteger Value { get; private set; }

    public bool HasError { get; private set; }

    /// <summary>A user-facing error description (in Ukrainian); empty if there is no error.</summary>
    public string ErrorMessage { get; private set; } = string.Empty;

    /// <summary>Zero-based position of a syntax error in <see cref="Expression"/>; -1 otherwise.</summary>
    public int ErrorPosition { get; private set; } = -1;

    /// <summary>
    /// True if the top-level operation is a comparison or <c>not</c>, so the value is 1 or 0
    /// and should be displayed as a logical value rather than as a number.
    /// </summary>
    public bool IsLogicalResult => _tree is ComparisonOpNode or UnaryNotNode;

    /// <summary>The cells this expression refers to (found by the lexer, so also for invalid expressions).</summary>
    public IReadOnlyList<CellAddress> References { get; private set; } = Array.Empty<CellAddress>();

    internal void SetExpression(string expression, FormulaParser parser)
    {
        Expression = expression;
        parser.TryParse(expression, out _tree, out _syntax);
        References = _tree is null ? Array.Empty<CellAddress>() : ReferenceRewriter.GetReferences(expression);
        ResetValue();
    }

    internal void MoveTo(CellAddress address) => Address = address;

    internal void Evaluate(ICellValueProvider cells)
    {
        ResetValue();

        // Stage 1 already happened in SetExpression; a syntax error makes the cell an error cell.
        if (_tree is null)
        {
            SetError(_syntax.ErrorMessage, _syntax.ErrorPosition);
            return;
        }

        // Stage 2: evaluation.
        try
        {
            Value = _tree.Evaluate(cells);
        }
        catch (EvaluationException ex)
        {
            SetError(ex.Message, -1);
        }
    }

    private void ResetValue()
    {
        Value = BigInteger.Zero;
        HasError = false;
        ErrorMessage = string.Empty;
        ErrorPosition = -1;
    }

    private void SetError(string message, int position)
    {
        Value = BigInteger.Zero;
        HasError = true;
        ErrorMessage = message;
        ErrorPosition = position;
    }
}

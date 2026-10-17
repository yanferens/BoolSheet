using System.Numerics;

namespace BoolSheet.Core.Ast;

public enum ComparisonOperator
{
    Equal,           // =
    NotEqual,        // <>
    Less,            // <
    Greater,         // >
    LessOrEqual,     // <=
    GreaterOrEqual,  // >=
}

/// <summary>Compares two numbers. The result is 1 (true) or 0 (false).</summary>
public sealed class ComparisonOpNode : INode
{
    private readonly INode _left;
    private readonly INode _right;
    private readonly ComparisonOperator _operator;

    public ComparisonOpNode(INode left, INode right, ComparisonOperator op)
    {
        _left = left;
        _right = right;
        _operator = op;
    }

    public BigInteger Evaluate(ICellValueProvider cells)
    {
        int comparison = _left.Evaluate(cells).CompareTo(_right.Evaluate(cells));

        bool result = _operator switch
        {
            ComparisonOperator.Equal => comparison == 0,
            ComparisonOperator.NotEqual => comparison != 0,
            ComparisonOperator.Less => comparison < 0,
            ComparisonOperator.Greater => comparison > 0,
            ComparisonOperator.LessOrEqual => comparison <= 0,
            ComparisonOperator.GreaterOrEqual => comparison >= 0,
            _ => throw new InvalidOperationException($"Unknown comparison operator: {_operator}"),
        };

        return Logic.From(result);
    }
}

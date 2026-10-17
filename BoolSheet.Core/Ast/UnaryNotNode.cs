using System.Numerics;

namespace BoolSheet.Core.Ast;

/// <summary>Logical negation: not 0 = 1, not (any non-zero) = 0.</summary>
public sealed class UnaryNotNode : INode
{
    private readonly INode _operand;

    public UnaryNotNode(INode operand)
    {
        _operand = operand;
    }

    public BigInteger Evaluate(ICellValueProvider cells)
        => Logic.From(!Logic.IsTrue(_operand.Evaluate(cells)));
}

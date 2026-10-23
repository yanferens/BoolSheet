using System.Numerics;

namespace BoolSheet.Core.Ast;

/// <summary>Unary minus: -5, -A1, -(2 + 3). A negative number is "true" like any other non-zero number.</summary>
public sealed class NegateNode : INode
{
    private readonly INode _operand;

    public NegateNode(INode operand)
    {
        _operand = operand;
    }

    public BigInteger Evaluate(ICellValueProvider cells) => -_operand.Evaluate(cells);
}

using System.Numerics;

namespace BoolSheet.Core.Ast;

/// <summary>An arbitrary-length integer literal.</summary>
public sealed class BigIntNumberNode : INode
{
    private readonly BigInteger _value;

    public BigIntNumberNode(BigInteger value)
    {
        _value = value;
    }

    public BigInteger Evaluate(ICellValueProvider cells) => _value;
}

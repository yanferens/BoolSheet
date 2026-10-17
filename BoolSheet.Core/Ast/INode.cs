using System.Numerics;

namespace BoolSheet.Core.Ast;

/// <summary>
/// A node of the expression tree.
/// Logical values are represented as numbers: 0 is false, any non-zero number is true.
/// Comparisons and <c>not</c> always return exactly 1 or 0.
/// </summary>
public interface INode
{
    BigInteger Evaluate(ICellValueProvider cells);
}

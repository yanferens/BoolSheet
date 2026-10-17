using System.Numerics;

namespace BoolSheet.Core.Ast;

public enum BinaryOperator
{
    Add,        // +
    Subtract,   // -
    Multiply,   // *
    Divide,     // /   truncating division (like C#):        -7 / 2 = -3
    Div,        // div floor division (rounds down):          -7 div 2 = -4
    Mod,        // mod remainder consistent with div, so that
                //     a = b * (a div b) + a mod b:           -7 mod 2 = 1
}

/// <summary>An arithmetic operation on two operands.</summary>
public sealed class BinaryOpNode : INode
{
    private readonly INode _left;
    private readonly INode _right;
    private readonly BinaryOperator _operator;

    public BinaryOpNode(INode left, INode right, BinaryOperator op)
    {
        _left = left;
        _right = right;
        _operator = op;
    }

    public BigInteger Evaluate(ICellValueProvider cells)
    {
        BigInteger left = _left.Evaluate(cells);
        BigInteger right = _right.Evaluate(cells);

        BigInteger result = _operator switch
        {
            BinaryOperator.Add => left + right,
            BinaryOperator.Subtract => left - right,
            BinaryOperator.Multiply => left * right,
            BinaryOperator.Divide => BigInteger.Divide(left, NotZero(right)),
            BinaryOperator.Div => FloorDivide(left, NotZero(right)),
            BinaryOperator.Mod => FloorModulo(left, NotZero(right)),
            _ => throw new InvalidOperationException($"Unknown binary operator: {_operator}"),
        };

        // Repeated squaring doubles the number of digits every time: without a limit a few cells
        // produce numbers with millions of digits that freeze the program and exhaust the memory.
        if (result.GetBitLength() > Limits.MaxNumberBits)
        {
            throw new EvaluationException("Число завелике (понад 30 000 цифр)");
        }

        return result;
    }

    private static BigInteger NotZero(BigInteger divisor)
    {
        if (divisor.IsZero)
        {
            // User-facing message, kept in Ukrainian.
            throw new EvaluationException("Ділення на нуль");
        }

        return divisor;
    }

    private static BigInteger FloorDivide(BigInteger dividend, BigInteger divisor)
    {
        BigInteger quotient = BigInteger.DivRem(dividend, divisor, out BigInteger remainder);

        // BigInteger.DivRem truncates toward zero. If the remainder is non-zero
        // and its sign differs from the divisor's, the floor result is one less.
        if (!remainder.IsZero && remainder.Sign != divisor.Sign)
        {
            quotient -= 1;
        }

        return quotient;
    }

    private static BigInteger FloorModulo(BigInteger dividend, BigInteger divisor)
    {
        BigInteger remainder = BigInteger.Remainder(dividend, divisor);

        // Shift the truncated remainder so that it takes the divisor's sign.
        if (!remainder.IsZero && remainder.Sign != divisor.Sign)
        {
            remainder += divisor;
        }

        return remainder;
    }
}

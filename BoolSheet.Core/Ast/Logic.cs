using System.Numerics;

namespace BoolSheet.Core.Ast;

/// <summary>
/// Conversion rules between numbers and logical values.
/// False is 0, true is any non-zero number.
/// </summary>
public static class Logic
{
    public static readonly BigInteger True = BigInteger.One;
    public static readonly BigInteger False = BigInteger.Zero;

    public static BigInteger From(bool value) => value ? True : False;

    public static bool IsTrue(BigInteger value) => !value.IsZero;
}

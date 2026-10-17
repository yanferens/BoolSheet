namespace BoolSheet.Core;

/// <summary>
/// Safety limits. They keep hostile or accidental input (a huge number, a deeply nested expression,
/// a very long chain of references) from freezing the program or overflowing the stack.
/// </summary>
public static class Limits
{
    /// <summary>Longest accepted expression, in characters.</summary>
    public const int MaxExpressionLength = 1000;

    /// <summary>Deepest accepted nesting of parentheses.</summary>
    public const int MaxNestingDepth = 64;

    /// <summary>Largest accepted result of an operation, in bits (about 30 000 decimal digits).</summary>
    public const int MaxNumberBits = 100_000;

    /// <summary>
    /// Deepest chain of cell references evaluated recursively. Cells are evaluated in dependency
    /// order, so ordinary chains never get deep; only circular references can reach this limit.
    /// </summary>
    public const int MaxReferenceDepth = 256;
}

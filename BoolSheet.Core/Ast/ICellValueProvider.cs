using System.Numerics;

namespace BoolSheet.Core.Ast;

/// <summary>
/// Supplies cell values when evaluating cell references (A1, B2, ...).
/// Implemented by Grid. The expression tree does not depend on Grid directly,
/// so parsing and evaluation can be tested independently of the spreadsheet.
/// </summary>
public interface ICellValueProvider
{
    /// <summary>
    /// Returns the value of the cell with the given upper-case address (e.g. "A1").
    /// Throws <see cref="EvaluationException"/> if the cell does not exist,
    /// contains an error, or is part of a circular reference.
    /// </summary>
    BigInteger GetCellValue(string address);
}

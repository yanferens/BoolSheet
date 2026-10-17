using System.Numerics;

namespace BoolSheet.Core.Ast;

/// <summary>A reference to a cell, e.g. A1 or AB12.</summary>
public sealed class CellRefNode : INode
{
    private readonly string _cellAddress;

    public CellRefNode(string cellAddress)
    {
        // The grammar is case-insensitive, so "a1" and "A1" refer to the same cell.
        _cellAddress = cellAddress.ToUpperInvariant();
    }

    public string CellAddress => _cellAddress;

    public BigInteger Evaluate(ICellValueProvider cells) => cells.GetCellValue(_cellAddress);
}

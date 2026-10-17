using BoolSheet.Core.Ast;
using BoolSheet.Core.Model;
using Xunit;

namespace BoolSheet.Tests;

public class GridTests
{
    private readonly Grid _grid = new(rowsCount: 3, columnsCount: 3);

    [Fact]
    public void CellReference_UsesReferencedCellValue()
    {
        _grid.SetCellExpression("A1", "2 + 3");
        _grid.SetCellExpression("B1", "A1 * 2 = 10");

        AssertLogical("B1", true);
    }

    [Fact]
    public void EmptyCell_EvaluatesToZero()
    {
        _grid.SetCellExpression("A1", "C3 = 0");

        AssertLogical("A1", true);
    }

    [Fact]
    public void ChangingCell_RecalculatesDependentCells()
    {
        _grid.SetCellExpression("A1", "1");
        _grid.SetCellExpression("B1", "A1 = 1");
        AssertLogical("B1", true);

        _grid.SetCellExpression("A1", "2");

        AssertLogical("B1", false);
    }

    [Fact]
    public void SelfReference_IsError()
    {
        _grid.SetCellExpression("A1", "A1 + 1");

        Assert.True(GetCell("A1").HasError);
    }

    [Fact]
    public void CircularReference_MarksEveryCellInCycle()
    {
        _grid.SetCellExpression("A1", "B1");
        _grid.SetCellExpression("B1", "A1");

        Assert.True(GetCell("A1").HasError);
        Assert.True(GetCell("B1").HasError);
    }

    [Fact]
    public void ErrorInReferencedCell_Propagates()
    {
        _grid.SetCellExpression("A1", "1 div 0");
        _grid.SetCellExpression("B1", "A1 = 0");

        Assert.True(GetCell("B1").HasError);
    }

    [Fact]
    public void SyntaxError_KeepsExpressionAndReportsPosition()
    {
        _grid.SetCellExpression("A1", "1 +");

        Assert.True(GetCell("A1").HasError);
        Assert.Equal("1 +", GetCell("A1").Expression);
        Assert.Equal(3, GetCell("A1").ErrorPosition);
    }

    [Fact]
    public void ReferenceOutsideGrid_IsErrorUntilColumnAdded()
    {
        _grid.SetCellExpression("A1", "D1 = 0");
        Assert.True(GetCell("A1").HasError);

        _grid.AddColumn();

        AssertLogical("A1", true);
    }

    [Fact]
    public void ClearCell_RemovesCell()
    {
        _grid.SetCellExpression("A1", "1");

        _grid.ClearCell("A1");

        Assert.Null(_grid.GetCell("A1"));
    }

    [Fact]
    public void DeleteRow_ShiftsCellsAndRewritesReferences()
    {
        _grid.SetCellExpression("A3", "7");
        _grid.SetCellExpression("B1", "a3 = 7");

        _grid.DeleteRow(1);

        Assert.Equal(2, _grid.RowsCount);
        Assert.Equal("7", GetCell("A2").Expression);
        Assert.Equal("A2 = 7", GetCell("B1").Expression);
        AssertLogical("B1", true);
    }

    [Fact]
    public void DeleteRow_ReferencedByOtherCell_Throws()
    {
        _grid.SetCellExpression("A2", "1");
        _grid.SetCellExpression("B1", "A2 = 1");

        Assert.Equal(new[] { CellAddress.Parse("B1") }, _grid.GetCellsReferencingRow(1));
        Assert.Throws<SpreadsheetRuleException>(() => _grid.DeleteRow(1));
        Assert.Equal(3, _grid.RowsCount);
    }

    [Fact]
    public void DeleteColumn_ShiftsCellsAndRewritesReferences()
    {
        _grid.SetCellExpression("C1", "4");
        _grid.SetCellExpression("A2", "C1 = 4");

        _grid.DeleteColumn(1);

        Assert.Equal(2, _grid.ColumnsCount);
        Assert.Equal("4", GetCell("B1").Expression);
        Assert.Equal("B1 = 4", GetCell("A2").Expression);
        AssertLogical("A2", true);
    }

    [Fact]
    public void DeleteOnlyRow_Throws()
    {
        var grid = new Grid(rowsCount: 1, columnsCount: 3);

        Assert.Throws<SpreadsheetRuleException>(() => grid.DeleteRow(0));
    }

    private Cell GetCell(string address)
    {
        Cell? cell = _grid.GetCell(address);
        Assert.NotNull(cell);
        return cell;
    }

    private void AssertLogical(string address, bool expected)
    {
        Cell cell = GetCell(address);
        Assert.False(cell.HasError, cell.ErrorMessage);
        Assert.Equal(expected, Logic.IsTrue(cell.Value));
    }
}

using System.Numerics;
using BoolSheet.Core;
using BoolSheet.Core.Ast;
using BoolSheet.Core.Model;
using BoolSheet.Core.Storage;
using Xunit;

namespace BoolSheet.Tests;

/// <summary>Inputs that used to freeze or crash the program (risks R1, R2, R8, R9, R11, R13, R23 of the guide).</summary>
public class RobustnessTests
{
    // ---------- R1: huge numbers ----------

    [Fact]
    public void RepeatedSquaring_StopsWithTooBigNumberError()
    {
        var grid = new Grid(rowsCount: 40, columnsCount: 1);
        var cells = new Dictionary<string, string> { ["A1"] = "10" };
        for (int row = 2; row <= 40; row++)
        {
            cells[$"A{row}"] = $"A{row - 1} * A{row - 1}";
        }

        grid.SetCellExpressions(cells);

        Assert.False(grid.GetCell("A10")!.HasError);   // 10^512 is still fine
        Assert.True(grid.GetCell("A40")!.HasError);
    }

    // ---------- R2: deep recursion ----------

    [Fact]
    public void ReferenceChainThroughWholeSheet_IsEvaluatedWithoutStackOverflow()
    {
        // Every cell refers to the next one, so evaluating in storage order would recurse 2 600 levels deep.
        List<string> addresses = AllAddresses(Grid.MaxRows, Grid.MaxColumns);
        var grid = new Grid(Grid.MaxRows, Grid.MaxColumns);
        var cells = new Dictionary<string, string>();
        for (int i = 0; i < addresses.Count - 1; i++)
        {
            cells[addresses[i]] = $"{addresses[i + 1]} + 1";
        }

        cells[addresses[^1]] = "1";

        grid.SetCellExpressions(cells);

        Cell first = grid.GetCell(addresses[0])!;
        Assert.False(first.HasError, first.ErrorMessage);
        Assert.Equal(new BigInteger(addresses.Count), first.Value);
    }

    [Fact]
    public void CycleThroughWholeSheet_IsErrorWithoutStackOverflow()
    {
        List<string> addresses = AllAddresses(Grid.MaxRows, Grid.MaxColumns);
        var grid = new Grid(Grid.MaxRows, Grid.MaxColumns);
        var cells = new Dictionary<string, string>();
        for (int i = 0; i < addresses.Count; i++)
        {
            cells[addresses[i]] = addresses[(i + 1) % addresses.Count];
        }

        grid.SetCellExpressions(cells);

        Assert.All(grid.Cells, cell => Assert.True(cell.HasError));
    }

    [Fact]
    public void TooLongExpression_IsSyntaxError()
    {
        string expression = "1" + string.Concat(Enumerable.Repeat(" + 1", 300));

        Cell cell = Evaluate(expression);

        Assert.True(cell.HasError);
        Assert.Contains("задовгий", cell.ErrorMessage);
    }

    [Fact]
    public void TooDeeplyNestedParentheses_IsSyntaxError()
    {
        string expression = new string('(', 100) + "1" + new string(')', 100);

        Cell cell = Evaluate(expression);

        Assert.True(cell.HasError);
        Assert.Contains("дужок", cell.ErrorMessage);
    }

    // ---------- R8: keywords without a space ----------

    [Theory]
    [InlineData("10 mod3", 1)]
    [InlineData("7 div2", 3)]
    [InlineData("notA1", 1)]     // A1 is empty, i.e. 0
    public void KeywordWithoutSpace_IsNotReadAsCellAddress(string expression, int expected)
    {
        Cell cell = Evaluate(expression);

        Assert.False(cell.HasError, cell.ErrorMessage);
        Assert.Equal(new BigInteger(expected), cell.Value);
    }

    // ---------- R9: "not" inside comparisons and arithmetic ----------

    [Theory]
    [InlineData("1 + not 0", 2)]
    [InlineData("0 = not 5", 1)]
    [InlineData("not 1 > 2", 1)]          // a leading "not" still applies to the whole comparison
    [InlineData("not (1 < 2) = 0", 1)]   // = not ((1 < 2) = 0) = not (1 = 0)
    public void NotInsideExpression_Works(string expression, int expected)
    {
        Cell cell = Evaluate(expression);

        Assert.False(cell.HasError, cell.ErrorMessage);
        Assert.Equal(new BigInteger(expected), cell.Value);
    }

    // ---------- R13: every operator of the tree is implemented ----------

    [Fact]
    public void EveryBinaryOperator_IsEvaluated()
    {
        foreach (BinaryOperator op in Enum.GetValues<BinaryOperator>())
        {
            var node = new BinaryOpNode(new BigIntNumberNode(7), new BigIntNumberNode(2), op);
            node.Evaluate(new EmptyCells());
        }
    }

    [Fact]
    public void EveryComparisonOperator_IsEvaluated()
    {
        foreach (ComparisonOperator op in Enum.GetValues<ComparisonOperator>())
        {
            var node = new ComparisonOpNode(new BigIntNumberNode(7), new BigIntNumberNode(2), op);
            node.Evaluate(new EmptyCells());
        }
    }

    // ---------- R23: duplicate addresses in a file ----------

    [Fact]
    public void Deserialize_SameCellTwiceInDifferentCase_ThrowsInvalidDataException()
    {
        const string json = """{ "version": 1, "rows": 2, "columns": 2, "cells": { "a1": "1", "A1": "2" } }""";

        Assert.Throws<InvalidDataException>(() => SpreadsheetSerializer.Deserialize(json));
    }

    // ---------- R11: saving leaves no temporary files ----------

    [Fact]
    public async Task LocalSave_ReplacesFileAndLeavesNoTemporaryFiles()
    {
        string folder = Path.Combine(Path.GetTempPath(), $"boolsheet-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "table" + SpreadsheetSerializer.FileExtension);

        try
        {
            var manager = new LocalFileManager();
            var grid = new Grid(rowsCount: 2, columnsCount: 2);

            grid.SetCellExpression("A1", "1");
            await manager.SaveAsync(grid, path);
            grid.SetCellExpression("A1", "2");
            await manager.SaveAsync(grid, path);

            Assert.Equal(new[] { path }, Directory.GetFiles(folder));
            Assert.Equal("2", (await manager.LoadAsync(path)).GetCell("A1")?.Expression);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    // ---------- Helpers ----------

    private static Cell Evaluate(string expression)
    {
        var grid = new Grid(rowsCount: 2, columnsCount: 2);
        grid.SetCellExpression("B2", expression);
        return grid.GetCell("B2")!;
    }

    private static List<string> AllAddresses(int rows, int columns)
    {
        var addresses = new List<string>(rows * columns);
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                addresses.Add(new CellAddress(column, row).ToString());
            }
        }

        return addresses;
    }

    private sealed class EmptyCells : ICellValueProvider
    {
        public BigInteger GetCellValue(string address) => BigInteger.Zero;
    }
}

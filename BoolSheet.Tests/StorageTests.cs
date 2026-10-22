using BoolSheet.Core.Ast;
using BoolSheet.Core.Model;
using BoolSheet.Core.Services;
using BoolSheet.Core.Storage;
using Xunit;

namespace BoolSheet.Tests;

public class StorageTests
{
    [Fact]
    public void SerializeThenDeserialize_RestoresSizeExpressionsAndValues()
    {
        var original = new Grid(rowsCount: 4, columnsCount: 3);
        original.SetCellExpression("A1", "2 + 3");
        original.SetCellExpression("C4", "A1 > 4");

        Grid restored = SpreadsheetSerializer.Deserialize(SpreadsheetSerializer.Serialize(original));

        Assert.Equal(4, restored.RowsCount);
        Assert.Equal(3, restored.ColumnsCount);
        Assert.Equal("2 + 3", restored.GetCell("A1")?.Expression);
        Assert.True(Logic.IsTrue(restored.GetCell("C4")!.Value));
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("{}")]
    [InlineData("""{ "version": 1, "rows": 0, "columns": 3, "cells": {} }""")]
    [InlineData("""{ "version": 1, "rows": 5, "columns": 500, "cells": {} }""")]
    [InlineData("""{ "version": 1, "rows": 2, "columns": 2, "cells": { "Z9": "1" } }""")]
    [InlineData("""{ "version": 1, "rows": 2, "columns": 2, "cells": { "hello": "1" } }""")]
    public void Deserialize_InvalidContent_ThrowsInvalidDataException(string json)
    {
        Assert.Throws<InvalidDataException>(() => SpreadsheetSerializer.Deserialize(json));
    }

    [Fact]
    public async Task LocalFileManager_SavesAndLoadsFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"boolsheet-test-{Guid.NewGuid()}{SpreadsheetSerializer.FileExtension}");
        var service = new SpreadsheetService(new LocalFileManager());
        service.EditCell("B2", "10 mod 3 = 1");

        try
        {
            await service.SaveSpreadsheetAsync(path);
            service.CreateSpreadsheet(rows: 1, columns: 1);

            await service.OpenSpreadsheetAsync(path);

            Assert.Equal(SpreadsheetService.DefaultRows, service.Grid.RowsCount);
            Assert.Equal("10 mod 3 = 1", service.Grid.GetCell("B2")?.Expression);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AddRow_AtMaximum_Throws()
    {
        var grid = new Grid(Grid.MaxRows, columnsCount: 1);

        Assert.Throws<SpreadsheetRuleException>(() => grid.AddRow());
    }

    [Theory]
    [InlineData("1 < 2", true)]
    [InlineData("not 0", true)]
    [InlineData("(1 < 2)", true)]
    [InlineData("2 + 3", false)]
    [InlineData("(1 < 2) + 1", false)]
    public void IsLogicalResult_DependsOnTopLevelOperation(string expression, bool expected)
    {
        var grid = new Grid(rowsCount: 1, columnsCount: 1);
        grid.SetCellExpression("A1", expression);

        Assert.Equal(expected, grid.GetCell("A1")!.IsLogicalResult);
    }
}

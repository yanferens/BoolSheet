using System.Text.Json;
using BoolSheet.Core.Model;

namespace BoolSheet.Core.Storage;

/// <summary>
/// Converts a spreadsheet to and from JSON. Only the size and the expressions are stored;
/// values are recalculated after loading.
/// </summary>
/// <example>
/// <code>
/// {
///   "version": 1,
///   "rows": 10,
///   "columns": 5,
///   "cells": { "A1": "2 + 3", "B1": "A1 > 4" }
/// }
/// </code>
/// </example>
public static class SpreadsheetSerializer
{
    public const string FileExtension = ".boolsheet";

    private const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public static string Serialize(Grid grid)
    {
        var cells = grid.Cells
            .OrderBy(cell => cell.Address.Row)
            .ThenBy(cell => cell.Address.Column)
            .ToDictionary(cell => cell.Address.ToString(), cell => cell.Expression);

        var document = new SpreadsheetDocument(CurrentVersion, grid.RowsCount, grid.ColumnsCount, cells);
        return JsonSerializer.Serialize(document, Options);
    }

    /// <exception cref="InvalidDataException">The JSON is malformed or describes an invalid spreadsheet.</exception>
    public static Grid Deserialize(string json)
    {
        SpreadsheetDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<SpreadsheetDocument>(json, Options);
        }
        catch (JsonException)
        {
            throw new InvalidDataException("Файл пошкоджений або не є таблицею BoolSheet");
        }

        if (document is null || document.Cells is null)
        {
            throw new InvalidDataException("Файл не є таблицею BoolSheet");
        }

        if (document.Version > CurrentVersion)
        {
            throw new InvalidDataException("Файл створено новішою версією BoolSheet");
        }

        if (document.Rows is < 1 or > Grid.MaxRows || document.Columns is < 1 or > Grid.MaxColumns)
        {
            throw new InvalidDataException(
                $"Неприпустимий розмір таблиці: {document.Rows} × {document.Columns} " +
                $"(максимум {Grid.MaxRows} × {Grid.MaxColumns})");
        }

        var grid = new Grid(document.Rows, document.Columns);

        var seen = new HashSet<CellAddress>();
        foreach (string address in document.Cells.Keys)
        {
            if (!CellAddress.TryParse(address, out CellAddress parsed) || !grid.Contains(parsed))
            {
                throw new InvalidDataException($"Неприпустима адреса клітинки у файлі: '{address}'");
            }

            // "a1" and "A1" are the same cell: one would silently overwrite the other.
            if (!seen.Add(parsed))
            {
                throw new InvalidDataException($"Клітинка {parsed} записана у файлі двічі");
            }
        }

        grid.SetCellExpressions(document.Cells);
        return grid;
    }
}

/// <summary>The on-disk shape of a spreadsheet file.</summary>
internal sealed record SpreadsheetDocument(
    int Version,
    int Rows,
    int Columns,
    Dictionary<string, string>? Cells);

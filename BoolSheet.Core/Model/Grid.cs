using System.Numerics;
using BoolSheet.Core.Ast;
using BoolSheet.Core.Parsing;

namespace BoolSheet.Core.Model;

/// <summary>
/// The spreadsheet: its size, the non-empty cells and their evaluation.
/// Implements <see cref="ICellValueProvider"/> so that cell references in expressions
/// are resolved against this grid.
/// </summary>
/// <remarks>
/// Rules for references:
/// an empty cell evaluates to 0 (false);
/// a reference outside the grid, to a cell with an error, or a circular reference is an error.
/// </remarks>
public sealed class Grid : ICellValueProvider
{
    /// <summary>Upper limits that keep the on-screen grid responsive.</summary>
    public const int MaxRows = 100;
    public const int MaxColumns = 26;

    private readonly FormulaParser _parser = new();
    private readonly HashSet<CellAddress> _inProgress = new();
    private readonly HashSet<CellAddress> _evaluated = new();
    private Dictionary<CellAddress, Cell> _cells = new();

    public Grid(int rowsCount, int columnsCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(rowsCount, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(rowsCount, MaxRows);
        ArgumentOutOfRangeException.ThrowIfLessThan(columnsCount, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(columnsCount, MaxColumns);

        RowsCount = rowsCount;
        ColumnsCount = columnsCount;
    }

    public int RowsCount { get; private set; }

    public int ColumnsCount { get; private set; }

    /// <summary>Non-empty cells only, in no particular order.</summary>
    public IEnumerable<Cell> Cells => _cells.Values;

    public bool Contains(CellAddress address)
        => address.Row < RowsCount && address.Column < ColumnsCount;

    /// <summary>Returns the cell, or null if it is empty.</summary>
    public Cell? GetCell(CellAddress address) => _cells.GetValueOrDefault(address);

    /// <inheritdoc cref="GetCell(CellAddress)"/>
    public Cell? GetCell(string address) => GetCell(CellAddress.Parse(address));

    /// <summary>Sets a cell's expression and recalculates the grid. An empty expression clears the cell.</summary>
    public void SetCellExpression(string address, string expression)
    {
        SetExpressionCore(ParseInside(address), expression);
        RecalculateAll();
    }

    /// <summary>Sets many cells at once (e.g. when loading a file) and recalculates only once.</summary>
    public void SetCellExpressions(IEnumerable<KeyValuePair<string, string>> expressions)
    {
        foreach ((string address, string expression) in expressions)
        {
            SetExpressionCore(ParseInside(address), expression);
        }

        RecalculateAll();
    }

    public void ClearCell(string address) => SetCellExpression(address, string.Empty);

    public void AddRow()
    {
        if (RowsCount == MaxRows)
        {
            throw new SpreadsheetRuleException($"Досягнуто максимальної кількості рядків ({MaxRows})");
        }

        RowsCount++;
        RecalculateAll();   // references to the new row may have been out of bounds before
    }

    public void AddColumn()
    {
        if (ColumnsCount == MaxColumns)
        {
            throw new SpreadsheetRuleException($"Досягнуто максимальної кількості стовпчиків ({MaxColumns})");
        }

        ColumnsCount++;
        RecalculateAll();
    }

    /// <summary>Cells outside the given (zero-based) row that reference a cell in it.</summary>
    public IReadOnlyList<CellAddress> GetCellsReferencingRow(int row)
        => FindCellsReferencing(address => address.Row == row);

    /// <summary>Cells outside the given (zero-based) column that reference a cell in it.</summary>
    public IReadOnlyList<CellAddress> GetCellsReferencingColumn(int column)
        => FindCellsReferencing(address => address.Column == column);

    /// <summary>
    /// Deletes a zero-based row: shifts the rows below it up and rewrites references to them.
    /// Throws <see cref="SpreadsheetRuleException"/> (with a user-facing message) if other cells
    /// reference the row or if it is the last remaining row.
    /// </summary>
    public void DeleteRow(int row)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(row, RowsCount);

        if (RowsCount == 1)
        {
            throw new SpreadsheetRuleException("Таблиця повинна мати хоча б один рядок");
        }

        EnsureNotReferenced(GetCellsReferencingRow(row), $"рядок {row + 1}");

        RemoveLine(
            isRemoved: address => address.Row == row,
            shift: address => address.Row > row ? address with { Row = address.Row - 1 } : address);

        RowsCount--;
        RecalculateAll();
    }

    /// <summary>Deletes a zero-based column; see <see cref="DeleteRow"/> for the rules.</summary>
    public void DeleteColumn(int column)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(column);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(column, ColumnsCount);

        if (ColumnsCount == 1)
        {
            throw new SpreadsheetRuleException("Таблиця повинна мати хоча б один стовпчик");
        }

        EnsureNotReferenced(GetCellsReferencingColumn(column), $"стовпчик {CellAddress.ColumnName(column)}");

        RemoveLine(
            isRemoved: address => address.Column == column,
            shift: address => address.Column > column ? address with { Column = address.Column - 1 } : address);

        ColumnsCount--;
        RecalculateAll();
    }

    /// <summary>Re-evaluates every cell. Each cell is evaluated once, after the cells it references.</summary>
    public void RecalculateAll()
    {
        _inProgress.Clear();
        _evaluated.Clear();

        // Evaluating in dependency order means that when a cell is evaluated, the cells it references
        // are already done, so the recursion through GetCellValue stays one level deep even for
        // a chain of thousands of cells (a deep recursion would overflow the stack, which kills the process).
        foreach (Cell cell in EvaluationOrder())
        {
            EnsureEvaluated(cell);
        }
    }

    /// <summary>Called by <see cref="CellRefNode"/> while an expression is being evaluated.</summary>
    public BigInteger GetCellValue(string address)
    {
        if (!CellAddress.TryParse(address, out CellAddress parsed) || !Contains(parsed))
        {
            throw new EvaluationException($"Клітинка {address} поза межами таблиці");
        }

        if (!_cells.TryGetValue(parsed, out Cell? cell))
        {
            return BigInteger.Zero;
        }

        // The cell is still being evaluated higher up the call stack, so we came back to it.
        if (_inProgress.Contains(parsed))
        {
            throw new EvaluationException($"Циклічне посилання на клітинку {parsed}");
        }

        // Only a long circular chain can get this deep (see EvaluationOrder); stop before the stack runs out.
        if (!_evaluated.Contains(parsed) && _inProgress.Count >= Limits.MaxReferenceDepth)
        {
            throw new EvaluationException("Занадто довгий або циклічний ланцюжок посилань");
        }

        EnsureEvaluated(cell);

        if (cell.HasError)
        {
            throw new EvaluationException($"Клітинка {parsed} містить помилку");
        }

        return cell.Value;
    }

    private void EnsureEvaluated(Cell cell)
    {
        if (_evaluated.Contains(cell.Address))
        {
            return;
        }

        _inProgress.Add(cell.Address);
        try
        {
            cell.Evaluate(this);
        }
        finally
        {
            _inProgress.Remove(cell.Address);
            _evaluated.Add(cell.Address);
        }
    }

    /// <summary>
    /// Orders the cells so that every cell comes after the cells it references
    /// (a post-order depth-first traversal, written with an explicit stack instead of recursion).
    /// Cells in a cycle get some order; the cycle itself is reported during evaluation.
    /// </summary>
    private List<Cell> EvaluationOrder()
    {
        var order = new List<Cell>(_cells.Count);
        var visited = new HashSet<CellAddress>();
        var stack = new Stack<(Cell Cell, IEnumerator<CellAddress> References)>();

        foreach (Cell root in _cells.Values)
        {
            if (!visited.Add(root.Address))
            {
                continue;
            }

            stack.Push((root, root.References.GetEnumerator()));
            while (stack.Count > 0)
            {
                (Cell cell, IEnumerator<CellAddress> references) = stack.Peek();

                if (references.MoveNext())
                {
                    if (_cells.TryGetValue(references.Current, out Cell? referenced) && visited.Add(referenced.Address))
                    {
                        stack.Push((referenced, referenced.References.GetEnumerator()));
                    }
                }
                else
                {
                    stack.Pop();
                    order.Add(cell);
                }
            }
        }

        return order;
    }

    private CellAddress ParseInside(string address)
    {
        CellAddress parsed = CellAddress.Parse(address);
        if (!Contains(parsed))
        {
            throw new ArgumentOutOfRangeException(nameof(address), $"Cell {parsed} is outside the grid.");
        }

        return parsed;
    }

    private void SetExpressionCore(CellAddress address, string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            _cells.Remove(address);
        }
        else if (_cells.TryGetValue(address, out Cell? cell))
        {
            cell.SetExpression(expression, _parser);
        }
        else
        {
            _cells[address] = new Cell(address, expression, _parser);
        }
    }

    private List<CellAddress> FindCellsReferencing(Func<CellAddress, bool> isTarget)
        => _cells.Values
            .Where(cell => !isTarget(cell.Address)
                           && ReferenceRewriter.GetReferences(cell.Expression).Any(isTarget))
            .Select(cell => cell.Address)
            .OrderBy(address => address.Row)
            .ThenBy(address => address.Column)
            .ToList();

    private static void EnsureNotReferenced(IReadOnlyList<CellAddress> referencingCells, string what)
    {
        if (referencingCells.Count > 0)
        {
            throw new SpreadsheetRuleException(
                $"Неможливо видалити {what}: на нього посилаються клітинки {string.Join(", ", referencingCells)}");
        }
    }

    /// <summary>Drops the removed cells, moves the rest and rewrites references in their expressions.</summary>
    private void RemoveLine(Func<CellAddress, bool> isRemoved, Func<CellAddress, CellAddress> shift)
    {
        var remaining = new Dictionary<CellAddress, Cell>();

        foreach (Cell cell in _cells.Values)
        {
            if (isRemoved(cell.Address))
            {
                continue;
            }

            string rewritten = ReferenceRewriter.Rewrite(cell.Expression, shift);
            if (rewritten != cell.Expression)
            {
                cell.SetExpression(rewritten, _parser);
            }

            cell.MoveTo(shift(cell.Address));
            remaining[cell.Address] = cell;
        }

        _cells = remaining;
    }
}

using System.Globalization;

namespace BoolSheet.Core.Model;

/// <summary>
/// Zero-based cell coordinates.
/// The text form is "A1": column letters (A..Z, AA, AB, ...) followed by a 1-based row number.
/// </summary>
public readonly record struct CellAddress
{
    public CellAddress(int column, int row)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(column);
        ArgumentOutOfRangeException.ThrowIfNegative(row);

        Column = column;
        Row = row;
    }

    public int Column { get; init; }

    public int Row { get; init; }

    public static CellAddress Parse(string text)
        => TryParse(text, out CellAddress address)
            ? address
            : throw new FormatException($"Invalid cell address: '{text}'");

    /// <summary>Parses "A1", "ab12" etc. Letters are case-insensitive; the row must be 1 or greater.</summary>
    public static bool TryParse(string? text, out CellAddress address)
    {
        address = default;

        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        // Column letters form a bijective base-26 number: A = 1, Z = 26, AA = 27.
        int i = 0;
        long column = 0;
        while (i < text.Length && char.IsAsciiLetter(text[i]))
        {
            column = (column * 26) + (char.ToUpperInvariant(text[i]) - 'A' + 1);
            if (column > int.MaxValue)
            {
                return false;
            }

            i++;
        }

        // At least one letter and at least one digit are required.
        if (i == 0 || i == text.Length)
        {
            return false;
        }

        if (!int.TryParse(text.AsSpan(i), NumberStyles.None, CultureInfo.InvariantCulture, out int row) || row < 1)
        {
            return false;
        }

        address = new CellAddress((int)column - 1, row - 1);
        return true;
    }

    /// <summary>Converts a zero-based column index to letters: 0 → A, 25 → Z, 26 → AA.</summary>
    public static string ColumnName(int column)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(column);

        string name = string.Empty;
        for (int n = column + 1; n > 0; n /= 26)
        {
            n--;
            name = (char)('A' + (n % 26)) + name;
        }

        return name;
    }

    public override string ToString()
        => ColumnName(Column) + (Row + 1).ToString(CultureInfo.InvariantCulture);
}

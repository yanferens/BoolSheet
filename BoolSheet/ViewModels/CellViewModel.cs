using System.Globalization;
using System.Numerics;
using BoolSheet.Core.Ast;
using CellAddress = BoolSheet.Core.Model.CellAddress;
using SheetCell = BoolSheet.Core.Model.Cell;

namespace BoolSheet.ViewModels;

/// <summary>
/// What one on-screen cell shows. Holds no logic of its own:
/// <see cref="MainViewModel"/> updates it from the model after every change.
/// </summary>
public sealed class CellViewModel : ViewModelBase
{
    public const string ErrorText = "#ПОМИЛКА";

    /// <summary>About 30 decimal digits; longer numbers are shown in scientific form.</summary>
    private const int MaxFullNumberBits = 100;

    private string _displayText = string.Empty;
    private string? _toolTip;
    private bool _hasError;
    private bool _isSelected;
    private CellValueKind _kind = CellValueKind.Empty;

    public CellViewModel(CellAddress address)
    {
        Address = address;
    }

    public CellAddress Address { get; }

    public string Name => Address.ToString();

    public string DisplayText
    {
        get => _displayText;
        private set => SetProperty(ref _displayText, value);
    }

    /// <summary>Shown on hover: the error message, or the "other side" of the cell (value vs expression).</summary>
    public string? ToolTip
    {
        get => _toolTip;
        private set => SetProperty(ref _toolTip, value);
    }

    public bool HasError
    {
        get => _hasError;
        private set => SetProperty(ref _hasError, value);
    }

    /// <summary>The kind of content shown; used for colouring and alignment.</summary>
    public CellValueKind Kind
    {
        get => _kind;
        private set => SetProperty(ref _kind, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    /// <summary>Refreshes the cell from the model. <paramref name="cell"/> is null for an empty cell.</summary>
    public void Update(SheetCell? cell, DisplayMode mode)
    {
        if (cell is null)
        {
            DisplayText = string.Empty;
            ToolTip = null;
            HasError = false;
            Kind = CellValueKind.Empty;
            return;
        }

        string value = FormatValue(cell);

        HasError = cell.HasError;
        DisplayText = mode == DisplayMode.Expression ? cell.Expression : value;
        ToolTip = cell.HasError
            ? cell.ErrorMessage
            : mode == DisplayMode.Expression ? value : cell.Expression;
        Kind = mode == DisplayMode.Expression ? CellValueKind.Expression : KindOf(cell);
    }

    private static CellValueKind KindOf(SheetCell cell)
    {
        if (cell.HasError)
        {
            return CellValueKind.Error;
        }

        if (cell.IsLogicalResult)
        {
            return Logic.IsTrue(cell.Value) ? CellValueKind.True : CellValueKind.False;
        }

        return CellValueKind.Number;
    }

    /// <summary>Logical results are shown as ІСТИНА / ХИБНІСТЬ, arithmetic results as numbers.</summary>
    public static string FormatValue(SheetCell cell)
    {
        if (cell.HasError)
        {
            return ErrorText;
        }

        if (cell.IsLogicalResult)
        {
            return Logic.IsTrue(cell.Value) ? "ІСТИНА" : "ХИБНІСТЬ";
        }

        return FormatNumber(cell.Value);
    }

    /// <summary>
    /// Short numbers are shown in full. Long ones (up to ~30 000 digits are allowed) are shown
    /// in scientific form: converting such a number to text for thousands of cells would freeze the UI.
    /// </summary>
    public static string FormatNumber(BigInteger value)
    {
        if (BigInteger.Abs(value).GetBitLength() <= MaxFullNumberBits)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        BigInteger magnitude = BigInteger.Abs(value);
        double log10 = BigInteger.Log10(magnitude);
        int exponent = (int)Math.Floor(log10);

        // Right next to a power of ten the floating-point logarithm can be off by one: check exactly.
        double fraction = log10 - exponent;
        if (fraction > 0.999999 && magnitude >= BigInteger.Pow(10, exponent + 1))
        {
            exponent++;
        }
        else if (fraction < 0.000001 && magnitude < BigInteger.Pow(10, exponent))
        {
            exponent--;
        }

        double mantissa = Math.Clamp(Math.Pow(10, log10 - exponent), 1, 9.99999);
        string sign = value.Sign < 0 ? "-" : string.Empty;

        return $"{sign}{mantissa.ToString("0.#####", CultureInfo.InvariantCulture)}E+{exponent} ({exponent + 1} цифр)";
    }
}

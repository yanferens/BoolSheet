namespace BoolSheet.ViewModels;

/// <summary>What a cell currently shows; the page picks the colour and alignment from it.</summary>
public enum CellValueKind
{
    Empty,
    Number,
    True,
    False,
    Error,

    /// <summary>The expression itself (in "expression" display mode).</summary>
    Expression,
}

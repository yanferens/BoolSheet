namespace BoolSheet;

/// <summary>
/// The colours of the sheet, which is built in code rather than in XAML.
/// Read from Resources/Styles/Colors.xaml, where every key has a Light and a Dark variant.
/// </summary>
internal sealed record SheetPalette(
    Color HeaderBackground,
    Color HeaderSelectedBackground,
    Color HeaderText,
    Color Accent,
    Color CellBackground,
    Color CellSelectedBackground,
    Color TextPrimary,
    Color TextSecondary,
    Color TrueText,
    Color FalseText,
    Color ErrorText,
    Color ErrorBackground)
{
    public static SheetPalette ForCurrentTheme()
    {
        string suffix = Application.Current?.RequestedTheme == AppTheme.Dark ? "Dark" : "Light";

        return new SheetPalette(
            Get("HeaderBackground"),
            Get("HeaderSelectedBackground"),
            Get("HeaderText"),
            Get("Accent"),
            Get("CellBackground"),
            Get("CellSelectedBackground"),
            Get("TextPrimary"),
            Get("TextSecondary"),
            Get("TrueText"),
            Get("FalseText"),
            Get("ErrorText"),
            Get("ErrorBackground"));

        Color Get(string key)
            => Application.Current?.Resources.TryGetValue(key + suffix, out object? value) == true && value is Color color
                ? color
                : Colors.Magenta;   // a loud colour makes a missing key obvious
    }
}

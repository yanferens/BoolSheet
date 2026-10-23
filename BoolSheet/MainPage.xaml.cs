using System.Globalization;
using System.Text;
using BoolSheet.Cloud;
using BoolSheet.Core.Services;
using BoolSheet.Core.Storage;
using BoolSheet.ViewModels;
using CommunityToolkit.Maui.Storage;
using CellAddress = BoolSheet.Core.Model.CellAddress;
using SheetGrid = BoolSheet.Core.Model.Grid;

namespace BoolSheet;

/// <summary>
/// The main window. Only UI work happens here: building the on-screen grid, showing dialogs
/// and forwarding menu clicks to <see cref="MainViewModel"/>, which holds all the logic.
/// </summary>
public partial class MainPage : ContentPage, IDialogService
{
    private const double CellWidth = 120;
    private const double CellHeight = 30;
    private const double RowHeaderWidth = 52;
    private const int MaxDriveFilesShown = 15;

    private static readonly FilePickerFileType SpreadsheetFileType = new(
        new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            [DevicePlatform.WinUI] = new[] { SpreadsheetSerializer.FileExtension, ".json" },
            [DevicePlatform.MacCatalyst] = new[] { "public.json", "public.data" },
            [DevicePlatform.iOS] = new[] { "public.json", "public.data" },
            [DevicePlatform.Android] = new[] { "application/json", "application/octet-stream" },
        });

    private readonly MainViewModel _viewModel;

    // Header labels, kept to highlight the row and column of the selected cell.
    private readonly List<Label> _columnHeaders = new();
    private readonly List<Label> _rowHeaders = new();
    private Label? _highlightedColumnHeader;
    private Label? _highlightedRowHeader;

    private SheetPalette _palette = SheetPalette.ForCurrentTheme();

    // Physical pixels per device-independent unit (1.25 at 125 % Windows scaling). Grid lines and cell
    // sizes are snapped to whole physical pixels: a 1-unit line at 125 % is 1.25 px, which Windows rounds
    // to 1 px in some places and 2 px in others, so the lines looked uneven and blurry.
    private double _scale = 1;

    public MainPage()
    {
        InitializeComponent();

        // Composition root: the only place that knows the concrete storages.
        var service = new SpreadsheetService(
            new LocalFileManager(),
            new CloudFileManager(new GoogleDriveStorage()));
        _viewModel = new MainViewModel(service, this);
        _viewModel.StructureChanged += (_, _) => BuildSheet();
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.SelectedCell))
            {
                HighlightHeaders();
            }
            else if (e.PropertyName == nameof(MainViewModel.Title))
            {
                UpdateWindowTitle();
            }
        };
        BindingContext = _viewModel;

        // The app follows the Windows theme; the sheet is built in code, so its colours are re-applied when it changes.
        if (Application.Current is { } app)
        {
            app.RequestedThemeChanged += (_, _) => Dispatcher.Dispatch(BuildSheet);
        }

        // The real scale of the window is known only once the sheet is on screen.
        SheetLayout.Loaded += (_, _) =>
        {
            if (Math.Abs(CurrentScale() - _scale) > 0.001)
            {
                BuildSheet();
            }
        };

        BuildSheet();
    }

    /// <summary>Set once the user agreed to close, so the window's closing handler does not ask again.</summary>
    internal static bool IsCloseConfirmed { get; private set; }

    /// <summary>Used by the window's closing handler (MauiProgram): true if closing would lose changes.</summary>
    internal bool NeedsCloseConfirmation() => !IsCloseConfirmed && _viewModel.HasUnsavedChanges();

    /// <summary>Asks "Save / Don't save / Cancel" and closes the program unless the user cancels.</summary>
    internal Task CloseAfterConfirmationAsync() => RunSafeAsync(async () =>
    {
        if (await _viewModel.ConfirmDiscardChangesAsync())
        {
            IsCloseConfirmed = true;
            Application.Current?.Quit();
        }
    });

    protected override void OnAppearing()
    {
        base.OnAppearing();
        UpdateWindowTitle();
    }

    // ---------- IDialogService ----------

    Task IDialogService.ShowMessageAsync(string title, string message)
        => DisplayAlert(title, message, "OK");

    Task<bool> IDialogService.ConfirmAsync(string title, string message)
        => DisplayAlert(title, message, "Так", "Ні");

    async Task<string?> IDialogService.ChooseAsync(string title, string cancel, params string[] choices)
    {
        string? choice = await DisplayActionSheet(title, cancel, null, choices);
        return choice is not null && choices.Contains(choice) ? choice : null;
    }

    Task<string?> IDialogService.PromptAsync(string title, string message, string initialValue)
        => DisplayPromptAsync(title, message, accept: "OK", cancel: "Скасувати", initialValue: initialValue);

    async Task<CloudFile?> IDialogService.PickCloudFileAsync(IReadOnlyList<CloudFile> files)
    {
        // A built-in action sheet is used instead of a custom modal page: it is more robust on Windows.
        List<CloudFile> shown = files.Take(MaxDriveFilesShown).ToList();

        // The number prefix keeps the labels unique even if several files share a name.
        string[] labels = shown
            .Select((file, index) => $"{index + 1}. {DescribeCloudFile(file)}")
            .ToArray();

        string? choice = await DisplayActionSheet("Відкрити з Google Drive (нові зверху)", "Скасувати", null, labels);

        int chosenIndex = Array.IndexOf(labels, choice);
        return chosenIndex >= 0 ? shown[chosenIndex] : null;
    }

    /// <summary>The system "Save as" dialog writes the file itself; the view model only supplies the content.</summary>
    async Task<string?> IDialogService.SaveFileAsAsync(string suggestedName, string content)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        FileSaverResult result = await FileSaver.Default.SaveAsync(suggestedName, stream, CancellationToken.None);
        return result.IsSuccessful ? result.FilePath : null;
    }

    async Task IDialogService.ShowUnexpectedErrorAsync(Exception exception)
    {
        CrashLog.Write(exception);
        await DisplayAlert(
            "Непередбачена помилка",
            $"{exception.Message}\n\nПрограма працює далі, таблицю не втрачено — краще збережіть її.\n" +
            $"Подробиці записано у файл:\n{CrashLog.FilePath}",
            "OK");
    }

    private static string DescribeCloudFile(CloudFile file)
        => file.ModifiedAt is { } modified
            ? $"{file.Name} — {modified.ToLocalTime().ToString("dd.MM.yyyy HH:mm", CultureInfo.CurrentCulture)}"
            : file.Name;

    // ---------- Building the sheet ----------

    /// <summary>Recreates the header row, the header column and one view per cell.</summary>
    private void BuildSheet()
    {
        _palette = SheetPalette.ForCurrentTheme();
        _scale = CurrentScale();

        // Exactly one physical pixel between cells: the grey background showing through draws the lines.
        SheetLayout.ColumnSpacing = 1 / _scale;
        SheetLayout.RowSpacing = 1 / _scale;

        SheetLayout.Children.Clear();
        SheetLayout.RowDefinitions.Clear();
        SheetLayout.ColumnDefinitions.Clear();
        _columnHeaders.Clear();
        _rowHeaders.Clear();
        _highlightedColumnHeader = null;
        _highlightedRowHeader = null;

        SheetLayout.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(Snap(RowHeaderWidth))));
        for (int column = 0; column < _viewModel.ColumnsCount; column++)
        {
            SheetLayout.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(Snap(CellWidth))));
        }

        SheetLayout.RowDefinitions.Add(new RowDefinition(new GridLength(Snap(CellHeight))));
        for (int row = 0; row < _viewModel.RowsCount; row++)
        {
            SheetLayout.RowDefinitions.Add(new RowDefinition(new GridLength(Snap(CellHeight))));
        }

        SheetLayout.Add(CreateHeader(string.Empty), 0, 0);

        for (int column = 0; column < _viewModel.ColumnsCount; column++)
        {
            Label header = CreateHeader(CellAddress.ColumnName(column));
            _columnHeaders.Add(header);
            SheetLayout.Add(header, column + 1, 0);
        }

        for (int row = 0; row < _viewModel.RowsCount; row++)
        {
            Label header = CreateHeader((row + 1).ToString(CultureInfo.InvariantCulture));
            _rowHeaders.Add(header);
            SheetLayout.Add(header, 0, row + 1);
        }

        foreach (CellViewModel cell in _viewModel.Cells)
        {
            SheetLayout.Add(CreateCellView(cell), cell.Address.Column + 1, cell.Address.Row + 1);
        }

        HighlightHeaders();
    }

    /// <summary>Rounds a size to a whole number of physical pixels.</summary>
    private double Snap(double size) => Math.Max(1, Math.Round(size * _scale)) / _scale;

    private double CurrentScale()
    {
#if WINDOWS
        if (SheetLayout.Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement { XamlRoot: { } root })
        {
            return root.RasterizationScale;
        }
#endif
        double density = DeviceDisplay.Current.MainDisplayInfo.Density;
        return density > 0 ? density : 1;
    }

    private Label CreateHeader(string text) => new()
    {
        Text = text,
        FontFamily = "OpenSansSemibold",
        FontSize = 12,
        TextColor = _palette.HeaderText,
        BackgroundColor = _palette.HeaderBackground,
        HorizontalTextAlignment = TextAlignment.Center,
        VerticalTextAlignment = TextAlignment.Center,
    };

    /// <summary>Marks the column letter and row number of the selected cell, as spreadsheets do.</summary>
    private void HighlightHeaders()
    {
        SetHeaderHighlight(_highlightedColumnHeader, false);
        SetHeaderHighlight(_highlightedRowHeader, false);
        _highlightedColumnHeader = null;
        _highlightedRowHeader = null;

        if (_viewModel.SelectedCell is not { } selected)
        {
            return;
        }

        CellAddress address = selected.Address;
        if (address.Column < _columnHeaders.Count)
        {
            _highlightedColumnHeader = _columnHeaders[address.Column];
            SetHeaderHighlight(_highlightedColumnHeader, true);
        }

        if (address.Row < _rowHeaders.Count)
        {
            _highlightedRowHeader = _rowHeaders[address.Row];
            SetHeaderHighlight(_highlightedRowHeader, true);
        }
    }

    private void SetHeaderHighlight(Label? header, bool highlighted)
    {
        if (header is null)
        {
            return;
        }

        header.BackgroundColor = highlighted ? _palette.HeaderSelectedBackground : _palette.HeaderBackground;
        header.TextColor = highlighted ? _palette.Accent : _palette.HeaderText;
    }

    private View CreateCellView(CellViewModel cell)
    {
        var label = new Label
        {
            LineBreakMode = LineBreakMode.TailTruncation,
            VerticalTextAlignment = TextAlignment.Center,
            FontSize = 13,
            Padding = new Thickness(8, 0),
        };
        label.SetBinding(Label.TextProperty, nameof(CellViewModel.DisplayText));

        // The frame always stays 2 px wide and only changes colour (accent when selected, the cell's own
        // background otherwise). Changing its width or removing it leaves traces of the old frame on Windows.
        var border = new Border
        {
            BindingContext = cell,
            Content = label,
            StrokeThickness = Snap(2),
            Padding = 0,
        };
        border.SetBinding(ToolTipProperties.TextProperty, nameof(CellViewModel.ToolTip));

        // Colours, alignment and the selection frame follow the view model.
        void ApplyState()
        {
            ApplyKind(label, cell.Kind);

            Color background = cell.HasError
                ? _palette.ErrorBackground
                : cell.IsSelected ? _palette.CellSelectedBackground : _palette.CellBackground;

            border.BackgroundColor = background;
            border.Stroke = new SolidColorBrush(cell.IsSelected ? _palette.Accent : background);
        }

        cell.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(CellViewModel.IsSelected)
                or nameof(CellViewModel.HasError)
                or nameof(CellViewModel.Kind))
            {
                ApplyState();
            }
        };
        ApplyState();

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => RunSafe(() => _viewModel.SelectCell(cell));
        border.GestureRecognizers.Add(tap);

        var doubleTap = new TapGestureRecognizer { NumberOfTapsRequired = 2 };
        doubleTap.Tapped += (_, _) => RunSafe(() =>
        {
            _viewModel.SelectCell(cell);
            FormulaEntry.Focus();
        });
        border.GestureRecognizers.Add(doubleTap);

        return border;
    }

    /// <summary>
    /// Numbers are right-aligned like in other spreadsheets; logical values and errors are centred
    /// and coloured; expressions use a monospaced font.
    /// </summary>
    private void ApplyKind(Label label, CellValueKind kind)
    {
        (Color color, TextAlignment alignment, string font) = kind switch
        {
            CellValueKind.Number => (_palette.TextPrimary, TextAlignment.End, "OpenSansRegular"),
            CellValueKind.True => (_palette.TrueText, TextAlignment.Center, "OpenSansSemibold"),
            CellValueKind.False => (_palette.FalseText, TextAlignment.Center, "OpenSansSemibold"),
            CellValueKind.Error => (_palette.ErrorText, TextAlignment.Center, "OpenSansSemibold"),
            CellValueKind.Expression => (_palette.TextSecondary, TextAlignment.Start, "Consolas"),
            _ => (_palette.TextPrimary, TextAlignment.Start, "OpenSansRegular"),
        };

        label.TextColor = color;
        label.HorizontalTextAlignment = alignment;
        label.FontFamily = font;
    }

    // ---------- "Файл" menu ----------

    private async void OnNewClicked(object? sender, EventArgs e) => await RunSafeAsync(async () =>
    {
        if (!await _viewModel.ConfirmDiscardChangesAsync())
        {
            return;
        }

        int? rows = await AskNumberAsync("Нова таблиця", "Кількість рядків", SpreadsheetService.DefaultRows, SheetGrid.MaxRows);
        if (rows is null)
        {
            return;
        }

        int? columns = await AskNumberAsync("Нова таблиця", "Кількість стовпчиків", SpreadsheetService.DefaultColumns, SheetGrid.MaxColumns);
        if (columns is null)
        {
            return;
        }

        _viewModel.CreateSpreadsheet(rows.Value, columns.Value);
    });

    private async void OnOpenClicked(object? sender, EventArgs e) => await RunSafeAsync(async () =>
    {
        if (!await _viewModel.ConfirmDiscardChangesAsync())
        {
            return;
        }

        FileResult? file = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Відкрити таблицю",
            FileTypes = SpreadsheetFileType,
        });

        if (file is not null)
        {
            await _viewModel.OpenAsync(file.FullPath);
        }
    });

    private async void OnSaveClicked(object? sender, EventArgs e) => await RunSafeAsync(_viewModel.SaveAsync);

    private async void OnSaveAsClicked(object? sender, EventArgs e) => await RunSafeAsync(_viewModel.SaveAsAsync);

    private async void OnOpenDriveClicked(object? sender, EventArgs e) => await RunSafeAsync(_viewModel.OpenFromDriveAsync);

    private async void OnSaveDriveClicked(object? sender, EventArgs e) => await RunSafeAsync(_viewModel.SaveToDriveAsync);

    private async void OnDriveSignOutClicked(object? sender, EventArgs e) => await RunSafeAsync(_viewModel.SignOutFromDriveAsync);

    private async void OnExitClicked(object? sender, EventArgs e) => await CloseAfterConfirmationAsync();

    // ---------- "Таблиця" and "Вигляд" menus ----------

    private async void OnAddRowClicked(object? sender, EventArgs e) => await RunSafeAsync(_viewModel.AddRowAsync);

    private async void OnAddColumnClicked(object? sender, EventArgs e) => await RunSafeAsync(_viewModel.AddColumnAsync);

    private async void OnDeleteRowClicked(object? sender, EventArgs e) => await RunSafeAsync(_viewModel.DeleteSelectedRowAsync);

    private async void OnDeleteColumnClicked(object? sender, EventArgs e) => await RunSafeAsync(_viewModel.DeleteSelectedColumnAsync);

    private void OnClearCellClicked(object? sender, EventArgs e) => _viewModel.ClearCellCommand.Execute(null);

    private void OnToggleModeClicked(object? sender, EventArgs e) => _viewModel.ToggleDisplayModeCommand.Execute(null);

    // ---------- "Довідка" menu ----------

    private async void OnHelpClicked(object? sender, EventArgs e)
        => await RunSafeAsync(() => DisplayAlert(HelpContent.UsageTitle, HelpContent.Usage, "Зрозуміло"));

    private async void OnAboutClicked(object? sender, EventArgs e)
        => await RunSafeAsync(() => DisplayAlert(HelpContent.AboutTitle, HelpContent.About, "OK"));

    // ---------- Helpers ----------

    /// <summary>
    /// Every event handler goes through here: an exception thrown from an <c>async void</c> handler has
    /// nobody to return to and would close the program, losing the user's unsaved table.
    /// </summary>
    private async Task RunSafeAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            await ((IDialogService)this).ShowUnexpectedErrorAsync(ex);
        }
    }

    private void RunSafe(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            _ = ((IDialogService)this).ShowUnexpectedErrorAsync(ex);
        }
    }

    /// <summary>The window caption shows the file name and "*" for unsaved changes.</summary>
    private void UpdateWindowTitle()
    {
        if (Window is not null)
        {
            Window.Title = _viewModel.Title;
        }
    }

    /// <summary>Asks for a whole number in [1, max]; returns null if the user cancels.</summary>
    private async Task<int?> AskNumberAsync(string title, string what, int initialValue, int max)
    {
        while (true)
        {
            string? text = await DisplayPromptAsync(
                title,
                $"{what} (від 1 до {max}):",
                accept: "OK",
                cancel: "Скасувати",
                keyboard: Keyboard.Numeric,
                initialValue: initialValue.ToString(CultureInfo.InvariantCulture));

            if (text is null)
            {
                return null;
            }

            if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int value) && value >= 1 && value <= max)
            {
                return value;
            }

            await DisplayAlert(title, $"Введіть ціле число від 1 до {max}.", "OK");
        }
    }
}

using System.Windows.Input;
using BoolSheet.Core.Model;
using BoolSheet.Core.Services;
using BoolSheet.Core.Storage;
using SheetGrid = BoolSheet.Core.Model.Grid;

namespace BoolSheet.ViewModels;

/// <summary>
/// State and actions of the main window. Contains no UI code: the page binds to it
/// and forwards user actions to it, so all the logic lives here and in <see cref="SpreadsheetService"/>
/// (this is what avoids the "magic pushbutton" anti-pattern). Does not depend on MAUI, so it is unit-tested.
/// </summary>
public sealed class MainViewModel : ViewModelBase
{
    public const string SaveChoice = "Зберегти";
    public const string DiscardChoice = "Не зберігати";

    private readonly SpreadsheetService _service;
    private readonly IDialogService _dialogs;

    private List<CellViewModel> _cells = new();
    private Dictionary<CellAddress, CellViewModel> _cellsByAddress = new();
    private CellViewModel? _selectedCell;
    private DisplayMode _displayMode = DisplayMode.Value;
    private string _formulaText = string.Empty;
    private string _statusText = "Готово";
    private DocumentLocation? _document;   // the file whose name is shown in the title
    private DocumentLocation? _localCopy;  // the table's file on disk, if any
    private DocumentLocation? _cloudCopy;  // the table's file on Google Drive, if any
    private bool _isDirty;
    private bool _isBusy;
    private int _changeCounter;

    public MainViewModel(SpreadsheetService service, IDialogService dialogs)
    {
        _service = service;
        _dialogs = dialogs;

        CommitFormulaCommand = new RelayCommand(() => RunSafely(CommitFormula));
        ClearCellCommand = new RelayCommand(() => RunSafely(ClearSelectedCell));
        ToggleDisplayModeCommand = new RelayCommand(() => RunSafely(ToggleDisplayMode));

        RebuildCells();
    }

    /// <summary>Raised when the number of rows or columns changes and the on-screen grid must be rebuilt.</summary>
    public event EventHandler? StructureChanged;

    public ICommand CommitFormulaCommand { get; }

    public ICommand ClearCellCommand { get; }

    public ICommand ToggleDisplayModeCommand { get; }

    public IReadOnlyList<CellViewModel> Cells => _cells;

    public int RowsCount => Sheet.RowsCount;

    public int ColumnsCount => Sheet.ColumnsCount;

    /// <summary>The sheet size for the status bar, e.g. "10 × 10".</summary>
    public string SizeText => $"{RowsCount} × {ColumnsCount}";

    /// <summary>Where the current table is stored, for the status bar: on disk, on Google Drive or both.</summary>
    public string LocationText => (_localCopy, _cloudCopy) switch
    {
        (not null, not null) => "Диск і Google Drive",
        (not null, null) => "Файл на диску",
        (null, not null) => "Google Drive",
        _ => "Не збережено",
    };

    public CellViewModel? SelectedCell => _selectedCell;

    public string SelectedCellName => _selectedCell?.Name ?? string.Empty;

    /// <summary>The text in the formula bar; applied to the selected cell by <see cref="CommitFormulaCommand"/>.</summary>
    public string FormulaText
    {
        get => _formulaText;
        set => SetProperty(ref _formulaText, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public DisplayMode DisplayMode
    {
        get => _displayMode;
        private set
        {
            if (SetProperty(ref _displayMode, value))
            {
                OnPropertyChanged(nameof(DisplayModeButtonText));
                RefreshCells();
            }
        }
    }

    public string DisplayModeButtonText
        => DisplayMode == DisplayMode.Value ? "Режим: ЗНАЧЕННЯ" : "Режим: ВИРАЗ";

    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            if (SetProperty(ref _isDirty, value))
            {
                OnPropertyChanged(nameof(Title));
            }
        }
    }

    /// <summary>True while a slow (cloud) operation is running; the UI shows a progress indicator.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public bool HasFilePath => _document is not null;

    public string Title
    {
        get
        {
            string name = _document?.DisplayName ?? "Нова таблиця";
            string place = (_localCopy, _cloudCopy) switch
            {
                (not null, not null) => " (диск + Google Drive)",
                (null, not null) => " (Google Drive)",
                _ => string.Empty,
            };
            return $"BoolSheet — {name}{place}{(IsDirty ? " *" : string.Empty)}";
        }
    }

    private SheetGrid Sheet => _service.Grid;

    // ---------- Cells ----------

    public void SelectCell(CellViewModel cell)
    {
        // Like in a spreadsheet, moving to another cell keeps what was typed for the previous one.
        if (cell != _selectedCell)
        {
            CommitPendingEdit();
        }

        if (_selectedCell is not null)
        {
            _selectedCell.IsSelected = false;
        }

        _selectedCell = cell;
        cell.IsSelected = true;

        FormulaText = Sheet.GetCell(cell.Address)?.Expression ?? string.Empty;
        StatusText = DescribeCell(cell.Address);

        OnPropertyChanged(nameof(SelectedCell));
        OnPropertyChanged(nameof(SelectedCellName));
    }

    /// <summary>
    /// Applies the text of the formula bar to the selected cell if it was edited but not applied yet
    /// (the user clicked another cell, saved, closed or changed the table). Does not move the selection.
    /// </summary>
    public void CommitPendingEdit()
    {
        if (_selectedCell is not null && FormulaText != CurrentExpression(_selectedCell.Address))
        {
            _service.EditCell(_selectedCell.Name, FormulaText);
            MarkChanged();
        }
    }

    /// <summary>Applies a pending edit first, then tells whether the table has unsaved changes.</summary>
    public bool HasUnsavedChanges()
    {
        CommitPendingEdit();
        return IsDirty;
    }

    private void CommitFormula()
    {
        if (_selectedCell is null)
        {
            StatusText = "Спочатку виберіть клітинку";
            return;
        }

        CellAddress address = _selectedCell.Address;

        // Pressing Enter on an unchanged expression only moves down; the table does not become "changed".
        if (FormulaText != CurrentExpression(address))
        {
            _service.EditCell(address.ToString(), FormulaText);
            MarkChanged();
        }

        // Like a spreadsheet: after a successful edit move down; after an error stay to let the user fix it.
        bool hasError = Sheet.GetCell(address)?.HasError == true;
        if (!hasError && address.Row + 1 < Sheet.RowsCount)
        {
            SelectCell(FindCell(address with { Row = address.Row + 1 }));
        }

        StatusText = DescribeCell(address);
    }

    private void ClearSelectedCell()
    {
        if (_selectedCell is null)
        {
            return;
        }

        if (Sheet.GetCell(_selectedCell.Address) is not null)
        {
            _service.ClearCell(_selectedCell.Name);
            MarkChanged();
        }

        FormulaText = string.Empty;
        StatusText = $"Клітинку {_selectedCell.Name} очищено";
    }

    private void ToggleDisplayMode()
        => DisplayMode = DisplayMode == DisplayMode.Value ? DisplayMode.Expression : DisplayMode.Value;

    // ---------- Rows and columns ----------

    public Task AddRowAsync() => ChangeStructureAsync(_service.AddRow, "Рядок додано");

    public Task AddColumnAsync() => ChangeStructureAsync(_service.AddColumn, "Стовпчик додано");

    public async Task DeleteSelectedRowAsync()
    {
        if (_selectedCell is null)
        {
            await _dialogs.ShowMessageAsync("Видалення рядка", "Виберіть клітинку в рядку, який треба видалити.");
            return;
        }

        int row = _selectedCell.Address.Row;
        if (await _dialogs.ConfirmAsync("Видалення рядка", $"Видалити рядок {row + 1}?"))
        {
            await ChangeStructureAsync(() => _service.DeleteRow(row), $"Рядок {row + 1} видалено");
        }
    }

    public async Task DeleteSelectedColumnAsync()
    {
        if (_selectedCell is null)
        {
            await _dialogs.ShowMessageAsync("Видалення стовпчика", "Виберіть клітинку в стовпчику, який треба видалити.");
            return;
        }

        int column = _selectedCell.Address.Column;
        string name = CellAddress.ColumnName(column);
        if (await _dialogs.ConfirmAsync("Видалення стовпчика", $"Видалити стовпчик {name}?"))
        {
            await ChangeStructureAsync(() => _service.DeleteColumn(column), $"Стовпчик {name} видалено");
        }
    }

    private async Task ChangeStructureAsync(Action change, string successMessage)
    {
        CommitPendingEdit();

        try
        {
            change();
        }
        catch (SpreadsheetRuleException ex)
        {
            // Only rule violations (e.g. a referenced row) are expected here; real bugs are not hidden.
            await _dialogs.ShowMessageAsync("Неможливо виконати", ex.Message);
            return;
        }

        RebuildCells();
        MarkChanged();
        StatusText = successMessage;
    }

    // ---------- Files ----------

    public void CreateSpreadsheet(int rows, int columns)
    {
        _service.CreateSpreadsheet(rows, columns);
        ResetDocument(document: null, $"Створено таблицю {rows} × {columns}");
    }

    public Task OpenAsync(string filePath) => OpenDocumentAsync(DocumentLocation.Local(filePath));

    /// <summary>
    /// Saves the table everywhere it is stored: to its file on disk and/or to its file on Google Drive,
    /// so the two copies never drift apart. A table that was never saved goes to "Save as".
    /// Returns true if every copy is saved now.
    /// </summary>
    public async Task<bool> SaveAsync()
    {
        CommitPendingEdit();

        DocumentLocation[] copies = new[] { _localCopy, _cloudCopy }.OfType<DocumentLocation>().ToArray();
        if (copies.Length == 0)
        {
            return await SaveAsAsync();
        }

        if (_isBusy)
        {
            return false;
        }

        IsBusy = true;
        try
        {
            int revision = _changeCounter;
            foreach (DocumentLocation copy in copies)
            {
                StatusText = $"Збереження {Describe(copy)}…";
                await _service.SaveSpreadsheetAsync(copy);
            }

            CompleteSave(revision);
            return true;
        }
        catch (OperationCanceledException)
        {
            StatusText = "Збереження скасовано";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusText = "Не вдалося зберегти";
            await _dialogs.ShowMessageAsync("Не вдалося зберегти файл", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }

        return false;
    }

    /// <summary>Asks where to save the table on disk and saves it there. Returns true if it was saved.</summary>
    public async Task<bool> SaveAsAsync()
    {
        CommitPendingEdit();

        string suggestedName = Path.GetFileNameWithoutExtension(_document?.DisplayName ?? "Таблиця")
                               + SpreadsheetSerializer.FileExtension;
        int revision = _changeCounter;

        string? path;
        try
        {
            path = await _dialogs.SaveFileAsAsync(suggestedName, _service.Export());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await _dialogs.ShowMessageAsync("Не вдалося зберегти файл", ex.Message);
            return false;
        }

        if (path is null)
        {
            return false;
        }

        RememberCopy(DocumentLocation.Local(path));

        // The copy on Google Drive (if any) gets the same content, so "both places" stays true.
        if (_cloudCopy is not null && !await TrySaveCopyAsync(_cloudCopy))
        {
            return false;
        }

        CompleteSave(revision);
        return true;
    }

    /// <summary>
    /// Asks what to do with unsaved changes before they would be lost: save, discard or cancel.
    /// Returns true if it is fine to continue.
    /// </summary>
    public async Task<bool> ConfirmDiscardChangesAsync()
    {
        if (!HasUnsavedChanges())
        {
            return true;
        }

        string? choice = await _dialogs.ChooseAsync(
            "Таблицю змінено. Зберегти зміни?", "Скасувати", SaveChoice, DiscardChoice);

        return choice switch
        {
            DiscardChoice => true,
            SaveChoice => await SaveAsync(),
            _ => false,
        };
    }

    // ---------- Google Drive ----------

    /// <summary>Lets the user choose a spreadsheet stored on Google Drive and opens it.</summary>
    public async Task OpenFromDriveAsync()
    {
        if (_isBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            StatusText = "Підключення до Google Drive…";
            IReadOnlyList<CloudFile> files = await _service.ListCloudFilesAsync();
            if (files.Count == 0)
            {
                StatusText = "Готово";
                await _dialogs.ShowMessageAsync(
                    "Google Drive",
                    "На Google Drive не знайдено жодної таблиці BoolSheet. Спочатку збережіть таблицю командою «Зберегти на Google Drive».");
                return;
            }

            CloudFile? chosen = await _dialogs.PickCloudFileAsync(files);
            if (chosen is null)
            {
                StatusText = "Готово";
                return;
            }

            // Saving the old table may need the busy flag (it is the same kind of operation).
            IsBusy = false;
            if (!await ConfirmDiscardChangesAsync())
            {
                StatusText = "Готово";
                return;
            }

            IsBusy = true;
            StatusText = $"Завантаження {chosen.Name}…";
            await OpenDocumentAsync(DocumentLocation.Cloud(chosen));
        }
        catch (OperationCanceledException)
        {
            StatusText = "Операцію скасовано";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusText = "Помилка Google Drive";
            await _dialogs.ShowMessageAsync("Google Drive", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Asks for a name and saves the spreadsheet to Google Drive as a new file; it becomes the current file.</summary>
    public async Task SaveToDriveAsync()
    {
        if (_isBusy)
        {
            return;
        }

        CommitPendingEdit();

        string? name = await _dialogs.PromptAsync(
            "Збереження на Google Drive",
            "Назва файлу:",
            Path.GetFileNameWithoutExtension(_document?.DisplayName ?? "Таблиця"));
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        IsBusy = true;
        try
        {
            StatusText = "Збереження на Google Drive…";
            int revision = _changeCounter;
            DocumentLocation location = await _service.SaveToCloudAsNewAsync(WithFileExtension(name.Trim()));
            RememberCopy(location);

            // The file on disk (if any) gets the same content, so "both places" stays true.
            if (_localCopy is not null)
            {
                await _service.SaveSpreadsheetAsync(_localCopy);
            }

            CompleteSave(revision);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Збереження скасовано";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusText = "Не вдалося зберегти";
            await _dialogs.ShowMessageAsync("Google Drive", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Forgets the Google account, so the next Drive operation asks to sign in again.</summary>
    public async Task SignOutFromDriveAsync()
    {
        if (_isBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _service.SignOutFromCloudAsync();
            StatusText = "Вихід з Google акаунта виконано";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await _dialogs.ShowMessageAsync("Google Drive", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string WithFileExtension(string name)
        => name.EndsWith(SpreadsheetSerializer.FileExtension, StringComparison.OrdinalIgnoreCase)
            ? name
            : name + SpreadsheetSerializer.FileExtension;

    private async Task OpenDocumentAsync(DocumentLocation location)
    {
        try
        {
            await _service.OpenSpreadsheetAsync(location);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            // InvalidDataException means the file exists but is not a valid spreadsheet.
            await _dialogs.ShowMessageAsync("Не вдалося відкрити файл", ex.Message);
            return;
        }

        ResetDocument(location, $"Відкрито {location.DisplayName}");
    }

    /// <summary>Remembers a new file of the table; it also becomes the file shown in the title.</summary>
    private void RememberCopy(DocumentLocation location)
    {
        _document = location;
        if (location.Kind == StorageKind.Local)
        {
            _localCopy = location;
        }
        else
        {
            _cloudCopy = location;
        }
    }

    /// <summary>
    /// Records a successful save. The table counts as saved only if it was not edited
    /// while a slow save was running (<paramref name="revision"/> is the change counter at the start).
    /// </summary>
    private void CompleteSave(int revision)
    {
        IsDirty = _changeCounter != revision;
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(HasFilePath));
        OnPropertyChanged(nameof(LocationText));
        StatusText = "Збережено: " + LocationText;
    }

    /// <summary>Saves one copy, reporting a failure to the user. Returns true on success.</summary>
    private async Task<bool> TrySaveCopyAsync(DocumentLocation copy)
    {
        IsBusy = true;
        try
        {
            StatusText = $"Збереження {Describe(copy)}…";
            await _service.SaveSpreadsheetAsync(copy);
            return true;
        }
        catch (OperationCanceledException)
        {
            StatusText = "Збереження скасовано";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusText = "Не вдалося зберегти";
            await _dialogs.ShowMessageAsync("Не вдалося зберегти файл", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }

        return false;
    }

    private static string Describe(DocumentLocation location)
        => location.Kind == StorageKind.Cloud ? $"{location.DisplayName} на Google Drive" : location.DisplayName;

    // ---------- Helpers ----------

    /// <summary>
    /// Safety net for the commands: an unexpected exception (a bug) is reported instead of closing
    /// the program together with the user's unsaved table.
    /// </summary>
    private void RunSafely(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            StatusText = "Сталася непередбачена помилка";
            _ = _dialogs.ShowUnexpectedErrorAsync(ex);
        }
    }

    private void ResetDocument(DocumentLocation? document, string status)
    {
        _document = document;
        _localCopy = document?.Kind == StorageKind.Local ? document : null;
        _cloudCopy = document?.Kind == StorageKind.Cloud ? document : null;
        _isDirty = false;
        RebuildCells();
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(HasFilePath));
        OnPropertyChanged(nameof(LocationText));
        OnPropertyChanged(nameof(Title));
        StatusText = status;
    }

    private void MarkChanged()
    {
        _changeCounter++;
        IsDirty = true;
        RefreshCells();
    }

    private string CurrentExpression(CellAddress address) => Sheet.GetCell(address)?.Expression ?? string.Empty;

    /// <summary>Recreates the cell view models after the grid size changed; keeps the selection where possible.</summary>
    private void RebuildCells()
    {
        CellAddress selected = _selectedCell?.Address ?? new CellAddress(0, 0);

        _cells = new List<CellViewModel>(Sheet.RowsCount * Sheet.ColumnsCount);
        _cellsByAddress = new Dictionary<CellAddress, CellViewModel>(Sheet.RowsCount * Sheet.ColumnsCount);
        for (int row = 0; row < Sheet.RowsCount; row++)
        {
            for (int column = 0; column < Sheet.ColumnsCount; column++)
            {
                var cell = new CellViewModel(new CellAddress(column, row));
                _cells.Add(cell);
                _cellsByAddress.Add(cell.Address, cell);
            }
        }

        _selectedCell = null;
        RefreshCells();

        OnPropertyChanged(nameof(Cells));
        OnPropertyChanged(nameof(RowsCount));
        OnPropertyChanged(nameof(ColumnsCount));
        OnPropertyChanged(nameof(SizeText));
        StructureChanged?.Invoke(this, EventArgs.Empty);

        CellAddress clamped = new(
            Math.Min(selected.Column, Sheet.ColumnsCount - 1),
            Math.Min(selected.Row, Sheet.RowsCount - 1));
        SelectCell(FindCell(clamped));
    }

    /// <summary>Copies the current values from the model into every cell view model.</summary>
    private void RefreshCells()
    {
        foreach (CellViewModel cell in _cells)
        {
            cell.Update(Sheet.GetCell(cell.Address), DisplayMode);
        }
    }

    /// <summary>Looks the cell up by address, so it does not depend on the order of <see cref="Cells"/>.</summary>
    private CellViewModel FindCell(CellAddress address) => _cellsByAddress[address];

    private string DescribeCell(CellAddress address)
    {
        // "var": the name Cell is also a MAUI type in the app project.
        var cell = Sheet.GetCell(address);
        if (cell is null)
        {
            return $"{address}: порожня клітинка";
        }

        return cell.HasError
            ? $"{address}: помилка — {cell.ErrorMessage}"
            : $"{address} = {CellViewModel.FormatValue(cell)}";
    }
}

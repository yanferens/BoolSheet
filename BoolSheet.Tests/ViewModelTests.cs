using System.Numerics;
using BoolSheet.Core.Services;
using BoolSheet.Core.Storage;
using BoolSheet.ViewModels;
using Xunit;

namespace BoolSheet.Tests;

/// <summary>Dialogs that answer with prepared values and record what was shown.</summary>
internal sealed class FakeDialogs : IDialogService
{
    public List<string> Messages { get; } = new();

    public bool ConfirmAnswer { get; set; } = true;

    public string? ChoiceAnswer { get; set; }

    /// <summary>Where the fake "Save as" dialog writes; null means the user cancels.</summary>
    public string? SaveAsPath { get; set; }

    public Exception? UnexpectedError { get; private set; }

    /// <summary>The answer to text prompts (e.g. the file name for Google Drive); null means cancel.</summary>
    public string? PromptAnswer { get; set; }

    public Task ShowMessageAsync(string title, string message)
    {
        Messages.Add(message);
        return Task.CompletedTask;
    }

    public Task<bool> ConfirmAsync(string title, string message) => Task.FromResult(ConfirmAnswer);

    public Task<string?> ChooseAsync(string title, string cancel, params string[] choices) => Task.FromResult(ChoiceAnswer);

    public Task<string?> PromptAsync(string title, string message, string initialValue) => Task.FromResult(PromptAnswer);

    public Task<CloudFile?> PickCloudFileAsync(IReadOnlyList<CloudFile> files) => Task.FromResult<CloudFile?>(null);

    public async Task<string?> SaveFileAsAsync(string suggestedName, string content)
    {
        if (SaveAsPath is not null)
        {
            await File.WriteAllTextAsync(SaveAsPath, content);
        }

        return SaveAsPath;
    }

    public Task ShowUnexpectedErrorAsync(Exception exception)
    {
        UnexpectedError = exception;
        return Task.CompletedTask;
    }
}

public class ViewModelTests
{
    private readonly SpreadsheetService _service = new(new LocalFileManager());
    private readonly FakeDialogs _dialogs = new();

    [Fact]
    public void SelectingAnotherCell_AppliesTypedExpression()
    {
        MainViewModel vm = CreateViewModel();
        vm.SelectCell(CellAt(vm, "A1"));
        vm.FormulaText = "2 + 3";

        vm.SelectCell(CellAt(vm, "B1"));

        Assert.Equal("2 + 3", _service.Grid.GetCell("A1")?.Expression);
        Assert.True(vm.IsDirty);
    }

    [Fact]
    public void CommittingUnchangedExpression_DoesNotMarkTableChanged()
    {
        MainViewModel vm = CreateViewModel();
        vm.SelectCell(CellAt(vm, "A1"));

        vm.CommitFormulaCommand.Execute(null);

        Assert.False(vm.IsDirty);
        Assert.Equal("A2", vm.SelectedCellName);   // still moves down like Enter in a spreadsheet
    }

    [Fact]
    public async Task DeletingReferencedRow_ShowsRuleMessageAndKeepsTable()
    {
        _service.EditCell("A1", "A2 + 1");
        MainViewModel vm = CreateViewModel();
        vm.SelectCell(CellAt(vm, "B2"));

        await vm.DeleteSelectedRowAsync();

        Assert.Equal(SpreadsheetService.DefaultRows, vm.RowsCount);
        Assert.Contains(_dialogs.Messages, message => message.Contains("посилаються"));
    }

    [Fact]
    public async Task AfterAddingColumn_EnterMovesToTheRightCell()
    {
        MainViewModel vm = CreateViewModel();
        await vm.AddColumnAsync();
        string newColumn = Core.Model.CellAddress.ColumnName(vm.ColumnsCount - 1);
        vm.SelectCell(CellAt(vm, $"{newColumn}1"));
        vm.FormulaText = "1";

        vm.CommitFormulaCommand.Execute(null);

        Assert.Equal($"{newColumn}2", vm.SelectedCellName);
    }

    [Fact]
    public async Task UnsavedChanges_SaveChoiceWithoutFile_GoesThroughSaveAs()
    {
        string path = Path.Combine(Path.GetTempPath(), $"boolsheet-vm-{Guid.NewGuid():N}{SpreadsheetSerializer.FileExtension}");
        MainViewModel vm = CreateViewModel();
        vm.SelectCell(CellAt(vm, "A1"));
        vm.FormulaText = "1 < 2";
        _dialogs.ChoiceAnswer = MainViewModel.SaveChoice;
        _dialogs.SaveAsPath = path;

        try
        {
            bool canContinue = await vm.ConfirmDiscardChangesAsync();

            Assert.True(canContinue);
            Assert.False(vm.IsDirty);
            Assert.Contains(Path.GetFileName(path), vm.Title);
            var saved = SpreadsheetSerializer.Deserialize(await File.ReadAllTextAsync(path));
            Assert.Equal("1 < 2", saved.GetCell("A1")?.Expression);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task UnsavedChanges_Cancel_StopsTheAction()
    {
        MainViewModel vm = CreateViewModel();
        vm.SelectCell(CellAt(vm, "A1"));
        vm.FormulaText = "1";
        _dialogs.ChoiceAnswer = null;

        Assert.False(await vm.ConfirmDiscardChangesAsync());
        Assert.True(vm.IsDirty);
    }

    [Theory]
    [InlineData(100, "1E+100 (101 цифр)")]
    [InlineData(1000, "1E+1000 (1001 цифр)")]
    public void HugeNumber_IsShownInScientificForm(int power, string expected)
    {
        Assert.Equal(expected, CellViewModel.FormatNumber(BigInteger.Pow(10, power)));
        Assert.Equal("-" + expected, CellViewModel.FormatNumber(-BigInteger.Pow(10, power)));
    }

    [Fact]
    public void NumberJustBelowPowerOfTen_HasCorrectDigitCount()
    {
        string text = CellViewModel.FormatNumber(BigInteger.Pow(10, 100) - 1);

        Assert.Contains("E+99 (100 цифр)", text);
    }

    [Fact]
    public void ShortNumber_IsShownInFull()
    {
        Assert.Equal("123456789012345678901234567890", CellViewModel.FormatNumber(BigInteger.Parse("123456789012345678901234567890")));
    }

    [Fact]
    public async Task SavedOnDiskAndOnDrive_ShowsBothAndSaveUpdatesBoth()
    {
        string path = Path.Combine(Path.GetTempPath(), $"boolsheet-vm-{Guid.NewGuid():N}{SpreadsheetSerializer.FileExtension}");
        var cloud = new FakeCloudStorage();
        var service = new SpreadsheetService(new LocalFileManager(), new CloudFileManager(cloud));
        var vm = new MainViewModel(service, _dialogs);
        _dialogs.SaveAsPath = path;
        _dialogs.PromptAnswer = "lab";

        try
        {
            vm.SelectCell(CellAt(vm, "A1"));
            vm.FormulaText = "1";
            await vm.SaveAsAsync();
            await vm.SaveToDriveAsync();

            Assert.Equal("Диск і Google Drive", vm.LocationText);
            Assert.False(vm.IsDirty);

            vm.SelectCell(CellAt(vm, "A1"));
            vm.FormulaText = "2";
            Assert.True(await vm.SaveAsync());

            CloudFile driveFile = Assert.Single(await cloud.ListAsync());
            Assert.Equal("lab.boolsheet", driveFile.Name);
            Assert.Equal("2", SpreadsheetSerializer.Deserialize(await cloud.DownloadAsync(driveFile.Id)).GetCell("A1")?.Expression);
            Assert.Equal("2", SpreadsheetSerializer.Deserialize(await File.ReadAllTextAsync(path)).GetCell("A1")?.Expression);
            Assert.False(vm.IsDirty);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void NewTable_IsNotSavedAnywhere()
    {
        MainViewModel vm = CreateViewModel();

        Assert.Equal("Не збережено", vm.LocationText);
    }

    private MainViewModel CreateViewModel() => new(_service, _dialogs);

    private static CellViewModel CellAt(MainViewModel vm, string name) => vm.Cells.Single(cell => cell.Name == name);
}

using BoolSheet.Core.Model;
using BoolSheet.Core.Storage;

namespace BoolSheet.Core.Services;

/// <summary>
/// The single entry point the UI uses to work with the current spreadsheet:
/// creating, opening, saving and editing it. Keeps the UI free of model and storage details.
/// </summary>
/// <remarks>
/// Not thread-safe, like <see cref="Grid"/>: call it from the UI thread only. Storage work may run
/// in the background, but the awaits here have no <c>ConfigureAwait(false)</c>, so the new grid is
/// always assigned back on the calling (UI) thread.
/// </remarks>
public sealed class SpreadsheetService
{
    public const int DefaultRows = 10;
    public const int DefaultColumns = 10;

    private readonly IFileManager _localFileManager;
    private readonly ICloudFileManager? _cloudFileManager;

    /// <param name="localFileManager">Stores files on the local disk.</param>
    /// <param name="cloudFileManager">Stores files in the cloud; null if the cloud is not available.</param>
    public SpreadsheetService(IFileManager localFileManager, ICloudFileManager? cloudFileManager = null)
    {
        _localFileManager = localFileManager;
        _cloudFileManager = cloudFileManager;
        Grid = new Grid(DefaultRows, DefaultColumns);
    }

    /// <summary>The spreadsheet currently being edited.</summary>
    public Grid Grid { get; private set; }

    public void CreateSpreadsheet(int rows, int columns) => Grid = new Grid(rows, columns);

    // ---------- Local files ----------

    public Task OpenSpreadsheetAsync(string path, CancellationToken cancellationToken = default)
        => OpenSpreadsheetAsync(DocumentLocation.Local(path), cancellationToken);

    public Task SaveSpreadsheetAsync(string path, CancellationToken cancellationToken = default)
        => SaveSpreadsheetAsync(DocumentLocation.Local(path), cancellationToken);

    // ---------- Any storage ----------

    /// <summary>Replaces the current spreadsheet only if the file was loaded successfully.</summary>
    public async Task OpenSpreadsheetAsync(DocumentLocation location, CancellationToken cancellationToken = default)
        => Grid = await ManagerFor(location.Kind).LoadAsync(location.Location, cancellationToken);

    public Task SaveSpreadsheetAsync(DocumentLocation location, CancellationToken cancellationToken = default)
        => ManagerFor(location.Kind).SaveAsync(Grid, location.Location, cancellationToken);

    /// <summary>The current spreadsheet as file content, for "Save as" dialogs that write the file themselves.</summary>
    public string Export() => SpreadsheetSerializer.Serialize(Grid);

    // ---------- Cloud ----------

    public async Task<IReadOnlyList<CloudFile>> ListCloudFilesAsync(CancellationToken cancellationToken = default)
        => await RequireCloud().ListAsync(cancellationToken);

    /// <summary>Saves the current spreadsheet to the cloud as a new file.</summary>
    public async Task<DocumentLocation> SaveToCloudAsNewAsync(string name, CancellationToken cancellationToken = default)
    {
        CloudFile file = await RequireCloud().CreateAsync(Grid, name, cancellationToken);
        return DocumentLocation.Cloud(file);
    }

    public Task SignOutFromCloudAsync(CancellationToken cancellationToken = default)
        => RequireCloud().SignOutAsync(cancellationToken);

    // ---------- Editing ----------

    public void EditCell(string address, string expression) => Grid.SetCellExpression(address, expression);

    public void ClearCell(string address) => Grid.ClearCell(address);

    public void AddRow() => Grid.AddRow();

    public void AddColumn() => Grid.AddColumn();

    public void DeleteRow(int row) => Grid.DeleteRow(row);

    public void DeleteColumn(int column) => Grid.DeleteColumn(column);

    private IFileManager ManagerFor(StorageKind kind) => kind switch
    {
        StorageKind.Local => _localFileManager,
        StorageKind.Cloud => RequireCloud(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    private ICloudFileManager RequireCloud()
        => _cloudFileManager ?? throw new InvalidOperationException("Хмарне сховище не налаштовано.");
}

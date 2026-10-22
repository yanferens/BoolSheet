using BoolSheet.Core.Services;
using BoolSheet.Core.Storage;
using Xunit;

namespace BoolSheet.Tests;

/// <summary>An in-memory stand-in for Google Drive, so the cloud logic can be tested without a network.</summary>
internal sealed class FakeCloudStorage : ICloudStorage
{
    private readonly Dictionary<string, (string Name, string Content)> _files = new();
    private int _nextId = 1;

    public bool SignedOut { get; private set; }

    public Task<IReadOnlyList<CloudFile>> ListAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<CloudFile>>(
            _files.Select(pair => new CloudFile(pair.Key, pair.Value.Name)).ToList());

    public Task<string> DownloadAsync(string fileId, CancellationToken cancellationToken = default)
        => _files.TryGetValue(fileId, out var file)
            ? Task.FromResult(file.Content)
            : throw new CloudStorageException("Файл не знайдено.");

    public Task<CloudFile> CreateAsync(string name, string content, CancellationToken cancellationToken = default)
    {
        string id = $"file-{_nextId++}";
        _files[id] = (name, content);
        return Task.FromResult(new CloudFile(id, name));
    }

    public Task UpdateAsync(string fileId, string content, CancellationToken cancellationToken = default)
    {
        if (!_files.TryGetValue(fileId, out var file))
        {
            throw new CloudStorageException("Файл не знайдено.");
        }

        _files[fileId] = (file.Name, content);
        return Task.CompletedTask;
    }

    public Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        SignedOut = true;
        return Task.CompletedTask;
    }

    public void PutRaw(string id, string name, string content) => _files[id] = (name, content);
}

public class CloudStorageTests
{
    private readonly FakeCloudStorage _storage = new();
    private readonly SpreadsheetService _service;

    public CloudStorageTests()
    {
        _service = new SpreadsheetService(new LocalFileManager(), new CloudFileManager(_storage));
    }

    [Fact]
    public async Task SaveToCloudAsNew_ThenOpen_RestoresSpreadsheet()
    {
        _service.EditCell("B2", "10 mod 3 = 1");

        DocumentLocation location = await _service.SaveToCloudAsNewAsync("lab.boolsheet");
        _service.CreateSpreadsheet(rows: 1, columns: 1);
        await _service.OpenSpreadsheetAsync(location);

        Assert.Equal(StorageKind.Cloud, location.Kind);
        Assert.Equal("lab.boolsheet", location.DisplayName);
        Assert.Equal(SpreadsheetService.DefaultRows, _service.Grid.RowsCount);
        Assert.Equal("10 mod 3 = 1", _service.Grid.GetCell("B2")?.Expression);
    }

    [Fact]
    public async Task SaveToExistingCloudFile_ReplacesContentWithoutCreatingAnotherFile()
    {
        _service.EditCell("A1", "1");
        DocumentLocation location = await _service.SaveToCloudAsNewAsync("lab.boolsheet");

        _service.EditCell("A1", "2");
        await _service.SaveSpreadsheetAsync(location);
        _service.CreateSpreadsheet(rows: 1, columns: 1);
        await _service.OpenSpreadsheetAsync(location);

        Assert.Equal("2", _service.Grid.GetCell("A1")?.Expression);
        Assert.Single(await _service.ListCloudFilesAsync());
    }

    [Fact]
    public async Task ListCloudFiles_ReturnsSavedFiles()
    {
        await _service.SaveToCloudAsNewAsync("a.boolsheet");
        await _service.SaveToCloudAsNewAsync("b.boolsheet");

        IReadOnlyList<CloudFile> files = await _service.ListCloudFilesAsync();

        Assert.Equal(new[] { "a.boolsheet", "b.boolsheet" }, files.Select(file => file.Name));
    }

    [Fact]
    public async Task OpenCloudFileWithInvalidContent_ThrowsAndKeepsCurrentSpreadsheet()
    {
        _service.EditCell("A1", "7");
        _storage.PutRaw("bad", "bad.boolsheet", "this is not a spreadsheet");

        await Assert.ThrowsAsync<InvalidDataException>(
            () => _service.OpenSpreadsheetAsync(new DocumentLocation(StorageKind.Cloud, "bad", "bad.boolsheet")));

        Assert.Equal("7", _service.Grid.GetCell("A1")?.Expression);
    }

    [Fact]
    public async Task OpenMissingCloudFile_ThrowsCloudStorageException()
    {
        var missing = new DocumentLocation(StorageKind.Cloud, "nope", "nope.boolsheet");

        // CloudStorageException is an IOException, so the UI handles it like any other file error.
        IOException error = await Assert.ThrowsAnyAsync<IOException>(() => _service.OpenSpreadsheetAsync(missing));

        Assert.IsType<CloudStorageException>(error);
    }

    [Fact]
    public async Task SignOut_IsForwardedToStorage()
    {
        await _service.SignOutFromCloudAsync();

        Assert.True(_storage.SignedOut);
    }

    [Fact]
    public async Task ServiceWithoutCloud_ThrowsInvalidOperationException()
    {
        var localOnly = new SpreadsheetService(new LocalFileManager());

        await Assert.ThrowsAsync<InvalidOperationException>(() => localOnly.ListCloudFilesAsync());
    }

    [Fact]
    public void DocumentLocation_Local_UsesFileNameAsDisplayName()
    {
        DocumentLocation location = DocumentLocation.Local(Path.Combine("folder", "lab.boolsheet"));

        Assert.Equal(StorageKind.Local, location.Kind);
        Assert.Equal("lab.boolsheet", location.DisplayName);
    }
}

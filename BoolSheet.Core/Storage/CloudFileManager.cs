using BoolSheet.Core.Model;

namespace BoolSheet.Core.Storage;

/// <summary>
/// Stores spreadsheets in a cloud using the same JSON format as <see cref="LocalFileManager"/>.
/// Translates between spreadsheets and the text that <see cref="ICloudStorage"/> moves around.
/// </summary>
public sealed class CloudFileManager : ICloudFileManager
{
    private readonly ICloudStorage _storage;

    public CloudFileManager(ICloudStorage storage) => _storage = storage;

    public Task<IReadOnlyList<CloudFile>> ListAsync(CancellationToken cancellationToken = default)
        => _storage.ListAsync(cancellationToken);

    public async Task<Grid> LoadAsync(string location, CancellationToken cancellationToken = default)
    {
        string json = await _storage.DownloadAsync(location, cancellationToken);
        return SpreadsheetSerializer.Deserialize(json);
    }

    public Task SaveAsync(Grid grid, string location, CancellationToken cancellationToken = default)
        => _storage.UpdateAsync(location, SpreadsheetSerializer.Serialize(grid), cancellationToken);

    public Task<CloudFile> CreateAsync(Grid grid, string name, CancellationToken cancellationToken = default)
        => _storage.CreateAsync(name, SpreadsheetSerializer.Serialize(grid), cancellationToken);

    public Task SignOutAsync(CancellationToken cancellationToken = default)
        => _storage.SignOutAsync(cancellationToken);
}

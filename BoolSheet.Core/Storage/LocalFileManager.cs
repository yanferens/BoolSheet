using BoolSheet.Core.Model;

namespace BoolSheet.Core.Storage;

/// <summary>Stores spreadsheets as JSON files on the local disk. <c>location</c> is a file path.</summary>
public sealed class LocalFileManager : IFileManager
{
    /// <summary>
    /// Writes a temporary file next to the target and then replaces the target with it,
    /// so a crash or power loss in the middle of saving never leaves a half-written file.
    /// </summary>
    public async Task SaveAsync(Grid grid, string location, CancellationToken cancellationToken = default)
    {
        string content = SpreadsheetSerializer.Serialize(grid);
        string temporary = $"{location}.{Guid.NewGuid():N}.tmp";

        try
        {
            await File.WriteAllTextAsync(temporary, content, cancellationToken);
            File.Move(temporary, location, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    public async Task<Grid> LoadAsync(string location, CancellationToken cancellationToken = default)
    {
        string json = await File.ReadAllTextAsync(location, cancellationToken);
        return SpreadsheetSerializer.Deserialize(json);
    }
}

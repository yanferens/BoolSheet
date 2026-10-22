using BoolSheet.Core.Model;

namespace BoolSheet.Core.Storage;

/// <summary>
/// Saves and loads a spreadsheet. The meaning of <c>location</c> depends on the implementation:
/// a file path for <see cref="LocalFileManager"/>, a file id for a cloud storage manager.
/// </summary>
public interface IFileManager
{
    Task SaveAsync(Grid grid, string location, CancellationToken cancellationToken = default);

    /// <exception cref="InvalidDataException">The file content is not a valid spreadsheet.</exception>
    Task<Grid> LoadAsync(string location, CancellationToken cancellationToken = default);
}

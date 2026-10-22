using BoolSheet.Core.Model;

namespace BoolSheet.Core.Storage;

/// <summary>
/// A <see cref="IFileManager"/> for a cloud storage. <c>location</c> is the file id.
/// Adds what a cloud needs beyond load and save: browsing files and creating new ones.
/// </summary>
public interface ICloudFileManager : IFileManager
{
    Task<IReadOnlyList<CloudFile>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Saves the spreadsheet as a new file and returns it.</summary>
    Task<CloudFile> CreateAsync(Grid grid, string name, CancellationToken cancellationToken = default);

    Task SignOutAsync(CancellationToken cancellationToken = default);
}

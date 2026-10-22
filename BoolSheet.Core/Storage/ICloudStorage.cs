namespace BoolSheet.Core.Storage;

/// <summary>
/// The raw operations of a cloud storage provider, working with plain text.
/// Implemented once per provider (see <c>GoogleDriveStorage</c>); contains no spreadsheet logic,
/// so the provider can be replaced or faked in tests.
/// </summary>
/// <remarks>
/// Implementations must report every provider-specific failure as <see cref="CloudStorageException"/>
/// and let <see cref="OperationCanceledException"/> pass through.
/// </remarks>
public interface ICloudStorage
{
    /// <summary>Lists the spreadsheet files available to the application, newest first.</summary>
    Task<IReadOnlyList<CloudFile>> ListAsync(CancellationToken cancellationToken = default);

    Task<string> DownloadAsync(string fileId, CancellationToken cancellationToken = default);

    /// <summary>Creates a new file and returns it (with its new id).</summary>
    Task<CloudFile> CreateAsync(string name, string content, CancellationToken cancellationToken = default);

    /// <summary>Replaces the content of an existing file.</summary>
    Task UpdateAsync(string fileId, string content, CancellationToken cancellationToken = default);

    /// <summary>Forgets the signed-in account, so the next operation asks to sign in again.</summary>
    Task SignOutAsync(CancellationToken cancellationToken = default);
}

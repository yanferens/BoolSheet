namespace BoolSheet.Core.Storage;

/// <summary>A spreadsheet file stored in a cloud storage (for example on Google Drive).</summary>
/// <param name="Id">The storage's own identifier of the file; this is the <c>location</c> used by <see cref="IFileManager"/>.</param>
/// <param name="Name">The file name shown to the user.</param>
/// <param name="ModifiedAt">When the file was last modified, if the storage reports it.</param>
public sealed record CloudFile(string Id, string Name, DateTimeOffset? ModifiedAt = null);

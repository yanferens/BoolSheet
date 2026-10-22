namespace BoolSheet.Core.Storage;

/// <summary>Where a spreadsheet is stored: on the local disk or in the cloud.</summary>
public enum StorageKind
{
    Local,
    Cloud,
}

/// <summary>Identifies the file the current spreadsheet was opened from or saved to.</summary>
/// <param name="Kind">Which storage holds the file.</param>
/// <param name="Location">A file path for <see cref="StorageKind.Local"/>, a file id for <see cref="StorageKind.Cloud"/>.</param>
/// <param name="DisplayName">The file name shown to the user.</param>
public sealed record DocumentLocation(StorageKind Kind, string Location, string DisplayName)
{
    public static DocumentLocation Local(string path)
        => new(StorageKind.Local, path, Path.GetFileName(path));

    public static DocumentLocation Cloud(CloudFile file)
        => new(StorageKind.Cloud, file.Id, file.Name);
}

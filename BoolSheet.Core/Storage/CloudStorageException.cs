namespace BoolSheet.Core.Storage;

/// <summary>
/// A cloud storage operation failed (no network, access denied, file not found, sign-in failed...).
/// Derives from <see cref="IOException"/> so callers can treat it like any other file error;
/// the message is written for the user.
/// </summary>
public sealed class CloudStorageException : IOException
{
    public CloudStorageException(string message)
        : base(message)
    {
    }

    public CloudStorageException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

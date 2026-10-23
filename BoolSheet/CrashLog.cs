namespace BoolSheet;

/// <summary>
/// Writes unexpected exceptions to a text file, so the cause of a failure can be found later
/// even when it happened without a debugger.
/// </summary>
public static class CrashLog
{
    public static string FilePath { get; } = Path.Combine(FileSystem.AppDataDirectory, "crash.log");

    public static void Write(Exception exception)
    {
        try
        {
            File.AppendAllText(
                FilePath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Logging must never become a new reason to crash.
        }
    }
}

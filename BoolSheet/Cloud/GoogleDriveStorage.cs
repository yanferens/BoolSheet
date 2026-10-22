using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using BoolSheet.Core.Storage;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Download;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.Util.Store;
using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace BoolSheet.Cloud;

/// <summary>
/// <see cref="ICloudStorage"/> on Google Drive. Signs the user in with OAuth 2.0 in the system browser
/// (a loopback redirect, the flow Google recommends for desktop apps) and remembers the tokens on disk.
/// </summary>
/// <remarks>
/// <para>
/// The full <c>drive</c> scope is requested so that spreadsheets uploaded to Drive by hand are listed too.
/// Google treats it as a restricted scope: until the app is verified, only the test users added in
/// Google Cloud Console can sign in, and they see an "unverified app" warning.
/// </para>
/// <para>
/// The OAuth client id comes from the <c>client_secret.json</c> app asset (see <c>Resources/Raw</c>);
/// the file is downloaded from Google Cloud Console and is not committed to git.
/// </para>
/// </remarks>
public sealed class GoogleDriveStorage : ICloudStorage
{
    private const string ClientSecretAsset = "client_secret.json";
    private const string ApplicationName = "BoolSheet";
    private const string MimeType = "application/json";
    private const string UserId = "user";
    private const int MaxPages = 10;

    private static readonly TimeSpan SignInTimeout = TimeSpan.FromMinutes(3);

    private readonly SemaphoreSlim _serviceLock = new(1, 1);
    private readonly string _tokenDirectory = Path.Combine(FileSystem.AppDataDirectory, "GoogleAuth");

    private DriveService? _service;

    public async Task<IReadOnlyList<CloudFile>> ListAsync(CancellationToken cancellationToken = default)
        => await RunAsync(async drive =>
        {
            var files = new List<CloudFile>();
            string? pageToken = null;

            for (int page = 0; page < MaxPages; page++)
            {
                FilesResource.ListRequest request = drive.Files.List();
                // Drive's "name contains" matches whole word prefixes only and uploaded files have arbitrary
                // MIME types, so list everything (newest first) and filter by extension below.
                request.Q = "trashed = false and mimeType != 'application/vnd.google-apps.folder'";
                request.Fields = "nextPageToken, files(id, name, modifiedTime)";
                request.OrderBy = "modifiedTime desc";
                request.PageSize = 100;
                request.PageToken = pageToken;

                Google.Apis.Drive.v3.Data.FileList result = await request.ExecuteAsync(cancellationToken);

                files.AddRange((result.Files ?? new List<DriveFile>())
                    .Where(file => file.Name.EndsWith(SpreadsheetSerializer.FileExtension, StringComparison.OrdinalIgnoreCase))
                    .Select(ToCloudFile));

                pageToken = result.NextPageToken;
                if (pageToken is null)
                {
                    break;
                }
            }

            return (IReadOnlyList<CloudFile>)files;
        }, cancellationToken);

    public async Task<string> DownloadAsync(string fileId, CancellationToken cancellationToken = default)
        => await RunAsync(async drive =>
        {
            using var buffer = new MemoryStream();
            IDownloadProgress progress = await drive.Files.Get(fileId).DownloadAsync(buffer, cancellationToken);
            if (progress.Status != DownloadStatus.Completed)
            {
                throw progress.Exception ?? new IOException("Завантаження з Google Drive не завершилося.");
            }

            buffer.Position = 0;
            using var reader = new StreamReader(buffer, Encoding.UTF8);
            return await reader.ReadToEndAsync(cancellationToken);
        }, cancellationToken);

    public async Task<CloudFile> CreateAsync(string name, string content, CancellationToken cancellationToken = default)
        => await RunAsync(async drive =>
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
            FilesResource.CreateMediaUpload request = drive.Files.Create(
                new DriveFile { Name = name, MimeType = MimeType }, stream, MimeType);
            request.Fields = "id, name, modifiedTime";

            EnsureCompleted(await request.UploadAsync(cancellationToken));
            return ToCloudFile(request.ResponseBody);
        }, cancellationToken);

    public async Task UpdateAsync(string fileId, string content, CancellationToken cancellationToken = default)
        => await RunAsync(async drive =>
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
            FilesResource.UpdateMediaUpload request = drive.Files.Update(new DriveFile(), fileId, stream, MimeType);
            request.Fields = "id";

            EnsureCompleted(await request.UploadAsync(cancellationToken));
            return true;
        }, cancellationToken);

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        await _serviceLock.WaitAsync(cancellationToken);
        try
        {
            ForgetAccount();
        }
        finally
        {
            _serviceLock.Release();
        }
    }

    // ---------- Sign-in ----------

    /// <summary>Returns the Drive client, signing the user in on first use (a browser window opens).</summary>
    private async Task<DriveService> GetServiceAsync(CancellationToken cancellationToken)
    {
        await _serviceLock.WaitAsync(cancellationToken);
        try
        {
            if (_service is not null)
            {
                return _service;
            }

            ClientSecrets secrets = await ReadClientSecretsAsync();

            // Nobody may be watching the browser: give up waiting for the sign-in after a while.
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(SignInTimeout);

            UserCredential credential;
            try
            {
                credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    secrets,
                    new[] { DriveService.Scope.Drive },
                    UserId,
                    timeout.Token,
                    new FileDataStore(_tokenDirectory, fullPath: true));
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new CloudStorageException("Вхід у Google акаунт не завершено вчасно. Спробуйте ще раз.");
            }

            _service = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            });
            return _service;
        }
        finally
        {
            _serviceLock.Release();
        }
    }

    private static async Task<ClientSecrets> ReadClientSecretsAsync()
    {
        try
        {
            await using Stream stream = await FileSystem.OpenAppPackageFileAsync(ClientSecretAsset);
            using JsonDocument json = await JsonDocument.ParseAsync(stream);

            // Google names the root "installed" for desktop clients and "web" for web clients.
            JsonElement root = json.RootElement;
            JsonElement client = root.TryGetProperty("installed", out JsonElement installed) ? installed : root.GetProperty("web");

            return new ClientSecrets
            {
                ClientId = client.GetProperty("client_id").GetString(),
                ClientSecret = client.GetProperty("client_secret").GetString(),
            };
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new CloudStorageException(
                "Не знайдено файл client_secret.json у Resources/Raw. Створіть OAuth-клієнт у Google Cloud Console й додайте файл до проєкту.",
                ex);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new CloudStorageException("Файл client_secret.json має неправильний формат (потрібен клієнт типу «Desktop app»).", ex);
        }
    }

    /// <summary>Drops the Drive client and deletes the saved tokens. Must be called under the lock.</summary>
    private void ForgetAccount()
    {
        _service?.Dispose();
        _service = null;

        try
        {
            if (Directory.Exists(_tokenDirectory))
            {
                Directory.Delete(_tokenDirectory, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new CloudStorageException("Не вдалося видалити збережені дані входу.", ex);
        }
    }

    // ---------- Requests and error translation ----------

    private async Task<T> RunAsync<T>(Func<DriveService, Task<T>> operation, CancellationToken cancellationToken)
    {
        DriveService drive = await GetServiceAsync(cancellationToken);

        try
        {
            return await operation(drive);
        }
        catch (GoogleApiException ex)
        {
            throw new CloudStorageException(DescribeApiError(ex), ex);
        }
        catch (TokenResponseException ex)
        {
            // The saved sign-in was revoked or expired: forget it so that the next attempt signs in again.
            await SignOutAsync(CancellationToken.None);
            throw new CloudStorageException("Сеанс входу в Google завершився. Повторіть дію, щоб увійти знову.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new CloudStorageException("Немає зв'язку з Google Drive. Перевірте підключення до інтернету.", ex);
        }
    }

    private static void EnsureCompleted(IUploadProgress progress)
    {
        if (progress.Status != UploadStatus.Completed)
        {
            throw progress.Exception ?? new IOException("Збереження на Google Drive не завершилося.");
        }
    }

    private static string DescribeApiError(GoogleApiException ex) => ex.HttpStatusCode switch
    {
        HttpStatusCode.NotFound => "Файл не знайдено на Google Drive (можливо, його видалено).",
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
            "Google Drive відмовив у доступі. Спробуйте вийти з акаунта (меню «Файл») і увійти знову.",
        _ => $"Помилка Google Drive: {ex.Message}",
    };

    private static CloudFile ToCloudFile(DriveFile file)
        => new(file.Id, file.Name, file.ModifiedTimeDateTimeOffset);
}

using System.Net.Http;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;

namespace DrekoAccSwitcher.Services;

public sealed record GitHubRelease(string TagName, Uri? InstallerUrl, string? InstallerDigest);

public sealed class GitHubReleaseService
{
    private const long MaximumInstallerSize = 200 * 1024 * 1024;
    private const string RepositoryDownloadPrefix =
        "/mohammedateyah/dreko-acc-switcher/releases/download/";
    private static readonly HttpClient ApiClient = CreateClient(TimeSpan.FromSeconds(10));
    private static readonly HttpClient DownloadClient = CreateClient(Timeout.InfiniteTimeSpan);
    private static readonly Uri LatestReleaseUri = new(
        "https://api.github.com/repos/mohammedateyah/dreko-acc-switcher/releases/latest");

    public async Task<GitHubRelease?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        using var response = await ApiClient.GetAsync(
            LatestReleaseUri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(
            stream,
            cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("tag_name", out var tagNameElement) ||
            tagNameElement.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(tagNameElement.GetString()))
            throw new InvalidDataException("The latest GitHub release does not contain a version tag.");

        var tagName = tagNameElement.GetString()!;
        var installerName = $"DrekoAccSwitcher-Setup-{tagName.TrimStart('v', 'V')}.exe";
        Uri? installerUrl = null;
        string? installerDigest = null;

        if (document.RootElement.TryGetProperty("assets", out var assets) &&
            assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assets.EnumerateArray())
            {
                if (!asset.TryGetProperty("name", out var nameElement) ||
                    !string.Equals(nameElement.GetString(), installerName, StringComparison.Ordinal))
                    continue;

                if (asset.TryGetProperty("browser_download_url", out var urlElement) &&
                    Uri.TryCreate(urlElement.GetString(), UriKind.Absolute, out var parsedUrl))
                    installerUrl = parsedUrl;

                if (asset.TryGetProperty("digest", out var digestElement) &&
                    digestElement.ValueKind == JsonValueKind.String)
                    installerDigest = digestElement.GetString();

                break;
            }
        }

        return new GitHubRelease(tagName, installerUrl, installerDigest);
    }

    public async Task DownloadInstallerAsync(
        Uri downloadUrl,
        string expectedTagName,
        string expectedInstallerName,
        string expectedDigest,
        string destinationPath,
        IProgress<double> progress,
        CancellationToken cancellationToken = default)
    {
        ValidateDownloadUrl(downloadUrl, expectedTagName, expectedInstallerName);
        var expectedHash = ParseSha256Digest(expectedDigest);

        using var response = await DownloadClient.GetAsync(
            downloadUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength is > MaximumInstallerSize)
            throw new InvalidDataException("The GitHub installer exceeds the permitted download size.");

        var directory = Path.GetDirectoryName(destinationPath)
                        ?? throw new InvalidDataException("The installer destination has no directory.");
        Directory.CreateDirectory(directory);

        try
        {
            await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var output = new FileStream(
                             destinationPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 81920,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var buffer = new byte[81920];
                long totalBytes = 0;
                int bytesRead;
                while ((bytesRead = await input.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    totalBytes += bytesRead;
                    if (totalBytes > MaximumInstallerSize)
                        throw new InvalidDataException("The GitHub installer exceeds the permitted download size.");

                    await output.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    if (response.Content.Headers.ContentLength is long expectedLength and > 0)
                        progress.Report((double)totalBytes / expectedLength);
                }

                if (totalBytes == 0)
                    throw new InvalidDataException("The GitHub installer download was empty.");
            }

            await using var downloadedFile = new FileStream(
                destinationPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var actualHash = await SHA256.HashDataAsync(downloadedFile, cancellationToken);
            if (!CryptographicOperations.FixedTimeEquals(expectedHash, actualHash))
                throw new InvalidDataException("The downloaded installer does not match GitHub's SHA-256 digest.");

            downloadedFile.Position = 0;
            var signature = new byte[2];
            if (await downloadedFile.ReadAsync(signature, cancellationToken) != signature.Length ||
                signature[0] != (byte)'M' ||
                signature[1] != (byte)'Z')
                throw new InvalidDataException("The downloaded installer is not a valid Windows executable.");

            progress.Report(1);
        }
        catch
        {
            if (File.Exists(destinationPath))
            {
                try
                {
                    File.Delete(destinationPath);
                }
                catch (IOException cleanupException)
                {
                    Log.Write($"Could not remove incomplete installer '{destinationPath}': {cleanupException}");
                }
                catch (UnauthorizedAccessException cleanupException)
                {
                    Log.Write($"Could not remove incomplete installer '{destinationPath}': {cleanupException}");
                }
            }
            throw;
        }
    }

    private static void ValidateDownloadUrl(
        Uri downloadUrl,
        string expectedTagName,
        string expectedInstallerName)
    {
        var expectedPath = $"{RepositoryDownloadPrefix}{Uri.EscapeDataString(expectedTagName)}/" +
                           Uri.EscapeDataString(expectedInstallerName);
        if (downloadUrl.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(downloadUrl.Host, "github.com", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(downloadUrl.AbsolutePath, expectedPath, StringComparison.Ordinal) ||
            !string.IsNullOrEmpty(downloadUrl.Query))
            throw new InvalidDataException("The GitHub release contains an untrusted installer download URL.");
    }

    private static byte[] ParseSha256Digest(string digest)
    {
        const string prefix = "sha256:";
        if (!digest.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            digest.Length != prefix.Length + 64)
            throw new InvalidDataException("The GitHub installer does not have a valid SHA-256 digest.");

        try
        {
            return Convert.FromHexString(digest[prefix.Length..]);
        }
        catch (FormatException ex)
        {
            throw new InvalidDataException("The GitHub installer SHA-256 digest is malformed.", ex);
        }
    }

    private static HttpClient CreateClient(TimeSpan timeout)
    {
        var client = new HttpClient { Timeout = timeout };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("DrekoAccSwitcher", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }
}

using System.Net.Http;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace DrekoAccSwitcher.Services;

public sealed record GitHubRelease(string TagName);

public sealed class GitHubReleaseService
{
    private static readonly HttpClient Client = CreateClient();
    private static readonly Uri LatestReleaseUri = new(
        "https://api.github.com/repos/mohammedateyah/dreko-acc-switcher/releases/latest");

    public async Task<GitHubRelease?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        using var response = await Client.GetAsync(
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

        return new GitHubRelease(tagNameElement.GetString()!);
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("DrekoAccSwitcher", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }
}

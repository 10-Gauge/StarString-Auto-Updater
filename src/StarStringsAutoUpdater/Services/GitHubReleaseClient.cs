using System.Net.Http.Headers;
using System.Text.Json;
using StarStringsAutoUpdater.Models;

namespace StarStringsAutoUpdater.Services;

public sealed class GitHubReleaseClient : IDisposable
{
    // The StarStrings release workflow deletes and recreates the "latest" tag/release
    // on every push to master, so the tag name never changes across versions — only
    // the release's published_at, name, and asset content do. See releases/tags/latest.
    private const string ReleaseApiUrl = "https://api.github.com/repos/MrKraken/StarStrings/releases/tags/latest";
    private const string ExpectedAssetName = "StarStrings-LIVE.zip";

    private readonly HttpClient _http;

    public GitHubReleaseClient()
    {
        _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30),
        };
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("StarStringsAutoUpdater", "1.0"));
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        _http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    }

    public async Task<GitHubRelease> GetLatestReleaseAsync(CancellationToken ct)
    {
        using var response = await _http.GetAsync(ReleaseApiUrl, ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw new InvalidOperationException(
                $"GitHub API returned {(int)response.StatusCode} {response.ReasonPhrase} for release lookup. {Truncate(body)}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var release = await JsonSerializer.DeserializeAsync<GitHubRelease>(stream, cancellationToken: ct).ConfigureAwait(false);

        if (release is null)
        {
            throw new InvalidOperationException("GitHub API returned an empty/unparseable release payload.");
        }

        return release;
    }

    /// <summary>Finds the StarStrings-LIVE.zip asset on a release, falling back to
    /// any single .zip asset if the expected name isn't present (defensive against
    /// the upstream workflow changing its asset naming).</summary>
    public static GitHubReleaseAsset? FindLiveZipAsset(GitHubRelease release)
    {
        var exact = release.Assets.FirstOrDefault(a =>
            string.Equals(a.Name, ExpectedAssetName, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }

        var zips = release.Assets.Where(a => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)).ToList();
        return zips.Count == 1 ? zips[0] : null;
    }

    public async Task DownloadFileAsync(string url, string destinationPath, CancellationToken ct)
    {
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

        await using var httpStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await httpStream.CopyToAsync(fileStream, ct).ConfigureAwait(false);
    }

    private static string Truncate(string s, int max = 300) => s.Length <= max ? s : s[..max] + "...";

    public void Dispose() => _http.Dispose();
}

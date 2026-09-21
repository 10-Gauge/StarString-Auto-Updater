using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using StarStringsAutoUpdater.Models;

namespace StarStringsAutoUpdater.Services;

public sealed class GitHubReleaseClient : IDisposable
{
    // The StarStrings release workflow deletes and recreates the "latest" tag/release
    // on every push to master, so the tag name never changes across versions — only
    // the release's published_at, name, and asset content do.
    public const string StarStringsLatestReleaseUrl = "https://api.github.com/repos/MrKraken/StarStrings/releases/tags/latest";
    private const string StarStringsExpectedAssetName = "StarStrings-LIVE.zip";

    // Neither the release metadata nor the zip itself names the Star Citizen PU version
    // StarStrings targets - the repo's README is the only place it's stated, as CIG's own
    // build-id format (e.g. "sc-alpha-4.10.1_live_12660092"). This is a plain CDN fetch
    // (not the api.github.com endpoint), so it doesn't count against the API rate limit.
    private const string StarStringsReadmeUrl = "https://raw.githubusercontent.com/MrKraken/StarStrings/master/readme.md";
    private static readonly Regex ScVersionPattern = new(
        @"sc-alpha-(?<version>\d+(?:\.\d+){1,3})_(?:live|ptu)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Our own repo uses ordinary semver tags, so the normal "latest release" endpoint
    // (most recent non-draft, non-prerelease release) works as expected here.
    public const string AppLatestReleaseUrl = "https://api.github.com/repos/10-Gauge/StarStrings-Auto-Updater/releases/latest";

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

    public async Task<GitHubRelease> GetLatestReleaseAsync(string releaseApiUrl, CancellationToken ct)
    {
        using var response = await _http.GetAsync(releaseApiUrl, ct).ConfigureAwait(false);

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
    public static GitHubReleaseAsset? FindStarStringsLiveZipAsset(GitHubRelease release)
    {
        var exact = release.Assets.FirstOrDefault(a =>
            string.Equals(a.Name, StarStringsExpectedAssetName, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }

        var zips = release.Assets.Where(a => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)).ToList();
        return zips.Count == 1 ? zips[0] : null;
    }

    /// <summary>Finds this app's own release exe. The filename is version-qualified
    /// (e.g. StarStringsAutoUpdater-v1.2.0.exe), so this just looks for the release's
    /// single .exe asset rather than matching an exact name.</summary>
    public static GitHubReleaseAsset? FindAppExeAsset(GitHubRelease release)
    {
        var exes = release.Assets.Where(a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)).ToList();
        return exes.Count == 1 ? exes[0] : null;
    }

    /// <summary>Best-effort lookup of the Star Citizen PU version (e.g. "4.10.1") the
    /// current StarStrings README says it targets. Returns null on any failure or if the
    /// expected build-id pattern isn't found, rather than failing the caller's flow.</summary>
    public async Task<string?> TryGetStarStringsTargetScVersionAsync(CancellationToken ct)
    {
        try
        {
            using var response = await _http.GetAsync(StarStringsReadmeUrl, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var match = ScVersionPattern.Match(text);
            return match.Success ? match.Groups["version"].Value : null;
        }
        catch (Exception ex)
        {
            Logger.Warning($"Couldn't determine the StarStrings target Star Citizen version: {ex.Message}");
            return null;
        }
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

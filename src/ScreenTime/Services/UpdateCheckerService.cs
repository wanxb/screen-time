using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;

namespace ScreenTime.Services;

public sealed class UpdateCheckerService
{
    private const string LatestReleaseUrl = "https://api.github.com/repos/wanxb/screen-time/releases/latest";
    private const string LatestReleasePageUrl = "https://github.com/wanxb/screen-time/releases/latest";
    private const string LatestDownloadUrl = "https://github.com/wanxb/screen-time/releases/latest/download/ScreenTime-{0}-win-x64.zip";
    private static readonly Uri LatestReleaseUri = new(LatestReleaseUrl);
    private static readonly Uri LatestReleasePageUri = new(LatestReleasePageUrl);

    private readonly HttpClient _httpClient;

    public UpdateCheckerService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ScreenTime.UpdateChecker");
        }
    }

    public static Version CurrentVersion => GetCurrentVersion();

    public async Task<UpdateCheckResult> CheckLatestAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await CheckLatestFromApiAsync(cancellationToken);
        }
        catch
        {
            return await CheckLatestFromReleaseRedirectAsync(cancellationToken);
        }
    }

    private async Task<UpdateCheckResult> CheckLatestFromApiAsync(CancellationToken cancellationToken)
    {
        var release = await _httpClient.GetFromJsonAsync<GitHubRelease>(LatestReleaseUri, cancellationToken);
        if (release is null || string.IsNullOrWhiteSpace(release.TagName))
        {
            throw new InvalidOperationException("无法读取最新版本信息。");
        }

        if (!TryParseVersion(release.TagName, out var latestVersion))
        {
            throw new InvalidOperationException($"无法识别最新版本号：{release.TagName}");
        }

        var downloadUrl = release.Assets
            .FirstOrDefault(asset => asset.Name.EndsWith("-win-x64.zip", StringComparison.OrdinalIgnoreCase))
            ?.BrowserDownloadUrl;

        var updateUrl = string.IsNullOrWhiteSpace(downloadUrl) ? release.HtmlUrl : downloadUrl;
        return new UpdateCheckResult(
            CurrentVersion,
            latestVersion,
            release.TagName,
            updateUrl,
            latestVersion > CurrentVersion);
    }

    private async Task<UpdateCheckResult> CheckLatestFromReleaseRedirectAsync(CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(LatestReleasePageUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var finalUri = response.RequestMessage?.RequestUri;
        var latestTagName = GetTagNameFromUri(finalUri)
            ?? throw new InvalidOperationException("无法从最新版本页面识别版本号。");

        if (!TryParseVersion(latestTagName, out var latestVersion))
        {
            throw new InvalidOperationException($"无法识别最新版本号：{latestTagName}");
        }

        var updateUrl = string.Format(LatestDownloadUrl, latestTagName);
        return new UpdateCheckResult(
            CurrentVersion,
            latestVersion,
            latestTagName,
            updateUrl,
            latestVersion > CurrentVersion);
    }

    public static bool TryParseVersion(string value, out Version version)
    {
        version = new Version(0, 0);
        var normalized = value.Trim().TrimStart('v', 'V');
        var suffixIndex = normalized.IndexOfAny(['-', '+']);
        if (suffixIndex >= 0)
        {
            normalized = normalized[..suffixIndex];
        }

        return Version.TryParse(normalized, out version!);
    }

    private static Version GetCurrentVersion()
    {
        var informationalVersion = typeof(UpdateCheckerService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion)
            && TryParseVersion(informationalVersion, out var parsed))
        {
            return parsed;
        }

        return typeof(UpdateCheckerService).Assembly.GetName().Version ?? new Version(0, 0);
    }

    private static string? GetTagNameFromUri(Uri? uri)
    {
        if (uri is null)
        {
            return null;
        }

        const string marker = "/releases/tag/";
        var path = uri.AbsolutePath;
        var markerIndex = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return null;
        }

        return Uri.UnescapeDataString(path[(markerIndex + marker.Length)..]).Trim('/');
    }

    private sealed record GitHubRelease(
        [property: JsonPropertyName("tag_name")] string TagName,
        [property: JsonPropertyName("html_url")] string HtmlUrl,
        [property: JsonPropertyName("assets")] GitHubAsset[] Assets);

    private sealed record GitHubAsset(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("browser_download_url")] string BrowserDownloadUrl);
}

public sealed record UpdateCheckResult(
    Version CurrentVersion,
    Version LatestVersion,
    string LatestTagName,
    string? UpdateUrl,
    bool HasUpdate);

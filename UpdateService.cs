using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;
using System.Windows;

namespace Ch34xProgrammer;

internal static class UpdateService
{
    private const string LatestReleaseUrl = "https://api.github.com/repos/mhqb365/CH34x-Programmer/releases/latest";

    public static Version CurrentVersion => ParseVersion(
        Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion) ?? new Version(1, 0, 0, 0);

    public static string DisplayCurrentVersion => FormatVersion(CurrentVersion);

    public static async Task<UpdateCheckResult> CheckLatestReleaseAsync()
    {
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("CH34xProgrammer");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

        using var response = await client.GetAsync(LatestReleaseUrl);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return UpdateCheckResult.NoRelease();
        }

        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync();
        var serializer = new DataContractJsonSerializer(typeof(GitHubRelease));
        var release = (GitHubRelease?)serializer.ReadObject(stream);
        var latestVersion = ParseVersion(release?.TagName ?? release?.Name);

        if (release is null || latestVersion is null)
        {
            return UpdateCheckResult.UnknownVersion(release);
        }

        return latestVersion > CurrentVersion
            ? UpdateCheckResult.UpdateAvailable(release, latestVersion)
            : UpdateCheckResult.UpToDate(release, latestVersion);
    }

    public static async Task<PreparedUpdate> DownloadAndPrepareUpdateAsync(
        GitHubRelease release,
        IProgress<UpdateProgressInfo> progress,
        CancellationToken cancellationToken)
    {
        var asset = FindUpdateAsset(release) ?? throw new InvalidOperationException("No ZIP update asset found");
        var tempRoot = Path.Combine(Path.GetTempPath(), $"ch34x-programmer-update-{Guid.NewGuid():N}");
        var zipPath = Path.Combine(tempRoot, asset.Name ?? "update.zip");
        var extractPath = Path.Combine(tempRoot, "extracted");

        Directory.CreateDirectory(tempRoot);
        Directory.CreateDirectory(extractPath);

        try
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("CH34xProgrammer");
            using var response = await client.GetAsync(asset.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var output = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
            {
                var buffer = new byte[8192];
                var totalRead = 0L;
                int read;
                while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    totalRead += read;
                    if (totalBytes > 0)
                    {
                        progress.Report(new UpdateProgressInfo(UpdateProgressState.Downloading, (int)(totalRead * 100 / totalBytes)));
                    }
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            progress.Report(new UpdateProgressInfo(UpdateProgressState.Extracting, 100));
            await Task.Run(() => ZipFile.ExtractToDirectory(zipPath, extractPath), cancellationToken);

            progress.Report(new UpdateProgressInfo(UpdateProgressState.Preparing, 100));
            return new PreparedUpdate(GetInstallSourceDirectory(extractPath), tempRoot);
        }
        catch
        {
            TryDeleteDirectory(tempRoot);
            throw;
        }
    }

    public static void InstallPreparedUpdate(PreparedUpdate update)
    {
        var appDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        var appExe = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName
            ?? throw new InvalidOperationException("Cannot locate application executable");
        var scriptPath = Path.Combine(update.TempRoot, "install-update.ps1");
        var script = $@"
$ErrorActionPreference = 'Stop'
$source = '{EscapePowerShellString(update.SourceDirectory)}'
$target = '{EscapePowerShellString(appDirectory)}'
$exe = '{EscapePowerShellString(appExe)}'
$temp = '{EscapePowerShellString(update.TempRoot)}'
$pidToWait = {Process.GetCurrentProcess().Id}
Wait-Process -Id $pidToWait -ErrorAction SilentlyContinue
Copy-Item -Path (Join-Path $source '*') -Destination $target -Recurse -Force
Start-Process -FilePath $exe -WorkingDirectory $target
Start-Sleep -Seconds 2
Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
";
        File.WriteAllText(scriptPath, script);

        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = appDirectory
        });

        Application.Current.Shutdown();
    }

    private static GitHubReleaseAsset? FindUpdateAsset(GitHubRelease release) =>
        release.Assets?.FirstOrDefault(asset =>
            !string.IsNullOrWhiteSpace(asset.DownloadUrl) &&
            asset.Name?.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) == true);

    private static string GetInstallSourceDirectory(string extractPath)
    {
        var rootFiles = Directory.GetFiles(extractPath);
        var rootDirectories = Directory.GetDirectories(extractPath);
        return rootFiles.Length == 0 && rootDirectories.Length == 1
            ? rootDirectories[0]
            : extractPath;
    }

    private static Version? ParseVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var match = Regex.Match(value, @"\d+(?:\.\d+){0,3}");
        return match.Success && Version.TryParse(match.Value, out var version)
            ? NormalizeVersion(version)
            : null;
    }

    private static Version NormalizeVersion(Version version) =>
        new(Math.Max(version.Major, 0), Math.Max(version.Minor, 0), Math.Max(version.Build, 0), Math.Max(version.Revision, 0));

    private static string FormatVersion(Version version) =>
        version.Revision > 0
            ? $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}"
            : $"{version.Major}.{version.Minor}.{version.Build}";

    private static string EscapePowerShellString(string value) => value.Replace("'", "''");

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch
        {
            // Best-effort temp cleanup.
        }
    }
}

internal sealed class UpdateCheckResult
{
    private UpdateCheckResult(UpdateCheckStatus status, GitHubRelease? release = null, Version? latestVersion = null)
    {
        Status = status;
        Release = release;
        LatestVersion = latestVersion;
    }

    public UpdateCheckStatus Status { get; }
    public GitHubRelease? Release { get; }
    public Version? LatestVersion { get; }
    public string DisplayLatestVersion => LatestVersion is null
        ? string.Empty
        : LatestVersion.Revision > 0
            ? $"{LatestVersion.Major}.{LatestVersion.Minor}.{LatestVersion.Build}.{LatestVersion.Revision}"
            : $"{LatestVersion.Major}.{LatestVersion.Minor}.{LatestVersion.Build}";

    public static UpdateCheckResult NoRelease() => new(UpdateCheckStatus.NoRelease);
    public static UpdateCheckResult UnknownVersion(GitHubRelease? release) => new(UpdateCheckStatus.UnknownVersion, release);
    public static UpdateCheckResult UpToDate(GitHubRelease release, Version latestVersion) => new(UpdateCheckStatus.UpToDate, release, latestVersion);
    public static UpdateCheckResult UpdateAvailable(GitHubRelease release, Version latestVersion) => new(UpdateCheckStatus.UpdateAvailable, release, latestVersion);
}

internal enum UpdateCheckStatus
{
    NoRelease,
    UnknownVersion,
    UpToDate,
    UpdateAvailable
}

[DataContract]
internal sealed class GitHubRelease
{
    [DataMember(Name = "name")]
    public string? Name { get; set; }

    [DataMember(Name = "tag_name")]
    public string? TagName { get; set; }

    [DataMember(Name = "html_url")]
    public string? HtmlUrl { get; set; }

    [DataMember(Name = "assets")]
    public GitHubReleaseAsset[]? Assets { get; set; }
}

[DataContract]
internal sealed class GitHubReleaseAsset
{
    [DataMember(Name = "name")]
    public string? Name { get; set; }

    [DataMember(Name = "browser_download_url")]
    public string? DownloadUrl { get; set; }
}

internal sealed record PreparedUpdate(string SourceDirectory, string TempRoot);

internal enum UpdateProgressState
{
    Downloading,
    Extracting,
    Preparing
}

internal sealed record UpdateProgressInfo(UpdateProgressState State, int Percentage);

namespace HS2VoiceReplace;

internal static partial class DependencyBootstrapper
{
    private static async Task EnsureUabeaClassDataAsync(string externalRoot, string dlRoot, Action<string> log, CancellationToken ct)
    {
        var classDataPath = Path.Combine(externalRoot, "uabea", "classdata.tpk");
        if (File.Exists(classDataPath) && new FileInfo(classDataPath).Length > 0)
        {
            log(L("log.uabeaClassdataExists"));
            return;
        }

        var overrideZipUrl = Environment.GetEnvironmentVariable("HS2VR_UABEA_ZIP_URL");
        var zipUrl = string.IsNullOrWhiteSpace(overrideZipUrl)
            ? await ResolveUabeaZipUrlAsync(ct)
            : overrideZipUrl.Trim();

        var zip = Path.Combine(dlRoot, "uabea.zip");
        await DownloadFileAsync(zipUrl, zip, log, ct);

        var temp = Path.Combine(dlRoot, "uabea_extract");
        if (Directory.Exists(temp))
            Directory.Delete(temp, true);
        Directory.CreateDirectory(temp);
        ExtractZip(zip, temp, stripSingleRoot: false);

        var found = Directory.GetFiles(temp, "classdata.tpk", SearchOption.AllDirectories)
            .FirstOrDefault();
        if (found == null)
            throw new InvalidOperationException(L("error.uabeaClassdataMissing"));

        Directory.CreateDirectory(Path.Combine(externalRoot, "uabea"));
        File.Copy(found, classDataPath, true);
        log(L("log.uabeaClassdataReady"));
    }

    private static async Task<string> ResolveUabeaZipUrlAsync(CancellationToken ct)
    {
        using var resp = await Http.GetAsync(UabeaLatestApiUrl, ct);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("assets", out var assets) || assets.ValueKind != System.Text.Json.JsonValueKind.Array)
            throw new InvalidOperationException(L("error.uabeaReleaseApiInvalid"));

        foreach (var a in assets.EnumerateArray())
        {
            var name = a.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "" : "";
            var url = a.TryGetProperty("browser_download_url", out var urlEl) ? urlEl.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(url)) continue;
            if (!name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;
            if (name.Contains("UABEA", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("UABEAvalonia", StringComparison.OrdinalIgnoreCase))
                return url;
        }

        throw new InvalidOperationException(L("error.uabeaReleaseZipNotFound"));
    }

    private static async Task EnsureVgmstreamCliAsync(string externalRoot, string dlRoot, Action<string> log, CancellationToken ct)
    {
        var outDir = Path.Combine(externalRoot, "vgmstream");
        var cliPath = Path.Combine(outDir, "vgmstream-cli.exe");
        var probeDll = Path.Combine(outDir, "in_vgmstream.dll");
        if (File.Exists(cliPath) && new FileInfo(cliPath).Length > 0 && File.Exists(probeDll))
        {
            log(L("log.vgmstreamExists"));
            return;
        }

        var overrideZipUrl = Environment.GetEnvironmentVariable("HS2VR_VGMSTREAM_ZIP_URL");
        var zipUrl = string.IsNullOrWhiteSpace(overrideZipUrl)
            ? await ResolveVgmstreamZipUrlAsync(ct)
            : overrideZipUrl.Trim();

        var zip = Path.Combine(dlRoot, "vgmstream.zip");
        await DownloadFileAsync(zipUrl, zip, log, ct);

        var temp = Path.Combine(dlRoot, "vgmstream_extract");
        if (Directory.Exists(temp))
            Directory.Delete(temp, true);
        Directory.CreateDirectory(temp);
        ExtractZip(zip, temp, stripSingleRoot: false);

        var foundCli = Directory.GetFiles(temp, "vgmstream-cli.exe", SearchOption.AllDirectories).FirstOrDefault();
        if (foundCli == null)
            throw new InvalidOperationException(L("error.vgmstreamCliMissing"));

        var srcDir = Path.GetDirectoryName(foundCli)!;
        Directory.CreateDirectory(outDir);
        foreach (var file in Directory.GetFiles(srcDir, "*", SearchOption.TopDirectoryOnly))
        {
            var dst = Path.Combine(outDir, Path.GetFileName(file));
            File.Copy(file, dst, true);
        }

        log(L("log.vgmstreamReady"));
    }

    private static async Task EnsureFfmpegAsync(string externalRoot, string dlRoot, Action<string> log, CancellationToken ct)
    {
        var outDir = Path.Combine(externalRoot, "ffmpeg", "bin");
        var ffmpegPath = Path.Combine(outDir, "ffmpeg.exe");
        var ffprobePath = Path.Combine(outDir, "ffprobe.exe");
        if (File.Exists(ffmpegPath) && File.Exists(ffprobePath))
        {
            log(L("log.ffmpegExists"));
            return;
        }

        var overrideZipUrl = Environment.GetEnvironmentVariable("HS2VR_FFMPEG_ZIP_URL");
        var zipUrl = string.IsNullOrWhiteSpace(overrideZipUrl)
            ? await ResolveFfmpegZipUrlAsync(ct)
            : overrideZipUrl.Trim();

        var zip = Path.Combine(dlRoot, "ffmpeg.zip");
        await DownloadFileAsync(zipUrl, zip, log, ct);

        var temp = Path.Combine(dlRoot, "ffmpeg_extract");
        if (Directory.Exists(temp))
            Directory.Delete(temp, true);
        Directory.CreateDirectory(temp);
        ExtractZip(zip, temp, stripSingleRoot: false);

        var foundFfmpeg = Directory.GetFiles(temp, "ffmpeg.exe", SearchOption.AllDirectories).FirstOrDefault();
        if (foundFfmpeg == null)
            throw new InvalidOperationException(L("error.ffmpegBinaryMissing"));

        var srcBinDir = Path.GetDirectoryName(foundFfmpeg)!;
        var foundFfprobe = Path.Combine(srcBinDir, "ffprobe.exe");
        if (!File.Exists(foundFfprobe))
            throw new InvalidOperationException(L("error.ffprobeBinaryMissing"));

        Directory.CreateDirectory(outDir);
        foreach (var file in Directory.GetFiles(srcBinDir, "*", SearchOption.TopDirectoryOnly))
        {
            var dst = Path.Combine(outDir, Path.GetFileName(file));
            File.Copy(file, dst, true);
        }
        log(L("log.ffmpegReady"));
    }

    private static async Task<string> ResolveFfmpegZipUrlAsync(CancellationToken ct)
    {
        using var resp = await Http.GetAsync(FfmpegLatestApiUrl, ct);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("assets", out var assets) || assets.ValueKind != System.Text.Json.JsonValueKind.Array)
            throw new InvalidOperationException(L("error.ffmpegReleaseApiInvalid"));

        // Prefer shared win64 builds so all runtime DLLs are included together in /bin.
        foreach (var a in assets.EnumerateArray())
        {
            var name = a.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "" : "";
            var url = a.TryGetProperty("browser_download_url", out var urlEl) ? urlEl.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(url)) continue;
            if (!name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;
            if (name.Contains("win64", StringComparison.OrdinalIgnoreCase) &&
                name.Contains("gpl", StringComparison.OrdinalIgnoreCase) &&
                name.Contains("shared", StringComparison.OrdinalIgnoreCase))
                return url;
        }

        // Fallback to any win64 zip asset.
        foreach (var a in assets.EnumerateArray())
        {
            var name = a.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "" : "";
            var url = a.TryGetProperty("browser_download_url", out var urlEl) ? urlEl.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(url)) continue;
            if (!name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;
            if (name.Contains("win64", StringComparison.OrdinalIgnoreCase))
                return url;
        }

        throw new InvalidOperationException(L("error.ffmpegReleaseZipNotFound"));
    }

    private static async Task<string> ResolveVgmstreamZipUrlAsync(CancellationToken ct)
    {
        using var resp = await Http.GetAsync(VgmstreamLatestApiUrl, ct);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("assets", out var assets) || assets.ValueKind != System.Text.Json.JsonValueKind.Array)
            throw new InvalidOperationException(L("error.vgmstreamReleaseApiInvalid"));

        string? anyZip = null;
        foreach (var a in assets.EnumerateArray())
        {
            var name = a.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "" : "";
            var url = a.TryGetProperty("browser_download_url", out var urlEl) ? urlEl.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(url)) continue;
            if (!name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;

            anyZip ??= url;
            if (name.Contains("win", StringComparison.OrdinalIgnoreCase))
                return url;
        }

        if (!string.IsNullOrWhiteSpace(anyZip))
            return anyZip;

        throw new InvalidOperationException(L("error.vgmstreamReleaseZipNotFound"));
    }
}


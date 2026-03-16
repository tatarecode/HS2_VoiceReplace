using System.Globalization;
using System.Net.Http;
using System.Text;

namespace HS2VoiceReplace;

internal static partial class DependencyBootstrapper
{
    private const string SeedVcZipUrl = "https://github.com/Plachtaa/seed-vc/archive/refs/heads/main.zip";
    private const string UabeaLatestApiUrl = "https://api.github.com/repos/nesrak1/UABEA/releases/latest";
    private const string VgmstreamLatestApiUrl = "https://api.github.com/repos/vgmstream/vgmstream/releases/latest";
    private const string FfmpegLatestApiUrl = "https://api.github.com/repos/BtbN/FFmpeg-Builds/releases/latest";
    private const string AuxScriptVersion = "20260306_1";
    private const string PipInstallWarningSuppression = "--no-warn-script-location";
    private const string PipProgressStyle = "--progress-bar raw";

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromMinutes(30),
    };

    static DependencyBootstrapper()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("HS2VoiceReplace/1.0");
    }

    public static async Task SetupAsync(string externalRoot, string bundledRoot, Action<string> log, CancellationToken ct)
    {
        externalRoot = Path.GetFullPath(externalRoot);
        bundledRoot = Path.GetFullPath(bundledRoot);
        Directory.CreateDirectory(externalRoot);
        var dlRoot = Path.Combine(externalRoot, "_downloads");
        var stateRoot = Path.Combine(externalRoot, "_state");
        Directory.CreateDirectory(dlRoot);
        Directory.CreateDirectory(stateRoot);

        bool IsDone(string step) => File.Exists(Path.Combine(stateRoot, $"{step}.done"));
        void MarkDone(string step) => File.WriteAllText(Path.Combine(stateRoot, $"{step}.done"), DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture), Encoding.UTF8);
        async Task RunStep(string step, Func<bool> ready, Func<Task> action)
        {
            if (ready() && IsDone(step))
            {
                log($"[skip] setup:{step}");
                return;
            }
            await action();
            if (ready()) MarkDone(step);
        }

        var sourceRoots = EnumerateSourceRoots(bundledRoot).ToArray();
        var pythonManifest = PythonRuntimeManifest.Load(sourceRoots);
        var sharedPythonExe = FindSharedPythonExe(sourceRoots, pythonManifest);
        var pythonDir = Path.Combine(externalRoot, "python");
        string? pythonExe = null;
        await RunStep(
            "python",
            () => (!string.IsNullOrWhiteSpace(sharedPythonExe) && File.Exists(sharedPythonExe)) || File.Exists(Path.Combine(pythonDir, "python.exe")),
            async () =>
            {
                if (!string.IsNullOrWhiteSpace(sharedPythonExe) && File.Exists(sharedPythonExe))
                {
                    pythonExe = sharedPythonExe;
                    log(L("log.sharedPythonUsed", sharedPythonExe));
                    return;
                }

                pythonExe = await EnsurePythonAsync(pythonDir, dlRoot, externalRoot, log, ct, pythonManifest);
            });
        pythonExe ??= !string.IsNullOrWhiteSpace(sharedPythonExe) && File.Exists(sharedPythonExe)
            ? sharedPythonExe
            : Path.Combine(pythonDir, "python.exe");
        if (!File.Exists(pythonExe)) throw new InvalidOperationException(L("error.pythonMissing"));
        var sitePackagesRoot = GetPythonSitePackagesRoot(pythonExe);

        var seedRoot = Path.Combine(externalRoot, "seed_vc_v2");
        await RunStep(
            "seedvc",
            () => File.Exists(Path.Combine(seedRoot, "requirements.txt")),
            async () => await EnsureSeedVcAsync(seedRoot, dlRoot, log, ct));

        await RunStep(
            "uabea",
            () => File.Exists(Path.Combine(externalRoot, "uabea", "classdata.tpk")),
            async () => await EnsureUabeaClassDataAsync(externalRoot, dlRoot, log, ct));

        await RunStep(
            "vgmstream",
            () =>
                File.Exists(Path.Combine(externalRoot, "vgmstream", "vgmstream-cli.exe")) &&
                File.Exists(Path.Combine(externalRoot, "vgmstream", "in_vgmstream.dll")),
            async () => await EnsureVgmstreamCliAsync(externalRoot, dlRoot, log, ct));

        await RunStep(
            "ffmpeg",
            () =>
                File.Exists(Path.Combine(externalRoot, "ffmpeg", "bin", "ffmpeg.exe")) &&
                File.Exists(Path.Combine(externalRoot, "ffmpeg", "bin", "ffprobe.exe")),
            async () => await EnsureFfmpegAsync(externalRoot, dlRoot, log, ct));

        await RunStep(
            "pip_packages",
            () =>
                Directory.Exists(Path.Combine(sitePackagesRoot, "noisereduce")) &&
                Directory.Exists(Path.Combine(sitePackagesRoot, "torch")) &&
                (!HasLikelyNvidiaSmi() || File.Exists(Path.Combine(stateRoot, "torch_cuda.ok"))),
            async () => await EnsurePipPackagesAsync(pythonExe, seedRoot, externalRoot, stateRoot, log, ct));

        await RunStep(
            "aux",
            () =>
                File.Exists(Path.Combine(externalRoot, "scripts", "python_cli_common.py")) &&
                File.Exists(Path.Combine(externalRoot, "scripts", "seed_vc_batch_common.py")) &&
                File.Exists(Path.Combine(externalRoot, "scripts", "seed_vc_v1_inprocess_batch.py")) &&
                File.Exists(Path.Combine(externalRoot, "scripts", "seed_vc_v2_inprocess_batch.py")) &&
                File.Exists(Path.Combine(externalRoot, "scripts", "select_voice_style_segment.py")) &&
                File.Exists(Path.Combine(externalRoot, "tools", "UabAudioClipPatcher", "UabAudioClipPatcher.exe")) &&
                File.Exists(Path.Combine(externalRoot, "mods_template", VoiceReplaceNames.RuntimeTemplateDirName, "manifest.xml")) &&
                File.Exists(Path.Combine(stateRoot, "aux.version")) &&
                string.Equals(
                    File.ReadAllText(Path.Combine(stateRoot, "aux.version")).Trim(),
                    AuxScriptVersion,
                    StringComparison.Ordinal),
            async () =>
            {
                await EnsureAuxiliaryFilesAsync(externalRoot, bundledRoot, log, ct);
                File.WriteAllText(Path.Combine(stateRoot, "aux.version"), AuxScriptVersion, new UTF8Encoding(false));
            });
    }

    private static async Task<string> EnsurePythonAsync(string pythonDir, string dlRoot, string workDir, Action<string> log, CancellationToken ct, PythonRuntimeManifest pythonManifest)
    {
        var pyExe = Path.Combine(pythonDir, "python.exe");
        if (!File.Exists(pyExe))
        {
            Directory.CreateDirectory(pythonDir);
            var pyZip = Path.Combine(dlRoot, pythonManifest.GetEmbedZipFileName());
            await DownloadFileAsync(pythonManifest.EmbedZipUrl, pyZip, log, ct);
            ExtractZip(pyZip, pythonDir, stripSingleRoot: false);
            PatchEmbeddedPythonPth(pythonDir, log);
        }

        pyExe = Path.Combine(pythonDir, "python.exe");
        if (!File.Exists(pyExe))
            throw new InvalidOperationException(L("error.embeddedPythonExtractFailed"));

        var getPip = Path.Combine(dlRoot, "get-pip.py");
        await DownloadFileAsync(pythonManifest.GetPipUrl, getPip, log, ct);

        log(L("log.installPip"));
        await ProcessUtil.RunAsync(pyExe, $"\"{getPip}\" --disable-pip-version-check", workDir, log, ct);
        await ProcessUtil.RunAsync(pyExe, $"-m pip install {PipInstallWarningSuppression} {PipProgressStyle} --upgrade pip setuptools wheel", workDir, log, ct);
        return pyExe;
    }

    private static async Task EnsureSeedVcAsync(string seedRoot, string dlRoot, Action<string> log, CancellationToken ct)
    {
        var req = Path.Combine(seedRoot, "requirements.txt");
        if (File.Exists(req))
        {
            log(L("log.seedVcAlreadyExists"));
            return;
        }

        Directory.CreateDirectory(seedRoot);
        var zip = Path.Combine(dlRoot, "seed-vc-main.zip");
        await DownloadFileAsync(SeedVcZipUrl, zip, log, ct);
        ExtractZip(zip, seedRoot, stripSingleRoot: true);
    }

    private static async Task EnsurePipPackagesAsync(string pyExe, string seedRoot, string workDir, string stateRoot, Action<string> log, CancellationToken ct)
    {
        var req = Path.Combine(seedRoot, "requirements.txt");
        if (File.Exists(req))
        {
            log(L("log.installSeedVcDeps"));
            await ProcessUtil.RunAsync(pyExe, $"-m pip install {PipInstallWarningSuppression} {PipProgressStyle} -r \"{req}\"", workDir, log, ct);
        }

        // In-process batch script dependency
        await ProcessUtil.RunAsync(pyExe, $"-m pip install {PipInstallWarningSuppression} {PipProgressStyle} noisereduce==3.0.3", workDir, log, ct);

        var cudaOk = await EnsureTorchCudaAsync(pyExe, workDir, log, ct);
        var marker = Path.Combine(stateRoot, "torch_cuda.ok");
        if (cudaOk)
        {
            File.WriteAllText(marker, DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture), new UTF8Encoding(false));
        }
        else if (File.Exists(marker))
        {
            File.Delete(marker);
        }
    }

    private static async Task<bool> EnsureTorchCudaAsync(string pyExe, string workDir, Action<string> log, CancellationToken ct)
    {
        var probe = await ProbeTorchAsync(pyExe, workDir, ct);
        log($"torch probe: ver={probe.Version} cuda={probe.CudaAvailable} cuda_ver={probe.CudaVersion}");
        if (probe.CudaAvailable)
            return true;

        var hasNvidia = await HasNvidiaGpuAsync(ct);
        if (!hasNvidia)
        {
            log(L("log.noNvidiaGpu"));
            return false;
        }

        log(L("log.enableCudaTorch"));
        var tried = new List<string>();
        foreach (var idxUrl in new[]
        {
            "https://download.pytorch.org/whl/cu121",
            "https://download.pytorch.org/whl/cu118",
        })
        {
            tried.Add(idxUrl);
            try
            {
                await ProcessUtil.RunAsync(
                    pyExe,
                    $"-m pip install {PipInstallWarningSuppression} {PipProgressStyle} --upgrade --index-url {idxUrl} torch torchvision torchaudio",
                    workDir,
                    log,
                    ct);

                var re = await ProbeTorchAsync(pyExe, workDir, ct);
                log($"torch reprobe: ver={re.Version} cuda={re.CudaAvailable} cuda_ver={re.CudaVersion}");
                if (re.CudaAvailable)
                    return true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log($"[warn] CUDA torch install failed ({idxUrl}): {ex.Message}");
            }
        }

        log(L("log.cudaTorchFailed"));
        log("       tried: " + string.Join(", ", tried));
        return false;
    }

    private sealed record TorchProbe(string Version, bool CudaAvailable, string? CudaVersion);

    private static async Task<TorchProbe> ProbeTorchAsync(string pyExe, string workDir, CancellationToken ct)
    {
        var code = "-c \"import json,torch; print(json.dumps({'ver':str(torch.__version__),'cuda':bool(torch.cuda.is_available()),'cuda_ver':str(torch.version.cuda)}))\"";
        var res = await ProcessUtil.RunCaptureAsync(pyExe, code, workDir, ct);
        if (res.ExitCode != 0)
            throw new InvalidOperationException("Torch runtime probe failed: " + res.StdErr);

        var line = res.StdOut
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault()?.Trim();
        if (string.IsNullOrWhiteSpace(line))
            throw new InvalidOperationException("Torch runtime probe returned no output.");

        using var doc = System.Text.Json.JsonDocument.Parse(line);
        var root = doc.RootElement;
        var ver = root.TryGetProperty("ver", out var v) ? v.GetString() ?? "" : "";
        var cuda = root.TryGetProperty("cuda", out var c) && c.GetBoolean();
        var cudaVer = root.TryGetProperty("cuda_ver", out var cv) ? cv.GetString() : null;
        return new TorchProbe(ver, cuda, cudaVer);
    }

    private static async Task<bool> HasNvidiaGpuAsync(CancellationToken ct)
    {
        try
        {
            var r = await ProcessUtil.RunCaptureAsync("nvidia-smi", "--query-gpu=name --format=csv,noheader", Directory.GetCurrentDirectory(), ct);
            return r.ExitCode == 0 && !string.IsNullOrWhiteSpace(r.StdOut);
        }
        catch
        {
            return false;
        }
    }

    private static bool HasLikelyNvidiaSmi()
    {
        try
        {
            var sysPath = Path.Combine(Environment.SystemDirectory, "nvidia-smi.exe");
            if (File.Exists(sysPath))
                return true;

            var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (var part in path.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = part.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                    continue;
                var candidate = Path.Combine(trimmed, "nvidia-smi.exe");
                if (File.Exists(candidate))
                    return true;
            }
        }
        catch
        {
        }
        return false;
    }

    private static string GetPythonSitePackagesRoot(string pythonExe)
        => Path.Combine(Path.GetDirectoryName(Path.GetFullPath(pythonExe))!, "Lib", "site-packages");

    private static string? FindSharedPythonExe(IEnumerable<string> roots, PythonRuntimeManifest pythonManifest)
    {
        foreach (var root in roots)
        {
            var candidate = pythonManifest.GetRepoLocalPythonFullPath(root);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static string L(string key, params object[] args) => UiTextCatalog.Get(LocalizationState.CurrentLanguage, key, args);
}


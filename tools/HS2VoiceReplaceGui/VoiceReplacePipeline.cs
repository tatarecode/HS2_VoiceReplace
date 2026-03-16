using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace HS2VoiceReplace;

internal enum PipelineMode
{
    ExtractOnly,
    PreviewOnly,
    BuildOnly,
    DeployOnly,
}

internal sealed class PipelineRunResult
{
    public string RunRoot { get; init; } = "";
    public string? PreviewNormalWav { get; init; }
    public string? PreviewEroWav { get; init; }
}

internal static partial class VoiceReplacePipeline
{
    // This partial holds the shared orchestration entry point, common constants,
    // and run-level dependency/path setup used by all pipeline modes.
    private const string RuntimePluginFileName = VoiceReplaceNames.RuntimePluginFileName;
    private const string LegacyRuntimePluginFileName = VoiceReplaceNames.LegacyRuntimePluginFileName;
    private const string DeployStateFileName = VoiceReplaceNames.DeployStateFileName;
    private sealed record BundleTarget(string Key, string WavRel, string SrcRel, string DstRel, bool IsCustomSource = false);
    private static readonly Regex PersonalityClipRegex = new(
        @"^(?:hsa|hsh|hso|hss)_(?<id>\d{2})_",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static async Task<PipelineRunResult> RunCoreAsync(PipelineOptions o, Action<string> log, CancellationToken ct, PipelineMode mode)
    {
        ValidateOptions(o, mode);

        var needsSeed = mode is PipelineMode.PreviewOnly or PipelineMode.BuildOnly;
        var needsExtractDeps = mode is PipelineMode.ExtractOnly or PipelineMode.PreviewOnly or PipelineMode.BuildOnly;
        var needsVgmstream = mode is PipelineMode.ExtractOnly or PipelineMode.PreviewOnly;
        var needsBuildBundles = mode == PipelineMode.BuildOnly;
        var needsDeploy = mode == PipelineMode.DeployOnly || o.DeployAfterBuild;

        var seedVcRoot = needsSeed ? ResolveSeedVcRoot(o, o.SeedVc) : "";
        var classDataPath = needsExtractDeps ? ResolveDependencyPath(o, "uabea", "classdata.tpk") : "";
        var vgmstreamCli = needsVgmstream ? ResolveDependencyPath(o, "vgmstream", "vgmstream-cli.exe") : "";
        var selectScript = needsSeed ? ResolveDependencyPath(o, "scripts", "select_voice_style_segment.py") : "";
        var inferScript = needsSeed ? ResolveDependencyPath(o, "scripts", GetSeedVcInferScriptName(o.SeedVc)) : "";
        var templateRoot = needsBuildBundles ? ResolveDependencyPath(o, "mods_template", VoiceReplaceNames.RuntimeTemplateDirName) : "";
        var runtimeDll = needsDeploy ? ResolveRuntimePluginPath(o) : "";
        var patcherExe = needsBuildBundles ? ResolveDependencyPath(o, "tools", "UabAudioClipPatcher", "UabAudioClipPatcher.exe") : "";
        var pyExe = needsSeed ? ResolvePythonExe(o) : "";
        var workRoot = ResolveWorkingDirectory(o);
        var pyEnv = needsSeed ? BuildPyEnv(o) : new Dictionary<string, string>();

        var pid = $"c{o.TargetPersonalityId:00}";
        var paths = BuildRunPaths(o, pid);
        EnsureRunDirectories(paths);
        var steps = new PipelineStepTracker(o, paths.DoneRoot, log);

        var runRootPid = ResolvePersonalityIdFromRunRoot(paths.RunRoot, -1);
        if (runRootPid >= 0 && runRootPid != o.TargetPersonalityId)
        {
            log($"[warn] run_root name indicates c{runRootPid:00}, but current target is c{o.TargetPersonalityId:00}.");
            log("[warn] stale artifacts may cause clip mismatch. forcing regenerate: 01_extract..07_split");
            steps.ClearDone("01_extract", "02_fsb_to_wav", "03_manifest", "05_seedvc", "06_rebuild", "07_split");
        }

        log($"dependency roots: external={o.ExternalToolsRoot}, bundled={o.BundleRoot}");
        log($"run root: {paths.RunRoot}");

        var sampleSigPath = Path.Combine(paths.RunRoot, "sample_signature.csv");
        var sampleNormalCurrent = ComputeTextSha256(BuildSampleInputSignature(o, isEro: false));
        var sampleEroCurrent = ComputeTextSha256(BuildSampleInputSignature(o, isEro: true));
        var (sampleNormalPrevious, sampleEroPrevious) = ReadSampleSignatures(sampleSigPath);
        var sampleSignatureSameAsPrevious =
            !string.IsNullOrWhiteSpace(sampleNormalPrevious) &&
            !string.IsNullOrWhiteSpace(sampleEroPrevious) &&
            string.Equals(sampleNormalPrevious, sampleNormalCurrent, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(sampleEroPrevious, sampleEroCurrent, StringComparison.OrdinalIgnoreCase);
        WriteSampleSignatureFile(paths.RunRoot, o);
        log("sample signature file: " + sampleSigPath);
        log($"sample signature current: normal={sampleNormalCurrent}, ero={sampleEroCurrent}");
        if (!string.IsNullOrWhiteSpace(sampleNormalPrevious) || !string.IsNullOrWhiteSpace(sampleEroPrevious))
            log($"sample signature previous: normal={sampleNormalPrevious}, ero={sampleEroPrevious}");

        var targets = ResolveTargetsForPersonality(o, pid, log);

        var bundleSigPath = Path.Combine(paths.RunRoot, "bundle_selection_signature.txt");
        var bundleSigCurrent = BuildBundleSelectionSignature(o, pid, targets);
        var bundleSigPrevious = File.Exists(bundleSigPath)
            ? (File.ReadAllText(bundleSigPath, Encoding.UTF8) ?? "").Trim()
            : "";
        if (string.IsNullOrWhiteSpace(bundleSigPrevious))
        {
            log("[info] bundle selection signature not found. baseline signature is recorded without forcing regenerate.");
        }
        else if (!string.Equals(bundleSigPrevious, bundleSigCurrent, StringComparison.OrdinalIgnoreCase))
        {
            if (mode == PipelineMode.BuildOnly)
                throw new InvalidOperationException(L("error.bundleSelectionChangedReextract"));

            log("[warn] bundle selection changed. forcing regenerate from extract to split.");
            steps.ClearDone("01_extract", "02_fsb_to_wav", "03_manifest", "05_seedvc", "06_rebuild", "07_split");
            ResetDir(paths.ExtractFsbRoot);
            ResetDir(paths.ExtractWavRoot);
            ResetDir(paths.BatchRoot);
            ResetDir(paths.OutWavRoot);
            ResetDir(paths.ReplaceInputRoot);
            ResetDir(paths.SplitOutRoot);
        }
        File.WriteAllText(bundleSigPath, bundleSigCurrent, new UTF8Encoding(false));
        log("bundle selection signature: " + bundleSigPath);

        if (mode == PipelineMode.PreviewOnly)
        {
            return await RunPreviewModeAsync(
                o,
                log,
                ct,
                pid,
                paths,
                steps,
                targets,
                classDataPath,
                vgmstreamCli,
                pyExe,
                selectScript,
                inferScript,
                seedVcRoot,
                workRoot,
                pyEnv);
        }

        await RunExtractPhaseAsync(
            o,
            log,
            ct,
            pid,
            paths,
            steps,
            targets,
            classDataPath,
            vgmstreamCli,
            workRoot,
            requireExistingExtract: mode == PipelineMode.BuildOnly);

        var manifest = Path.Combine(paths.BatchRoot, "routing_manifest.csv");
        if (File.Exists(manifest))
        {
            EnsureSampleSignatureMap(paths.RunRoot, manifest, paths.OutWavRoot);
            log("sample signature map: " + Path.Combine(paths.RunRoot, "sample_signature_map.csv"));
        }

        if (mode == PipelineMode.ExtractOnly)
        {
            log(L("log.extractCompletedShort"));
            return new PipelineRunResult { RunRoot = paths.RunRoot };
        }

        if (mode == PipelineMode.DeployOnly)
        {
            if (!HasExpectedConvertedWavs(manifest, paths.OutWavRoot))
                throw new InvalidOperationException(L("error.deployIncompleteOutputs"));

            var deployZipmods = Directory.GetFiles(paths.SplitOutRoot, $"HS2VoiceReplace_{pid}_*.zipmod", SearchOption.TopDirectoryOnly)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (deployZipmods.Count == 0)
                throw new InvalidOperationException(L("error.deployZipmodsMissing"));
            log(L("log.deployToBackup"));
            Deploy(o, runtimeDll, deployZipmods, log);
            return new PipelineRunResult { RunRoot = paths.RunRoot };
        }

        await RunBuildAndDeployPhaseAsync(
            o,
            log,
            ct,
            pid,
            paths,
            steps,
            targets,
            pyExe,
            selectScript,
            inferScript,
            seedVcRoot,
            templateRoot,
            runtimeDll,
            patcherExe,
            classDataPath,
            workRoot,
            pyEnv,
            sampleSignatureSameAsPrevious,
            sampleNormalCurrent,
            sampleEroCurrent);

        return new PipelineRunResult { RunRoot = paths.RunRoot };
    }

    private static IReadOnlyDictionary<string, string> BuildPyEnv(PipelineOptions o)
    {
        var env = new Dictionary<string, string>
        {
            ["PYTHONWARNINGS"] = "ignore::UserWarning,ignore::FutureWarning",
            ["TQDM_DISABLE"] = "1",
            ["ALL_PROXY"] = "",
            ["HTTP_PROXY"] = "",
            ["HTTPS_PROXY"] = "",
            ["GIT_HTTP_PROXY"] = "",
            ["GIT_HTTPS_PROXY"] = "",
        };

        var ffmpegBin = FindFfmpegBinDir(o);
        if (!string.IsNullOrWhiteSpace(ffmpegBin))
        {
            var currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            env["PATH"] = ffmpegBin + ";" + currentPath;
        }
        return env;
    }

    private static string? FindFfmpegBinDir(PipelineOptions o)
    {
        var candidates = new List<string>();
        foreach (var root in EnumerateDependencyRoots(o))
        {
            candidates.Add(Path.Combine(root, "ffmpeg", "bin"));
            candidates.Add(Path.Combine(root, "_tools", "ffmpeg", "bin"));
            candidates.Add(Path.Combine(root, "_tools", "rvc_webui", "_deps", "ffmpeg", "bin"));
            candidates.Add(Path.Combine(root, "rvc_webui", "_deps", "ffmpeg", "bin"));
        }
        var overridePath = Environment.GetEnvironmentVariable("HS2VR_FFMPEG_BIN");
        if (!string.IsNullOrWhiteSpace(overridePath))
            candidates.Insert(0, overridePath.Trim());

        foreach (var dir in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dir)) continue;
                var full = Path.GetFullPath(dir);
                if (File.Exists(Path.Combine(full, "ffmpeg.exe")))
                    return full;
            }
            catch
            {
            }
        }
        return null;
    }

    private readonly record struct StyleWavPair(string StyleNormalWav, string StyleEroWav);

    private static string L(string key, params object[] args) => UiTextCatalog.Get(LocalizationState.CurrentLanguage, key, args);
}


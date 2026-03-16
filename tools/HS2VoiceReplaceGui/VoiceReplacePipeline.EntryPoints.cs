using System.Globalization;
using System.Text;

namespace HS2VoiceReplace;

internal static partial class VoiceReplacePipeline
{
    // Public entry points stay here so callers can treat VoiceReplacePipeline as the compatibility facade while internal responsibilities remain split.

    public static Task<PipelineRunResult> RunPreviewAsync(PipelineOptions o, Action<string> log, CancellationToken ct)
        => RunCoreAsync(o, log, ct, PipelineMode.PreviewOnly);

    public static Task<PipelineRunResult> RunExtractAsync(PipelineOptions o, Action<string> log, CancellationToken ct)
        => RunCoreAsync(o, log, ct, PipelineMode.ExtractOnly);

    public static Task<PipelineRunResult> RunBuildAsync(PipelineOptions o, Action<string> log, CancellationToken ct)
        => RunCoreAsync(o, log, ct, PipelineMode.BuildOnly);

    public static Task<PipelineRunResult> RunDeployAsync(PipelineOptions o, Action<string> log, CancellationToken ct)
        => RunCoreAsync(o, log, ct, PipelineMode.DeployOnly);

    public static void RunUndeploy(PipelineOptions o, Action<string> log, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(o.Hs2Root) || !Directory.Exists(o.Hs2Root))
            throw new InvalidOperationException(L("error.hs2RootMissing", o.Hs2Root));

        log(L("log.undeployPersonality", o.TargetPersonalityId.ToString("00")));
        Undeploy(o.Hs2Root, o.TargetPersonalityId, log);
    }

    public static async Task<string> RunSingleWavAsync(PipelineOptions o, string sourceWav, string modelBucket, Action<string> log, CancellationToken ct)
    {
        ValidateSingleOptions(o, sourceWav, modelBucket);

        var seedVcRoot = ResolveSeedVcRoot(o, o.SeedVc);
        var selectScript = ResolveDependencyPath(o, "scripts", "select_voice_style_segment.py");
        var inferScript = ResolveDependencyPath(o, "scripts", GetSeedVcInferScriptName(o.SeedVc));
        var pyExe = ResolvePythonExe(o);
        var workRoot = ResolveWorkingDirectory(o);
        var pyEnv = BuildPyEnv(o);

        var pid = $"c{o.TargetPersonalityId:00}";
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var runRoot = Path.Combine(o.OutputBaseRoot, "gui_runs", $"single_{stamp}_{pid}");
        var styleRoot = Path.Combine(runRoot, "style");
        var outRoot = Path.Combine(runRoot, "single_out");
        var manifest = Path.Combine(runRoot, "single_manifest.csv");
        var report = Path.Combine(runRoot, "single_seed_vc_report.csv");

        Directory.CreateDirectory(runRoot);
        Directory.CreateDirectory(styleRoot);
        Directory.CreateDirectory(outRoot);

        log($"single run root: {runRoot}");
        var stylePair = await PrepareStyleWavsAsync(o, pyExe, selectScript, workRoot, pyEnv, styleRoot, log, ct);

        var bucket = string.Equals(modelBucket, "ero", StringComparison.OrdinalIgnoreCase) ? "ero" : "normal";
        var outName = SanitizeWavFileName(Path.GetFileName(sourceWav));
        var manifestLines = new List<string>
        {
            "relative_path,model_bucket,source_file",
            $"\"{outName}\",\"{bucket}\",\"{sourceWav.Replace("\"", "\"\"")}\"",
        };
        File.WriteAllLines(manifest, manifestLines, new UTF8Encoding(false));

        await RunSeedVcAsync(
            o.SeedVc,
            pyExe,
            inferScript,
            seedVcRoot,
            manifest,
            stylePair.StyleNormalWav,
            stylePair.StyleEroWav,
            outRoot,
            report,
            pyEnv,
            workRoot,
            log,
            ct);

        var outWav = Path.Combine(outRoot, outName);
        if (!File.Exists(outWav))
        {
            outWav = Directory.GetFiles(outRoot, "*.wav", SearchOption.AllDirectories)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault() ?? outWav;
        }
        if (!File.Exists(outWav))
            throw new InvalidOperationException(L("error.singleRebuildOutputMissing", report));

        log($"single output: {outWav}");
        return outWav;
    }

    public static async Task<string> RebuildSingleInFullRunAsync(
        PipelineOptions o,
        string runRoot,
        string inputWav,
        string modelBucket,
        Action<string> log,
        CancellationToken ct)
    {
        ValidatePartialRebuildOptions(o, runRoot, inputWav, modelBucket);

        var runPidValue = ResolvePersonalityIdFromRunRoot(runRoot, o.TargetPersonalityId);
        var seedVcRoot = ResolveSeedVcRoot(o, o.SeedVc);
        var selectScript = ResolveDependencyPath(o, "scripts", "select_voice_style_segment.py");
        var inferScript = ResolveDependencyPath(o, "scripts", GetSeedVcInferScriptName(o.SeedVc));
        var classDataPath = ResolveDependencyPath(o, "uabea", "classdata.tpk");
        var templateRoot = ResolveDependencyPath(o, "mods_template", VoiceReplaceNames.RuntimeTemplateDirName);
        var runtimeDll = ResolveRuntimePluginPath(o);
        var patcherExe = ResolveDependencyPath(o, "tools", "UabAudioClipPatcher", "UabAudioClipPatcher.exe");
        var pyExe = ResolvePythonExe(o);
        var workRoot = ResolveWorkingDirectory(o);
        var pyEnv = BuildPyEnv(o);

        var pid = $"c{runPidValue:00}";
        var runRootFull = Path.GetFullPath(runRoot);
        var manifestPath = Path.Combine(runRootFull, "rvc_batches", "routing_manifest.csv");
        var partialRoot = Path.Combine(runRootFull, "_partial_rebuild");
        // Keep partial rebuild style artifacts isolated from the main full-run style cache.
        // Otherwise a per-row rebuild can overwrite resume_cXX/style wavs without updating
        // style_signature.txt, and subsequent Run All may reuse stale style wavs.
        var styleRoot = Path.Combine(partialRoot, "style");
        var outWavRoot = Path.Combine(runRootFull, "voice_replace_wav");
        var extractWavRoot = Path.Combine(runRootFull, "voice_extract_wav", pid);
        var replaceInputRoot = Path.Combine(runRootFull, "voice_replace_input");
        var splitOutRoot = Path.Combine(runRootFull, "voice_replace_mod_split");
        Directory.CreateDirectory(partialRoot);
        Directory.CreateDirectory(styleRoot);
        Directory.CreateDirectory(outWavRoot);
        Directory.CreateDirectory(replaceInputRoot);
        Directory.CreateDirectory(splitOutRoot);

        log($"partial run root: {runRootFull}");
        log($"partial style root: {styleRoot}");
        var stylePair = await PrepareStyleWavsAsync(o, pyExe, selectScript, workRoot, pyEnv, styleRoot, log, ct);

        var resolved = ResolvePartialInput(manifestPath, extractWavRoot, outWavRoot, inputWav, modelBucket);
        var singleManifest = Path.Combine(partialRoot, "single_manifest.csv");
        var report = Path.Combine(partialRoot, $"seed_vc_partial_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        File.WriteAllLines(singleManifest, new[]
        {
            "relative_path,model_bucket,source_file",
            $"\"{resolved.RelativePath}\",\"{resolved.Bucket}\",\"{resolved.SourceFile.Replace("\"", "\"\"")}\"",
        }, new UTF8Encoding(false));

        await RunSeedVcAsync(
            o.SeedVc,
            pyExe,
            inferScript,
            seedVcRoot,
            singleManifest,
            stylePair.StyleNormalWav,
            stylePair.StyleEroWav,
            outWavRoot,
            report,
            pyEnv,
            workRoot,
            log,
            ct);

        var outWav = Path.Combine(outWavRoot, resolved.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(outWav))
            throw new InvalidOperationException(L("error.partialRebuildOutputMissing", outWav));

        var currentNormalSig = ComputeTextSha256(BuildSampleInputSignature(o, isEro: false));
        var currentEroSig = ComputeTextSha256(BuildSampleInputSignature(o, isEro: true));
        UpdateSampleSignatureMapFromProcessedManifest(
            runRootFull,
            manifestPath,
            singleManifest,
            outWavRoot,
            currentNormalSig,
            currentEroSig,
            o.StyleNormalSampleDisplayName,
            o.StyleEroSampleDisplayName,
            o.SeedVcSummary);

        var target = ResolveTargetFromRelativePath(resolved.RelativePath);
        var srcBundle = BuildSourceBundlePath(o.Hs2Root, pid, target);
        var wavDir = Path.Combine(outWavRoot, target.WavRel.Replace('/', Path.DirectorySeparatorChar));
        var dstBundle = Path.Combine(replaceInputRoot, target.DstRel.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(dstBundle)!);

        log($"partial rebuild bundle: {target.Key}");
        await ProcessUtil.RunAsync(
            patcherExe,
            $"\"{srcBundle}\" \"{classDataPath}\" \"{wavDir}\" \"{dstBundle}\" .wav",
            workRoot, log, ct);

        var rebuilt = BuildSplitZipmods(templateRoot, replaceInputRoot, splitOutRoot, pid, new[] { target.Key }, new[] { target });
        log($"partial split zipmod rebuilt: {string.Join(", ", rebuilt.Select(Path.GetFileName))}");

        if (o.DeployAfterBuild)
        {
            var allZipmods = Directory.GetFiles(splitOutRoot, $"HS2VoiceReplace_{pid}_*.zipmod", SearchOption.TopDirectoryOnly)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
            Deploy(o, runtimeDll, allZipmods, log);
            log($"partial deploy done: total zipmods={allZipmods.Count}");
        }

        return outWav;
    }

    public static async Task<string> RebuildRelativeInFullRunAsync(
        PipelineOptions o,
        string runRoot,
        string relativePath,
        string modelBucket,
        Action<string> log,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new InvalidOperationException(L("error.relativePathEmpty"));

        var runRootFull = Path.GetFullPath(runRoot);
        var manifestPath = Path.Combine(runRootFull, "rvc_batches", "routing_manifest.csv");
        var pid = $"c{o.TargetPersonalityId:00}";
        var extractWavRoot = Path.Combine(runRootFull, "voice_extract_wav", pid);
        var outWavRoot = Path.Combine(runRootFull, "voice_replace_wav");

        var rows = LoadManifestRows(manifestPath);
        var rel = relativePath.Replace('\\', '/');
        var row = rows.FirstOrDefault(r => string.Equals(r.RelativePath, rel, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(row.RelativePath))
            throw new InvalidOperationException(L("error.relativePathMissingInManifest", relativePath));

        var srcCandidates = new[]
        {
            row.SourceFile,
            Path.Combine(extractWavRoot, rel.Replace('/', Path.DirectorySeparatorChar)),
            Path.Combine(outWavRoot, rel.Replace('/', Path.DirectorySeparatorChar)),
        };
        var src = srcCandidates.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p));
        if (string.IsNullOrWhiteSpace(src))
            throw new FileNotFoundException(
                L("error.rebuildSourceMissing", rel, string.Join(" | ", srcCandidates.Where(x => !string.IsNullOrWhiteSpace(x)))));

        var bucket = string.Equals(modelBucket, "ero", StringComparison.OrdinalIgnoreCase) ? "ero" : "normal";
        return await RebuildSingleInFullRunAsync(o, runRootFull, src, bucket, log, ct);
    }
}













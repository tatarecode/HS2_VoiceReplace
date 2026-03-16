using System.Text;

namespace HS2VoiceReplace;

internal static partial class VoiceReplacePipeline
{
    // Preview and extract are grouped here because both operate on source bundles
    // and avoid touching the rebuild/deploy side of the pipeline.
    // Preview uses an isolated /preview subtree so sample audition artifacts never pollute
    // the main full-run outputs that deployment and resume logic depend on.
    private static async Task<PipelineRunResult> RunPreviewModeAsync(
        PipelineOptions o,
        Action<string> log,
        CancellationToken ct,
        string pid,
        PipelineRunPaths paths,
        PipelineStepTracker steps,
        IReadOnlyList<BundleTarget> targets,
        string classDataPath,
        string vgmstreamCli,
        string pyExe,
        string selectScript,
        string inferScript,
        string seedVcRoot,
        string workRoot,
        IReadOnlyDictionary<string, string> pyEnv)
    {
        ResetDir(paths.PreviewExtractFsbRoot);
        ResetDir(paths.PreviewExtractWavRoot);
        ResetDir(paths.PreviewBatchRoot);
        ResetDir(paths.PreviewWavRoot);

        var previewTarget = targets.FirstOrDefault(t => string.Equals(t.Key, "etc", StringComparison.OrdinalIgnoreCase));
        if (previewTarget == null)
            throw new InvalidOperationException(L("error.previewEtcBundleMissing"));

        log(L("log.previewExtractFsb"));
        ct.ThrowIfCancellationRequested();
        var previewSrcBundle = BuildSourceBundlePath(o.Hs2Root, pid, previewTarget);
        var previewFsbOutDir = Path.Combine(paths.PreviewExtractFsbRoot, previewTarget.WavRel.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(previewFsbOutDir);
        var previewClipCount = AudioBundleExtractor.ExtractAudioPayloads(previewSrcBundle, classDataPath, previewFsbOutDir);
        log($"  etc: clips={previewClipCount}");

        log(L("log.previewConvertWav"));
        var previewFsbs = Directory.GetFiles(paths.PreviewExtractFsbRoot, "*.fsb", SearchOption.AllDirectories)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (previewFsbs.Length == 0)
            throw new InvalidOperationException(L("error.previewFsbMissing"));
        foreach (var fsb in previewFsbs)
        {
            ct.ThrowIfCancellationRequested();
            var rel = Path.GetRelativePath(paths.PreviewExtractFsbRoot, fsb);
            var wav = Path.Combine(paths.PreviewExtractWavRoot, Path.ChangeExtension(rel, ".wav"));
            Directory.CreateDirectory(Path.GetDirectoryName(wav)!);
            await ProcessUtil.RunAsync(vgmstreamCli, $"-o \"{wav}\" \"{fsb}\"", workRoot, log, ct);
        }

        var previewSourceManifest = Path.Combine(paths.PreviewBatchRoot, "routing_manifest_etc.csv");
        BuildRoutingManifest(paths.PreviewExtractWavRoot, previewSourceManifest);
        log($"  preview source manifest: {previewSourceManifest}");

        var styleNormalWavPreview = Path.Combine(paths.StyleRoot, "style_normal.wav");
        var styleEroWavPreview = Path.Combine(paths.StyleRoot, "style_ero.wav");
        var styleNormalCsvPreview = Path.Combine(paths.StyleRoot, "style_normal.csv");
        var styleEroCsvPreview = Path.Combine(paths.StyleRoot, "style_ero.csv");
        var stylePairCsvPreview = Path.Combine(paths.StyleRoot, "style_pair.csv");
        var styleSigPathPreview = Path.Combine(paths.StyleRoot, "style_signature.txt");
        var styleSigCurrentPreview = BuildStyleSignature(o);

        bool StyleUpToDatePreview() =>
            File.Exists(styleNormalWavPreview) &&
            File.Exists(styleEroWavPreview) &&
            File.Exists(styleSigPathPreview) &&
            StyleReportInputMatches(styleNormalCsvPreview, o.StyleNormalSample) &&
            StyleReportInputMatches(styleEroCsvPreview, o.StyleEroSample) &&
            StyleReportInputMatches(stylePairCsvPreview, o.StyleNormalSample) &&
            string.Equals(File.ReadAllText(styleSigPathPreview, Encoding.UTF8), styleSigCurrentPreview, StringComparison.Ordinal);

        if (!steps.CanSkip("04_style", StyleUpToDatePreview))
        {
            log(L("log.previewPrepareStyle"));
            await PrepareStyleWavsAsync(o, pyExe, selectScript, workRoot, pyEnv, paths.StyleRoot, log, ct);
            File.WriteAllText(styleSigPathPreview, styleSigCurrentPreview, new UTF8Encoding(false));
            steps.MarkDone("04_style");
        }

        log(L("log.previewSeedVc"));
        var previewManifest = Path.Combine(paths.PreviewRoot, "preview_manifest.csv");
        var previewSeedReport = Path.Combine(paths.PreviewRoot, "preview_seed_vc_report.csv");
        var previewRows = BuildPreviewManifestFromEtc(previewSourceManifest, previewManifest, log);
        await RunSeedVcAsync(
            o.SeedVc,
            pyExe,
            inferScript,
            seedVcRoot,
            previewManifest,
            styleNormalWavPreview,
            styleEroWavPreview,
            paths.PreviewWavRoot,
            previewSeedReport,
            pyEnv,
            workRoot,
            log,
            ct);

        var previewNormalOut = Path.Combine(paths.PreviewWavRoot, previewRows.NormalRel!.Replace('/', Path.DirectorySeparatorChar));
        var previewEroOut = Path.Combine(paths.PreviewWavRoot, previewRows.EroRel!.Replace('/', Path.DirectorySeparatorChar));
        log($"  preview normal: {previewNormalOut}");
        log($"  preview ero: {previewEroOut}");

        return new PipelineRunResult
        {
            RunRoot = paths.RunRoot,
            PreviewNormalWav = File.Exists(previewNormalOut) ? previewNormalOut : null,
            PreviewEroWav = File.Exists(previewEroOut) ? previewEroOut : null,
        };
    }

    private static async Task RunExtractPhaseAsync(
        PipelineOptions o,
        Action<string> log,
        CancellationToken ct,
        string pid,
        PipelineRunPaths paths,
        PipelineStepTracker steps,
        IReadOnlyList<BundleTarget> targets,
        string classDataPath,
        string vgmstreamCli,
        string workRoot,
        bool requireExistingExtract)
    {
        if (requireExistingExtract)
        {
            var requiredManifest = Path.Combine(paths.BatchRoot, "routing_manifest.csv");
            if (!HasFiles(paths.ExtractFsbRoot, "*.fsb") ||
                !HasFiles(paths.ExtractWavRoot, "*.wav") ||
                !IsManifestReadyForPersonality(requiredManifest, o.TargetPersonalityId, paths.ExtractWavRoot))
            {
                throw new InvalidOperationException(L("error.extractArtifactsMissing"));
            }
            return;
        }

        if (!steps.CanSkip("01_extract", () => HasFiles(paths.ExtractFsbRoot, "*.fsb")))
        {
            log(L("log.extractStep1"));
            foreach (var t in targets)
            {
                if (t.Key == "custom")
                    continue;

                ct.ThrowIfCancellationRequested();
                var srcBundle = BuildSourceBundlePath(o.Hs2Root, pid, t);
                var outDir = Path.Combine(paths.ExtractFsbRoot, t.WavRel.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(outDir);
                if (o.SkipCompletedProcesses && Directory.GetFiles(outDir, "*.fsb", SearchOption.TopDirectoryOnly).Length > 0)
                {
                    log($"  [skip] {t.Key}: already extracted");
                    continue;
                }
                var count = AudioBundleExtractor.ExtractAudioPayloads(srcBundle, classDataPath, outDir);
                log($"  {t.Key}: clips={count}");
            }
            steps.MarkDone("01_extract");
        }

        if (!steps.CanSkip("02_fsb_to_wav", () => HasFiles(paths.ExtractWavRoot, "*.wav")))
        {
            log(L("log.extractStep2"));
            var fsbs = Directory.GetFiles(paths.ExtractFsbRoot, "*.fsb", SearchOption.AllDirectories)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (fsbs.Length == 0)
                throw new InvalidOperationException(L("error.extractedFsbMissing"));

            int converted = 0;
            int skipped = 0;
            foreach (var fsb in fsbs)
            {
                ct.ThrowIfCancellationRequested();
                var rel = Path.GetRelativePath(paths.ExtractFsbRoot, fsb);
                var wav = Path.Combine(paths.ExtractWavRoot, Path.ChangeExtension(rel, ".wav"));
                Directory.CreateDirectory(Path.GetDirectoryName(wav)!);
                if (o.SkipCompletedProcesses && File.Exists(wav))
                {
                    skipped++;
                    continue;
                }
                await ProcessUtil.RunAsync(vgmstreamCli, $"-o \"{wav}\" \"{fsb}\"", workRoot, log, ct);
                converted++;
                if (converted % 200 == 0)
                    log($"  converted={converted}/{fsbs.Length}");
            }
            if (skipped > 0)
                log($"  skipped existing wav={skipped}");
            steps.MarkDone("02_fsb_to_wav");
        }

        var extractManifest = Path.Combine(paths.BatchRoot, "routing_manifest.csv");
        if (!steps.CanSkip("03_manifest", () => IsManifestReadyForPersonality(extractManifest, o.TargetPersonalityId, paths.ExtractWavRoot)))
        {
            log(L("log.extractStep3"));
            BuildRoutingManifest(paths.ExtractWavRoot, extractManifest);
            log($"  manifest: {extractManifest}");
            steps.MarkDone("03_manifest");
        }

        var voiceLineMapPath = Path.Combine(paths.RunRoot, "voice_line_map.csv");
        var existingTextAssetFiles = VoiceLineMapUtil.EnumerateVoiceLineTextAssetFiles(paths.RunRoot);
        if (existingTextAssetFiles.Count == 0)
        {
            log("export voice text assets into run-root");
            var exportedTextAssets = VoiceLineBundleExtractor.ExportVoiceLineTextAssets(
                o.Hs2Root,
                classDataPath,
                paths.RunRoot);
            if (exportedTextAssets > 0)
                log($"  exported voice text assets={exportedTextAssets}");
        }

        log("build voice_line_map.csv from list/h/sound/voice + adv/scenario");
        var textAssetFiles = VoiceLineMapUtil.EnumerateVoiceLineTextAssetFiles(paths.RunRoot);
        var voiceLineMap = VoiceLineMapUtil.BuildVoiceLineMapFromTextAssetFiles(textAssetFiles);
        var advVoiceLineMap = AdvScenarioVoiceLineExtractor.ExtractVoiceLineMap(
            o.Hs2Root,
            classDataPath,
            pid);
        var supplemented = 0;
        foreach (var kv in advVoiceLineMap)
        {
            if (voiceLineMap.ContainsKey(kv.Key))
                continue;
            voiceLineMap[kv.Key] = kv.Value;
            supplemented++;
        }

        if (voiceLineMap.Count > 0)
        {
            VoiceLineMapUtil.SaveVoiceLineMapCsv(voiceLineMapPath, voiceLineMap);
            log($"  voice lines: files={textAssetFiles.Count}, rows={voiceLineMap.Count}, adv_supplemented={supplemented}");
        }
        else
        {
            log($"  [warn] voice lines were not found under {paths.RunRoot}");
        }
    }
}






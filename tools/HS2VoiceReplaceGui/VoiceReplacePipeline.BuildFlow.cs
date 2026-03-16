using System.Text;

namespace HS2VoiceReplace;

internal static partial class VoiceReplacePipeline
{
    // This partial owns the resumable full-build flow after extraction:
    // style wav preparation, Seed-VC execution, bundle rebuild, split zipmods, and optional deploy.
    private static async Task RunBuildAndDeployPhaseAsync(
        PipelineOptions o,
        Action<string> log,
        CancellationToken ct,
        string pid,
        PipelineRunPaths paths,
        PipelineStepTracker steps,
        IReadOnlyList<BundleTarget> targets,
        string pyExe,
        string selectScript,
        string inferScript,
        string seedVcRoot,
        string templateRoot,
        string runtimeDll,
        string patcherExe,
        string classDataPath,
        string workRoot,
        IReadOnlyDictionary<string, string> pyEnv,
        bool sampleSignatureSameAsPrevious,
        string sampleNormalCurrent,
        string sampleEroCurrent)
    {
        var manifest = Path.Combine(paths.BatchRoot, "routing_manifest.csv");
        var styleNormalWav = Path.Combine(paths.StyleRoot, "style_normal.wav");
        var styleEroWav = Path.Combine(paths.StyleRoot, "style_ero.wav");
        var styleNormalCsv = Path.Combine(paths.StyleRoot, "style_normal.csv");
        var styleEroCsv = Path.Combine(paths.StyleRoot, "style_ero.csv");
        var stylePairCsv = Path.Combine(paths.StyleRoot, "style_pair.csv");
        var styleSigPath = Path.Combine(paths.StyleRoot, "style_signature.txt");
        var styleSigCurrent = BuildStyleSignature(o);

        bool StyleUpToDate() =>
            File.Exists(styleNormalWav) &&
            File.Exists(styleEroWav) &&
            File.Exists(styleSigPath) &&
            StyleReportInputMatches(styleNormalCsv, o.StyleNormalSample) &&
            StyleReportInputMatches(styleEroCsv, o.StyleEroSample) &&
            StyleReportInputMatches(stylePairCsv, o.StyleNormalSample) &&
            string.Equals(File.ReadAllText(styleSigPath, Encoding.UTF8), styleSigCurrent, StringComparison.Ordinal);

        if (!steps.CanSkip("04_style", StyleUpToDate))
        {
            log(L("log.buildStep4"));
            if (File.Exists(styleSigPath))
            {
                try
                {
                    var oldSig = File.ReadAllText(styleSigPath, Encoding.UTF8);
                    if (!string.Equals(oldSig, styleSigCurrent, StringComparison.Ordinal))
                        log(L("log.styleInputChanged"));
                }
                catch
                {
                }
            }
            await PrepareStyleWavsAsync(o, pyExe, selectScript, workRoot, pyEnv, paths.StyleRoot, log, ct);
            File.WriteAllText(styleSigPath, styleSigCurrent, new UTF8Encoding(false));
            steps.MarkDone("04_style");
        }

        await RunSeedVcStageAsync(o, log, ct, paths, steps, manifest, styleNormalWav, styleEroWav, styleSigCurrent, pyExe, inferScript, seedVcRoot, workRoot, pyEnv, sampleSignatureSameAsPrevious, sampleNormalCurrent, sampleEroCurrent);

        if (o.SkipCompletedProcesses && !steps.IsDone("06_rebuild") && HasExpectedRebuiltBundlesFresh(targets, paths.ReplaceInputRoot, paths.OutWavRoot))
        {
            steps.MarkDone("06_rebuild");
            log(L("log.skipRebuildArtifacts"));
        }
        if (!steps.CanSkip("06_rebuild", () => HasExpectedRebuiltBundlesFresh(targets, paths.ReplaceInputRoot, paths.OutWavRoot)))
        {
            log(L("log.buildStep6"));
            foreach (var t in targets)
            {
                ct.ThrowIfCancellationRequested();
                var srcBundle = BuildSourceBundlePath(o.SourceHs2Root, pid, t);
                var wavDir = Path.Combine(paths.OutWavRoot, t.WavRel.Replace('/', Path.DirectorySeparatorChar));
                if (!Directory.Exists(wavDir))
                    throw new InvalidOperationException(L("error.convertedWavDirectoryMissing", wavDir));
                if (!HasMatchingPersonalityWavs(wavDir, o.TargetPersonalityId, out var wavTotal, out var wavMatched))
                {
                    throw new InvalidOperationException(
                        L("error.convertedWavPersonalityMismatch", o.TargetPersonalityId.ToString("00"), wavDir, wavTotal, wavMatched));
                }

                var dstBundle = Path.Combine(paths.ReplaceInputRoot, t.DstRel.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(dstBundle)!);
                await ProcessUtil.RunAsync(
                    patcherExe,
                    $"\"{srcBundle}\" \"{classDataPath}\" \"{wavDir}\" \"{dstBundle}\" .wav",
                    workRoot,
                    log,
                    ct);
            }
            steps.MarkDone("06_rebuild");
        }

        if (o.SkipCompletedProcesses && !steps.IsDone("07_split") && HasExpectedSplitZipmodsFresh(targets, paths.SplitOutRoot, pid, paths.ReplaceInputRoot))
        {
            steps.MarkDone("07_split");
            log(L("log.skipSplitArtifacts"));
        }

        List<string> zipmods;
        if (!steps.CanSkip("07_split", () => HasExpectedSplitZipmodsFresh(targets, paths.SplitOutRoot, pid, paths.ReplaceInputRoot)))
        {
            log(L("log.buildStep7"));
            zipmods = BuildSplitZipmods(templateRoot, paths.ReplaceInputRoot, paths.SplitOutRoot, pid, null, targets);
            log($"  built={zipmods.Count}");
            steps.MarkDone("07_split");
        }
        else
        {
            zipmods = Directory.GetFiles(paths.SplitOutRoot, $"HS2VoiceReplace_{pid}_*.zipmod", SearchOption.TopDirectoryOnly)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
            log($"  reuse zipmods={zipmods.Count}");
        }

        if (o.DeployToBackup)
        {
            log(L("log.buildStep8Deploy"));
            Deploy(o, runtimeDll, zipmods, log);
        }
        else
        {
            log(L("log.buildStep8Skip"));
        }
    }

    private static async Task RunSeedVcStageAsync(
        PipelineOptions o,
        Action<string> log,
        CancellationToken ct,
        PipelineRunPaths paths,
        PipelineStepTracker steps,
        string manifest,
        string styleNormalWav,
        string styleEroWav,
        string styleSigCurrent,
        string pyExe,
        string inferScript,
        string seedVcRoot,
        string workRoot,
        IReadOnlyDictionary<string, string> pyEnv,
        bool sampleSignatureSameAsPrevious,
        string sampleNormalCurrent,
        string sampleEroCurrent)
    {
        var seedReport = Path.Combine(paths.RunRoot, "seed_vc_report.csv");
        var seedSigPath = Path.Combine(paths.RunRoot, "seed_vc_signature.txt");
        var seedSigCurrent = BuildSeedVcSignature(o, manifest, styleSigCurrent);
        var seedSigCurrentNormalized = NormalizeSeedVcSignature(seedSigCurrent);
        string seedSigPrevious = "";
        try
        {
            if (File.Exists(seedSigPath))
                seedSigPrevious = File.ReadAllText(seedSigPath, Encoding.UTF8);
        }
        catch
        {
            seedSigPrevious = "";
        }

        var seedSigPreviousNormalized = NormalizeSeedVcSignature(seedSigPrevious);
        var seedSignatureSameAsPrevious =
            !string.IsNullOrWhiteSpace(seedSigPreviousNormalized) &&
            string.Equals(seedSigPreviousNormalized, seedSigCurrentNormalized, StringComparison.Ordinal);

        bool SeedVcUpToDate()
        {
            if (!File.Exists(seedReport)) return false;
            if (!File.Exists(seedSigPath)) return false;
            if (!HasExpectedConvertedWavs(manifest, paths.OutWavRoot)) return false;
            var current = NormalizeSeedVcSignature(File.ReadAllText(seedSigPath, Encoding.UTF8));
            return string.Equals(current, seedSigCurrentNormalized, StringComparison.Ordinal);
        }

        var skipSeedVc = steps.CanSkip("05_seedvc", SeedVcUpToDate);
        if (skipSeedVc)
            return;

        log(L("log.buildStep5SeedVc", o.SeedVc.Engine));
        log($"  seed signature current={ComputeTextSha256(seedSigCurrentNormalized)}");
        if (!string.IsNullOrWhiteSpace(seedSigPreviousNormalized))
            log($"  seed signature previous={ComputeTextSha256(seedSigPreviousNormalized)}");

        var manifestForSeed = manifest;
        var partialSeedReport = seedReport;
        if (o.SkipCompletedProcesses)
        {
            if (!seedSignatureSameAsPrevious)
            {
                log(L("log.seedSignatureChanged"));
            }
            else if (!sampleSignatureSameAsPrevious)
            {
                log(L("log.sampleSignatureChanged"));
            }
            else
            {
                var pendingManifest = Path.Combine(paths.BatchRoot, "routing_manifest_pending.csv");
                var pending = BuildPendingManifestForSeedVc(
                    manifest,
                    paths.OutWavRoot,
                    pendingManifest,
                    paths.RunRoot,
                    sampleNormalCurrent,
                    sampleEroCurrent,
                    allowExistingFileFallbackWithoutSignature: true,
                    log);
                if (pending == 0)
                {
                    log(L("log.skipSeedVcPendingZero"));
                    File.WriteAllText(seedSigPath, seedSigCurrent, new UTF8Encoding(false));
                    steps.MarkDone("05_seedvc");
                    return;
                }

                manifestForSeed = pendingManifest;
                partialSeedReport = Path.Combine(paths.RunRoot, "seed_vc_report_partial.csv");
                log(L("log.resumeSeedVcPendingOnly", pending));
            }
        }

        File.WriteAllText(seedSigPath, seedSigCurrent, new UTF8Encoding(false));

        try
        {
            await RunSeedVcChunkedAsync(
                o.SeedVc,
                pyExe,
                inferScript,
                seedVcRoot,
                manifestForSeed,
                styleNormalWav,
                styleEroWav,
                paths.OutWavRoot,
                partialSeedReport,
                pyEnv,
                workRoot,
                log,
                ct);
        }
        catch (Exception ex)
        {
            if (ex is OperationCanceledException || ct.IsCancellationRequested)
                throw;

            if (File.Exists(seedReport))
            {
                var firstFailed = File.ReadLines(seedReport)
                    .Skip(1)
                    .FirstOrDefault(line => line.Contains(",failed,", StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(firstFailed))
                    log("  first failed row: " + firstFailed);
                throw new InvalidOperationException(L("error.seedVcFailedWithReport", seedReport), ex);
            }

            throw new InvalidOperationException(
                L("error.seedVcFailedWithoutReport", ex.Message),
                ex);
        }

        if (!string.Equals(partialSeedReport, seedReport, StringComparison.OrdinalIgnoreCase))
        {
            MergeSeedReports(manifest, paths.OutWavRoot, seedReport, partialSeedReport);
            log("  merged partial report into seed_vc_report.csv");
        }
        UpdateSampleSignatureMapFromProcessedManifest(
            paths.RunRoot,
            manifest,
            manifestForSeed,
            paths.OutWavRoot,
            sampleNormalCurrent,
            sampleEroCurrent);
        File.WriteAllText(seedSigPath, seedSigCurrent, new UTF8Encoding(false));
        steps.MarkDone("05_seedvc");
        steps.ClearDone("06_rebuild", "07_split");
    }
}






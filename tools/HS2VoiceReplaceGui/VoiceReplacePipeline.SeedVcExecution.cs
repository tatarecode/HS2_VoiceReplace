using System.Globalization;
using System.Text;

namespace HS2VoiceReplace;

internal static partial class VoiceReplacePipeline
{
    // Seed-VC execution is isolated because it owns the most failure-prone external process flow:

    // argument construction, chunked execution, report merge, and runtime probing.

private static async Task LogTorchRuntimeAsync(string pyExe, string workDir, Action<string> log, CancellationToken ct, IReadOnlyDictionary<string, string>? env)
    {
        try
        {
            const string probe = "-c \"import torch; print('[torch] ver=' + str(torch.__version__) + ' cuda=' + str(torch.cuda.is_available()) + ' cuda_ver=' + str(torch.version.cuda))\"";
            await ProcessUtil.RunAsync(pyExe, probe, workDir, log, ct, env);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log("[warn] torch runtime probe failed: " + ex.Message);
        }
    }
    private static async Task RunSeedVcAsync(
        SeedVcUiSettings seed,
        string pyExe,
        string inferScript,
        string seedVcRoot,
        string manifest,
        string styleNormalWav,
        string styleEroWav,
        string outWavRoot,
        string seedReport,
        IReadOnlyDictionary<string, string> pyEnv,
        string workRoot,
        Action<string> log,
        CancellationToken ct,
        int progressOffset = 0,
        int progressTotal = 0)
    {
        await LogTorchRuntimeAsync(pyExe, workRoot, log, ct, pyEnv);

        string F(double v) => v.ToString("0.######", CultureInfo.InvariantCulture);
        var args = new List<string>
        {
            $"\"{inferScript}\"",
            $"--seed-root \"{seedVcRoot}\"",
            $"--manifest \"{manifest}\"",
            $"--style-normal \"{styleNormalWav}\"",
            $"--style-ero \"{styleEroWav}\"",
            $"--out-root \"{outWavRoot}\"",
            $"--report \"{seedReport}\"",
            $"--diffusion-steps {seed.DiffusionSteps}",
            $"--length-adjust {F(seed.LengthAdjust)}",
            $"--intelligibility-cfg-rate {F(seed.IntelligibilityCfgRate)}",
            $"--similarity-cfg-rate {F(seed.SimilarityCfgRate)}",
            $"--top-p {F(seed.TopP)}",
            $"--temperature {F(seed.Temperature)}",
            $"--repetition-penalty {F(seed.RepetitionPenalty)}",
            "--on-error copy-source",
            "--report-every 1",
        };
        if (progressOffset > 0) args.Add($"--progress-offset {progressOffset}");
        if (progressTotal > 0) args.Add($"--progress-total {progressTotal}");

        if (seed.NrStylePre) args.Add("--nr-style-pre");
        if (seed.NrOutPost) args.Add("--nr-out-post");
        if (seed.NrStylePre || seed.NrOutPost)
        {
            args.Add($"--nr-style-prop-decrease {F(seed.NrStylePropDecrease)}");
            args.Add($"--nr-out-prop-decrease {F(seed.NrOutPropDecrease)}");
            args.Add($"--nr-time-mask-smooth-ms {F(seed.NrTimeMaskSmoothMs)}");
            args.Add($"--nr-freq-mask-smooth-hz {F(seed.NrFreqMaskSmoothHz)}");
        }

        if (seed.HarshFix)
        {
            args.Add("--harsh-fix");
            args.Add($"--harsh-hf-cutoff {F(seed.HarshHfCutoff)}");
            args.Add($"--harsh-src-hf-mix {F(seed.HarshSrcHfMix)}");
            args.Add($"--harsh-over-factor {F(seed.HarshOverFactor)}");
            args.Add($"--harsh-flatness-th {F(seed.HarshFlatnessTh)}");
            args.Add($"--harsh-min-segment-ms {F(seed.HarshMinSegmentMs)}");
        }

        if (seed.BreathPassThrough)
        {
            args.Add("--breath-pass-through");
            args.Add($"--breath-flatness-th {F(seed.BreathFlatnessTh)}");
            args.Add($"--breath-rms-max {F(seed.BreathRmsMax)}");
            args.Add($"--breath-mix {F(seed.BreathMix)}");
        }

        if (seed.GlobalHfBlend)
        {
            args.Add("--global-hf-blend");
            args.Add($"--global-hf-cutoff {F(seed.GlobalHfCutoff)}");
            args.Add($"--global-hf-src-mix {F(seed.GlobalHfSrcMix)}");
        }

        if (seed.GlobalDeEsser)
        {
            args.Add("--global-deesser");
            args.Add($"--deesser-low-hz {F(seed.DeEsserLowHz)}");
            args.Add($"--deesser-high-hz {F(seed.DeEsserHighHz)}");
            args.Add($"--deesser-strength {F(seed.DeEsserStrength)}");
        }

        await ProcessUtil.RunAsync(pyExe, string.Join(" ", args), seedVcRoot, log, ct, pyEnv);
    }

    private static async Task RunSeedVcChunkedAsync(
        SeedVcUiSettings seed,
        string pyExe,
        string inferScript,
        string seedVcRoot,
        string manifest,
        string styleNormalWav,
        string styleEroWav,
        string outWavRoot,
        string seedReport,
        IReadOnlyDictionary<string, string> pyEnv,
        string workRoot,
        Action<string> log,
        CancellationToken ct)
    {
        const int chunkSize = 200;
        var rows = LoadManifestRows(manifest)
            .Where(r => !string.IsNullOrWhiteSpace(r.RelativePath))
            .ToList();
        if (rows.Count == 0)
        {
            File.WriteAllLines(
                seedReport,
                new[] { "relative_path,bucket,source_file,style_file,output_file,status,exit_code,note" },
                new UTF8Encoding(false));
            return;
        }

        if (rows.Count <= chunkSize)
        {
            await RunSeedVcAsync(
                seed, pyExe, inferScript, seedVcRoot, manifest, styleNormalWav, styleEroWav, outWavRoot, seedReport, pyEnv, workRoot, log, ct);
            return;
        }

        var totalChunks = (rows.Count + chunkSize - 1) / chunkSize;
        log($"  Seed-VC chunk mode: total={rows.Count}, chunk={chunkSize}, chunks={totalChunks}");

        var chunkRoot = Path.Combine(Path.GetDirectoryName(seedReport) ?? Path.GetTempPath(), "_seedvc_chunks");
        Directory.CreateDirectory(chunkRoot);
        var sessionRoot = Path.Combine(chunkRoot, DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(sessionRoot);

        var chunkReports = new List<string>(totalChunks);
        for (int chunk = 0; chunk < totalChunks; chunk++)
        {
            ct.ThrowIfCancellationRequested();

            var chunkRows = rows.Skip(chunk * chunkSize).Take(chunkSize).ToList();
            var chunkManifest = Path.Combine(sessionRoot, $"manifest_{chunk + 1:0000}.csv");
            var chunkReport = Path.Combine(sessionRoot, $"report_{chunk + 1:0000}.csv");
            var rangeStart = chunk * chunkSize + 1;
            var rangeEnd = chunk * chunkSize + chunkRows.Count;

            var lines = new List<string>(chunkRows.Count + 1) { "relative_path,model_bucket,source_file" };
            foreach (var r in chunkRows)
                lines.Add($"\"{r.RelativePath}\",\"{r.Bucket}\",\"{r.SourceFile.Replace("\"", "\"\"")}\"");
            File.WriteAllLines(chunkManifest, lines, new UTF8Encoding(false));

            try
            {
                log($"  chunk {chunk + 1}/{totalChunks} start rows={rangeStart}-{rangeEnd}/{rows.Count}");
                await RunSeedVcAsync(
                    seed,
                    pyExe,
                    inferScript,
                    seedVcRoot,
                    chunkManifest,
                    styleNormalWav,
                    styleEroWav,
                    outWavRoot,
                    chunkReport,
                    pyEnv,
                    workRoot,
                    log,
                    ct,
                    progressOffset: chunk * chunkSize,
                    progressTotal: rows.Count);
                chunkReports.Add(chunkReport);
                log($"  chunk {chunk + 1}/{totalChunks} done rows={rangeStart}-{rangeEnd}/{rows.Count}");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new InvalidOperationException(
                    L("error.seedVcChunkFailed", $"{chunk + 1}/{totalChunks}", chunkManifest, chunkReport),
                    ex);
            }
        }

        MergeChunkReports(chunkReports, seedReport);
        log($"  merged chunk reports: {seedReport}");
    }

    private static void MergeChunkReports(IReadOnlyList<string> chunkReports, string mergedReport)
    {
        var lines = new List<string> { "relative_path,bucket,source_file,style_file,output_file,status,exit_code,note" };
        foreach (var report in chunkReports)
        {
            if (!File.Exists(report))
                continue;
            foreach (var line in File.ReadLines(report).Skip(1))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                lines.Add(line);
            }
        }
        File.WriteAllLines(mergedReport, lines, new UTF8Encoding(false));
    }

    private static int BuildPendingManifestForSeedVc(
        string fullManifestCsv,
        string outWavRoot,
        string pendingManifestCsv,
        string runRoot,
        string currentNormalSig,
        string currentEroSig,
        bool allowExistingFileFallbackWithoutSignature,
        Action<string> log)
    {
        var rows = LoadManifestRows(fullManifestCsv);
        var sigMap = LoadSignatureMapRows(runRoot, fullManifestCsv, outWavRoot);
        var outLines = new List<string> { "relative_path,model_bucket,source_file" };
        var (pendingRows, summary) = VoiceReplaceFreshnessUtil.BuildPendingRows(
            rows.Select(row => (row.RelativePath, row.Bucket, row.SourceFile)),
            outWavRoot,
            sigMap,
            currentNormalSig,
            currentEroSig,
            allowExistingFileFallbackWithoutSignature);
        foreach (var row in pendingRows)
            outLines.Add($"\"{row.RelativePath}\",\"{row.Bucket}\",\"{row.SourceFile.Replace("\"", "\"\"")}\"");
        File.WriteAllLines(pendingManifestCsv, outLines, new UTF8Encoding(false));
        var pending = Math.Max(0, outLines.Count - 1);
        log($"  seedvc pending manifest: {pendingManifestCsv} (rows={pending})");
        log($"  pending reason summary: missing_file_only={summary.MissingFileOnly}, sig_mismatch_only={summary.SigMismatchOnly}, missing_and_sig_mismatch={summary.MissingAndSigMismatch}");
        return pending;
    }

    private static void MergeSeedReports(string fullManifestCsv, string outWavRoot, string mergedReportCsv, string partialReportCsv)
    {
        var statusMap = new Dictionary<string, (string Status, string Note)>(StringComparer.OrdinalIgnoreCase);

        void Load(string path)
        {
            if (!File.Exists(path))
                return;
            foreach (var entry in VoiceReplaceReportUtil.ParseSeedReportStatusMap(File.ReadLines(path)))
                statusMap[entry.Key] = entry.Value;
        }

        Load(mergedReportCsv);
        if (!string.Equals(mergedReportCsv, partialReportCsv, StringComparison.OrdinalIgnoreCase))
            Load(partialReportCsv);

        var outLines = VoiceReplaceReportUtil.BuildMergedSeedReportLines(
            LoadManifestRows(fullManifestCsv).Select(row => (row.RelativePath, row.Bucket, row.SourceFile)),
            outWavRoot,
            statusMap);
        VoiceReplaceReportUtil.WriteMergedSeedReport(mergedReportCsv, outLines);
    }
}














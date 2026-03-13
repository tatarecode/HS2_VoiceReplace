using System.Globalization;
using System.Text;

namespace HS2VoiceReplace;

internal static partial class VoiceReplacePipeline
{
    // These path objects collect the derived run-root layout in one place so the
    // orchestration code does not need to rebuild the same directory tree ad hoc.
    private sealed record PipelineRunPaths(
        string RunRoot,
        string ExtractFsbRoot,
        string ExtractWavRoot,
        string BatchRoot,
        string OutWavRoot,
        string ReplaceInputRoot,
        string SplitOutRoot,
        string StyleRoot,
        string PreviewRoot,
        string PreviewExtractFsbRoot,
        string PreviewExtractWavRoot,
        string PreviewBatchRoot,
        string PreviewWavRoot,
        string DoneRoot);

    // Step tracking is reused by extract/build flows and keeps the skip semantics in one place.
    private sealed class PipelineStepTracker
    {
        private readonly PipelineOptions _options;
        private readonly Action<string> _log;
        private readonly string _doneRoot;

        public PipelineStepTracker(PipelineOptions options, string doneRoot, Action<string> log)
        {
            _options = options;
            _doneRoot = doneRoot;
            _log = log;
        }

        public bool IsDone(string step) => File.Exists(GetDoneFilePath(step));

        public void MarkDone(string step)
            => File.WriteAllText(GetDoneFilePath(step), DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture), Encoding.UTF8);

        public void ClearDone(params string[] steps)
        {
            foreach (var step in steps)
            {
                var path = GetDoneFilePath(step);
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        public bool CanSkip(string step, Func<bool> ready)
        {
            if (!_options.SkipCompletedProcesses)
                return false;
            if (!IsDone(step))
                return false;
            if (!ready())
                return false;
            _log(L("log.skipCompletedStep", step));
            return true;
        }

        private string GetDoneFilePath(string step) => Path.Combine(_doneRoot, $"{step}.done");
    }

    private static PipelineRunPaths BuildRunPaths(PipelineOptions o, string pid)
    {
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var runId = o.SkipCompletedProcesses ? $"resume_{pid}" : $"{stamp}_{pid}";
        var runRoot = string.IsNullOrWhiteSpace(o.ResumeRunRoot)
            ? Path.Combine(o.OutputBaseRoot, "gui_runs", runId)
            : Path.GetFullPath(o.ResumeRunRoot);
        var previewRoot = Path.Combine(runRoot, "preview");

        return new PipelineRunPaths(
            runRoot,
            Path.Combine(runRoot, "voice_extract", pid),
            Path.Combine(runRoot, "voice_extract_wav", pid),
            Path.Combine(runRoot, "rvc_batches"),
            Path.Combine(runRoot, "voice_replace_wav"),
            Path.Combine(runRoot, "voice_replace_input"),
            Path.Combine(runRoot, "voice_replace_mod_split"),
            Path.Combine(runRoot, "style"),
            previewRoot,
            Path.Combine(previewRoot, "voice_extract", pid),
            Path.Combine(previewRoot, "voice_extract_wav", pid),
            Path.Combine(previewRoot, "rvc_batches"),
            Path.Combine(previewRoot, "voice_preview_wav"),
            Path.Combine(runRoot, "_done"));
    }

    private static void EnsureRunDirectories(PipelineRunPaths paths)
    {
        Directory.CreateDirectory(paths.ExtractFsbRoot);
        Directory.CreateDirectory(paths.ExtractWavRoot);
        Directory.CreateDirectory(paths.BatchRoot);
        Directory.CreateDirectory(paths.OutWavRoot);
        Directory.CreateDirectory(paths.ReplaceInputRoot);
        Directory.CreateDirectory(paths.SplitOutRoot);
        Directory.CreateDirectory(paths.StyleRoot);
        Directory.CreateDirectory(paths.PreviewRoot);
        Directory.CreateDirectory(paths.PreviewExtractFsbRoot);
        Directory.CreateDirectory(paths.PreviewExtractWavRoot);
        Directory.CreateDirectory(paths.PreviewBatchRoot);
        Directory.CreateDirectory(paths.PreviewWavRoot);
        Directory.CreateDirectory(paths.DoneRoot);
    }

    private static bool HasFiles(string root, string pattern)
        => Directory.Exists(root) && Directory.GetFiles(root, pattern, SearchOption.AllDirectories).Length > 0;

    private static void ResetDir(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, true);
        Directory.CreateDirectory(path);
    }

    private static (string normal, string ero) ReadSampleSignatures(string csvPath)
    {
        if (!File.Exists(csvPath))
            return ("", "");
        try
        {
            string normal = "";
            string ero = "";
            foreach (var line in File.ReadLines(csvPath).Skip(1))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                var cols = ParseCsvLine(line);
                if (cols.Count < 2)
                    continue;
                var kind = (cols[0] ?? "").Trim().ToLowerInvariant();
                var sig = (cols[1] ?? "").Trim();
                if (kind == "normal")
                    normal = sig;
                if (kind == "ero")
                    ero = sig;
            }
            return (normal, ero);
        }
        catch
        {
            return ("", "");
        }
    }

    private static string BuildBundleSelectionSignature(PipelineOptions options, string pid, IReadOnlyList<BundleTarget> targets)
    {
        var sb = new StringBuilder();
        foreach (var target in targets.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            var srcBundle = BuildSourceBundlePath(options.SourceHs2Root, pid, target);
            long len = 0;
            long ticks = 0;
            try
            {
                var fi = new FileInfo(srcBundle);
                if (fi.Exists)
                {
                    len = fi.Length;
                    ticks = fi.LastWriteTimeUtc.Ticks;
                }
            }
            catch
            {
            }

            sb.Append(target.Key).Append('|')
              .Append(target.SrcRel.Replace('\\', '/')).Append('|')
              .Append(srcBundle).Append('|')
              .Append(len).Append('|')
              .Append(ticks)
              .AppendLine();
        }

        return ComputeTextSha256(sb.ToString());
    }
}



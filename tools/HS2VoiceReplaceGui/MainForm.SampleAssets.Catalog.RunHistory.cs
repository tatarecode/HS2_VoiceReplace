using System.Globalization;
using System.Text;

namespace HS2VoiceReplace;

public sealed partial class MainForm
{
    // Recover the latest known source range from prior style manifests under the generic gui_runs
    // directory. This keeps the catalog self-describing without importing obsolete user-specific
    // settings blobs.
    private StyleSegmentSelection? ResolveSegmentFromRunHistory(string? sourceFile, bool preferEro)
    {
        if (string.IsNullOrWhiteSpace(sourceFile))
            return null;
        string srcFull;
        try { srcFull = Path.GetFullPath(sourceFile); }
        catch { return null; }

        var runRoot = Path.Combine(_activeOutputRoot, "gui_runs");
        if (!Directory.Exists(runRoot))
            return null;

        foreach (var kind in preferEro ? new[] { "style_ero.csv", "style_pair.csv", "style_normal.csv" } : new[] { "style_normal.csv", "style_pair.csv", "style_ero.csv" })
        {
            // Scan the most recent style manifests first so restored assets inherit the latest known range.
            var files = Directory.GetFiles(runRoot, kind, SearchOption.AllDirectories)
                .Select(p => new FileInfo(p))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Take(200);

            foreach (var fi in files)
            {
                var seg = TryReadSegmentFromStyleCsv(fi.FullName, srcFull, preferEro);
                if (seg != null)
                    return seg;
            }
        }
        return null;
    }

    private static StyleSegmentSelection? TryReadSegmentFromStyleCsv(string csvPath, string sourceFileFull, bool preferEro)
    {
        var lines = File.ReadAllLines(csvPath, Encoding.UTF8);
        if (lines.Length < 2)
            return null;
        var header = ParseCsvLineSimple(lines[0]);
        if (header.Count == 0)
            return null;

        int Idx(string name)
        {
            for (var i = 0; i < header.Count; i++)
            {
                if (string.Equals(header[i], name, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }

        var idxInput = Idx("input");
        var idxStart = Idx("start_sec");
        var idxDuration = Idx("duration_sec");
        if (idxInput < 0 || idxStart < 0 || idxDuration < 0)
            return null;

        var idxRole = Idx("role");
        foreach (var raw in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;
            var cols = ParseCsvLineSimple(raw);
            if (cols.Count <= Math.Max(idxInput, Math.Max(idxStart, idxDuration)))
                continue;

            var input = cols[idxInput].Trim();
            if (string.IsNullOrWhiteSpace(input))
                continue;
            string inputFull;
            try { inputFull = Path.GetFullPath(input); }
            catch { continue; }
            if (!string.Equals(inputFull, sourceFileFull, StringComparison.OrdinalIgnoreCase))
                continue;

            if (idxRole >= 0 && idxRole < cols.Count)
            {
                var role = (cols[idxRole] ?? "").Trim().ToLowerInvariant();
                if (role == "normal" && preferEro) continue;
                if (role == "ero" && !preferEro) continue;
            }

            if (!double.TryParse(cols[idxStart], NumberStyles.Float, CultureInfo.InvariantCulture, out var st))
                continue;
            if (!double.TryParse(cols[idxDuration], NumberStyles.Float, CultureInfo.InvariantCulture, out var du))
                continue;
            if (du <= 0)
                continue;

            return new StyleSegmentSelection
            {
                SourceFile = sourceFileFull,
                StartSec = Math.Max(0, st),
                DurationSec = du,
            };
        }
        return null;
    }

    private static List<string> ParseCsvLineSimple(string line)
    {
        var cols = new List<string>();
        var sb = new StringBuilder();
        var inQ = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQ && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else
                {
                    inQ = !inQ;
                }
                continue;
            }
            if (c == ',' && !inQ)
            {
                cols.Add(sb.ToString());
                sb.Clear();
                continue;
            }
            sb.Append(c);
        }
        cols.Add(sb.ToString());
        return cols;
    }
}


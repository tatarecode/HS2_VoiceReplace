using System.Text;

namespace HS2VoiceReplace;

// Provides pure CSV merge helpers for resumable Seed-VC report aggregation.
internal static class VoiceReplaceReportUtil
{
    public static Dictionary<string, (string Status, string Note)> ParseSeedReportStatusMap(IEnumerable<string> lines)
    {
        var statusMap = new Dictionary<string, (string Status, string Note)>(StringComparer.OrdinalIgnoreCase);
        var materialized = lines.ToList();
        if (materialized.Count <= 1)
            return statusMap;

        var header = PartialRebuildGridDataUtil.ParseCsvLine(materialized[0]);
        var relIdx = header.FindIndex(h => string.Equals(h, "relative_path", StringComparison.OrdinalIgnoreCase));
        var statusIdx = header.FindIndex(h => string.Equals(h, "status", StringComparison.OrdinalIgnoreCase));
        var noteIdx = header.FindIndex(h => string.Equals(h, "note", StringComparison.OrdinalIgnoreCase));
        if (relIdx < 0 || statusIdx < 0)
            return statusMap;

        foreach (var line in materialized.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            var cols = PartialRebuildGridDataUtil.ParseCsvLine(line);
            if (cols.Count <= Math.Max(relIdx, statusIdx))
                continue;
            var rel = cols[relIdx];
            if (string.IsNullOrWhiteSpace(rel))
                continue;
            var note = noteIdx >= 0 && cols.Count > noteIdx ? cols[noteIdx] : "";
            statusMap[rel] = (cols[statusIdx], note);
        }

        return statusMap;
    }

    public static List<string> BuildMergedSeedReportLines(
        IEnumerable<(string RelativePath, string Bucket, string SourceFile)> manifestRows,
        string outWavRoot,
        IReadOnlyDictionary<string, (string Status, string Note)> mergedStatusMap)
    {
        var outLines = new List<string> { "relative_path,model_bucket,source_file,status,note" };
        foreach (var row in manifestRows.OrderBy(r => r.RelativePath, StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(row.RelativePath))
                continue;

            var dst = Path.Combine(outWavRoot, row.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            var (status, note) = mergedStatusMap.TryGetValue(row.RelativePath, out var st)
                ? st
                : (File.Exists(dst) ? "ok" : "pending", "");

            outLines.Add(
                $"\"{row.RelativePath}\",\"{row.Bucket}\",\"{row.SourceFile.Replace("\"", "\"\"")}\",\"{status.Replace("\"", "\"\"")}\",\"{note.Replace("\"", "\"\"")}\"");
        }
        return outLines;
    }

    public static void WriteMergedSeedReport(string path, IReadOnlyList<string> lines)
    {
        File.WriteAllLines(path, lines, new UTF8Encoding(false));
    }
}


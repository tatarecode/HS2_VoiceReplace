namespace HS2VoiceReplace;

// Parses lightweight status CSV files used by the partial rebuild grid.
internal static class GridStatusMapUtil
{
    public static Dictionary<string, string> ParseStatusMap(IEnumerable<string> lines)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var materialized = lines.ToList();
        if (materialized.Count <= 1)
            return map;

        var headers = PartialRebuildGridDataUtil.ParseCsvLine(materialized[0]);
        var relIdx = headers.FindIndex(h => string.Equals(h, "relative_path", StringComparison.OrdinalIgnoreCase));
        var statusIdx = headers.FindIndex(h => string.Equals(h, "status", StringComparison.OrdinalIgnoreCase));
        if (relIdx < 0 || statusIdx < 0)
            return map;

        foreach (var line in materialized.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            var cols = PartialRebuildGridDataUtil.ParseCsvLine(line);
            if (cols.Count <= Math.Max(relIdx, statusIdx))
                continue;
            var rel = cols[relIdx].Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(rel))
                continue;
            map[rel] = cols[statusIdx];
        }

        return map;
    }
}


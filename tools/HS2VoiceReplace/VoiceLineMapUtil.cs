using System.Text;

namespace HS2VoiceReplace;

// Handles parsing and serialization of the optional voice-line lookup cache.
internal static class VoiceLineMapUtil
{
    public static Dictionary<string, string> ParseVoiceLineMapCsv(IEnumerable<string> lines)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            var cols = PartialRebuildGridDataUtil.ParseCsvLine(line);
            if (cols.Count < 2)
                continue;
            var rel = (cols[0] ?? "").Replace('\\', '/');
            var text = cols[1] ?? "";
            if (string.IsNullOrWhiteSpace(rel) || string.IsNullOrWhiteSpace(text))
                continue;
            map[rel] = text;
        }
        return map;
    }

    public static List<string> SerializeVoiceLineMapCsv(IReadOnlyDictionary<string, string> map)
    {
        var lines = new List<string>(map.Count + 1) { "relative_path,voice_line" };
        foreach (var kv in map.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            lines.Add($"\"{kv.Key}\",\"{kv.Value.Replace("\"", "\"\"")}\"");
        return lines;
    }

    public static Dictionary<string, string> BuildVoiceLineMapFromTextAssetLines(IEnumerable<string> rawLines)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in rawLines)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;
            var cols = raw.Split('\t');
            if (cols.Length < 4)
                continue;

            var lineText = (cols[0] ?? "").Trim();
            var bundlePath = (cols[2] ?? "").Trim().Replace('\\', '/');
            var clipName = (cols[3] ?? "").Trim();
            if (string.IsNullOrWhiteSpace(lineText) || string.IsNullOrWhiteSpace(bundlePath) || string.IsNullOrWhiteSpace(clipName))
                continue;

            if (!PartialRebuildGridDataUtil.TryBuildRelativePathFromVoiceTextAsset(bundlePath, clipName, out var rel))
                continue;

            if (!map.ContainsKey(rel))
                map[rel] = lineText;
        }
        return map;
    }

    public static void SaveVoiceLineMapCsv(string path, IReadOnlyDictionary<string, string> map)
    {
        File.WriteAllLines(path, SerializeVoiceLineMapCsv(map), new UTF8Encoding(false));
    }
}


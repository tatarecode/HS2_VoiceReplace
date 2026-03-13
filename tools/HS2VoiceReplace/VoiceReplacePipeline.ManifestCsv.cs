using System.Text;

namespace HS2VoiceReplace;

internal static partial class VoiceReplacePipeline
{
    // Manifest CSV helpers are grouped here because they define the pipeline's stable contract

    // between extract, conversion, rebuild, and preview stages.

private static List<ManifestRow> LoadManifestRows(string manifestCsv)
    {
        if (!File.Exists(manifestCsv))
            throw new FileNotFoundException(manifestCsv);

        var rows = new List<ManifestRow>();
        foreach (var line in File.ReadLines(manifestCsv).Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            var cols = ParseCsvLine(line);
            if (cols.Count < 3)
                continue;
            rows.Add(new ManifestRow(cols[0], cols[1], cols[2]));
        }
        return rows;
    }

    private static bool IsUnderRoot(string filePath, string rootPath)
    {
        var full = Path.GetFullPath(filePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var root = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(full, root, StringComparison.OrdinalIgnoreCase);
    }

    private readonly record struct PreviewManifestPick(string? NormalRel, string? EroRel);

    private static PreviewManifestPick BuildPreviewManifestFromEtc(string srcManifestCsv, string dstManifestCsv, Action<string> log)
    {
        if (!File.Exists(srcManifestCsv))
            throw new FileNotFoundException(srcManifestCsv);

        var rows = new List<(string rel, string bucket, string src)>();
        foreach (var line in File.ReadLines(srcManifestCsv).Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            var cols = ParseCsvLine(line);
            if (cols.Count < 3)
                continue;
            rows.Add((cols[0], cols[1], cols[2]));
        }

        var etc = rows
            .Where(r => r.rel.StartsWith("etc/", StringComparison.OrdinalIgnoreCase))
            .OrderBy(r => r.rel, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (etc.Count == 0)
            throw new InvalidOperationException(L("error.previewEtcWavMissing"));

        var source = etc[0].src;
        const string normalRel = "preview/preview_normal.wav";
        const string eroRel = "preview/preview_ero.wav";
        var outLines = new List<string> { "relative_path,model_bucket,source_file" };
        outLines.Add($"\"{normalRel}\",\"normal\",\"{source.Replace("\"", "\"\"")}\"");
        outLines.Add($"\"{eroRel}\",\"ero\",\"{source.Replace("\"", "\"\"")}\"");
        File.WriteAllLines(dstManifestCsv, outLines, new UTF8Encoding(false));
        log($"  preview manifest(/etc only): {dstManifestCsv}");
        return new PreviewManifestPick(normalRel, eroRel);
    }

    private static List<string> ParseCsvLine(string line)
    {
        var cols = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            else
            {
                if (c == ',')
                {
                    cols.Add(sb.ToString());
                    sb.Clear();
                }
                else if (c == '"')
                {
                    inQuotes = true;
                }
                else
                {
                    sb.Append(c);
                }
            }
        }
        cols.Add(sb.ToString());
        return cols;
    }

    private static void BuildRoutingManifest(string wavRoot, string manifestCsv)
    {
        var lines = new List<string> { "relative_path,model_bucket,source_file" };
        var wavs = Directory.GetFiles(wavRoot, "*.wav", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
        foreach (var wav in wavs)
        {
            var rel = Path.GetRelativePath(wavRoot, wav).Replace('\\', '/');
            var bucket = rel.StartsWith("h/", StringComparison.OrdinalIgnoreCase) ? "ero" : "normal";
            lines.Add($"\"{rel}\",\"{bucket}\",\"{wav.Replace("\"", "\"\"")}\"");
        }
        File.WriteAllLines(manifestCsv, lines, Encoding.UTF8);
    }
}












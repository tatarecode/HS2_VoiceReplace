using System.Text;

namespace HS2VoiceReplace;

// Provides pure CSV/status helpers for the partial rebuild grid without requiring a live dialog instance.
internal static class PartialRebuildGridDataUtil
{
    internal readonly record struct RunSampleSignatures(string Normal, string Ero);
    internal readonly record struct RowSampleSignature(string Normal, string Ero, string Used);

    public static string PreferNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }
        return "";
    }

    public static RowSampleSignature ResolveDisplayedRowSampleSignatures(
        string bucket,
        RunSampleSignatures runSignatures,
        RowSampleSignature? mapSignatures,
        RowSampleSignature? previousSignatures = null)
    {
        var normal = PreferNonEmpty(
            mapSignatures?.Normal,
            runSignatures.Normal,
            previousSignatures?.Normal);

        var ero = PreferNonEmpty(
            mapSignatures?.Ero,
            runSignatures.Ero,
            previousSignatures?.Ero);

        var bucketNormalized = string.Equals(bucket, "ero", StringComparison.OrdinalIgnoreCase) ? "ero" : "normal";
        var fallbackUsed = bucketNormalized == "ero" ? ero : normal;
        var used = PreferNonEmpty(
            mapSignatures?.Used,
            fallbackUsed,
            previousSignatures?.Used);

        return new RowSampleSignature(normal, ero, used);
    }

    public static List<string> ParseCsvLine(string line)
    {
        var cols = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;
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

    public static bool TryBuildRelativePathFromVoiceTextAsset(string bundlePath, string clipName, out string relativePath)
    {
        relativePath = "";
        if (string.IsNullOrWhiteSpace(bundlePath) || string.IsNullOrWhiteSpace(clipName))
            return false;

        var normalized = bundlePath.Replace('\\', '/');
        const string key = "sound/data/pcm/";
        var keyPos = normalized.IndexOf(key, StringComparison.OrdinalIgnoreCase);
        if (keyPos < 0)
            return false;

        var tail = normalized[(keyPos + key.Length)..];
        var slash = tail.IndexOf('/');
        if (slash < 0 || slash + 1 >= tail.Length)
            return false;

        var sub = tail[(slash + 1)..];
        var lastSlash = sub.LastIndexOf('/');
        if (lastSlash <= 0)
            return false;
        var relDir = sub[..lastSlash];
        if (string.IsNullOrWhiteSpace(relDir))
            return false;

        var clip = clipName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)
            ? clipName
            : clipName + ".wav";
        relativePath = (relDir + "/" + clip).Replace('\\', '/');
        return true;
    }

    public static string BuildDisplayStatus(UiLanguage language, string rawStatus, bool convertedExists)
    {
        var s = (rawStatus ?? "").Trim().ToLowerInvariant();
        return s switch
        {
            "ok" => UiTextCatalog.Get(language, "status.ok"),
            "fallback_src" => UiTextCatalog.Get(language, "status.fallbackSrc"),
            "fallback_silence" => UiTextCatalog.Get(language, "status.fallbackSilence"),
            "failed" => UiTextCatalog.Get(language, "status.failedRebuild"),
            "start" => UiTextCatalog.Get(language, "dialog.partialGrid.status.converting"),
            "pending" => convertedExists ? UiTextCatalog.Get(language, "status.pendingWithOutput") : UiTextCatalog.Get(language, "status.pending"),
            "" => convertedExists ? UiTextCatalog.Get(language, "status.unknownWithOutput") : UiTextCatalog.Get(language, "status.pending"),
            _ => convertedExists ? UiTextCatalog.Get(language, "status.outputWithRaw", s) : UiTextCatalog.Get(language, "status.pendingWithRaw", s),
        };
    }

    public static RunSampleSignatures ParseRunLevelSampleSignatures(IEnumerable<string> lines)
    {
        string n = "";
        string e = "";
        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            var cols = ParseCsvLine(line);
            if (cols.Count < 2)
                continue;
            var kind = (cols[0] ?? "").Trim().ToLowerInvariant();
            var sig = (cols[1] ?? "").Trim();
            if (kind == "normal") n = sig;
            if (kind == "ero") e = sig;
        }
        return new RunSampleSignatures(n, e);
    }

    public static Dictionary<string, RowSampleSignature> ParseSampleSignatureMap(IEnumerable<string> lines)
    {
        var map = new Dictionary<string, RowSampleSignature>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            var cols = ParseCsvLine(line);
            if (cols.Count < 6)
                continue;
            var rel = cols[0].Replace('\\', '/');
            var sigN = cols[3];
            var sigE = cols[4];
            var sigU = cols[5];
            if (string.IsNullOrWhiteSpace(rel))
                continue;
            map[rel] = new RowSampleSignature(sigN, sigE, sigU);
        }
        return map;
    }
}


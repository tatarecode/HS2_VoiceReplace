namespace HS2VoiceReplace;

internal static partial class VoiceReplacePipeline
{
    // Partial rebuild resolution maps a selected wav back to the canonical manifest row

    // so per-row rebuild uses the same routing semantics as the full pipeline.

private static PartialInputResolved ResolvePartialInput(string manifestCsv, string extractWavRoot, string outWavRoot, string inputWav, string bucketOverride)
    {
        var rows = LoadManifestRows(manifestCsv);
        var inputFull = Path.GetFullPath(inputWav);
        var extractRootFull = Path.GetFullPath(extractWavRoot);
        var outRootFull = Path.GetFullPath(outWavRoot);

        string? rel = null;
        string? source = null;
        string? bucket = null;

        if (IsUnderRoot(inputFull, extractRootFull))
        {
            rel = Path.GetRelativePath(extractRootFull, inputFull).Replace('\\', '/');
            source = inputFull;
            var row = rows.FirstOrDefault(r => string.Equals(r.RelativePath, rel, StringComparison.OrdinalIgnoreCase));
            bucket = row.Bucket;
        }
        else if (IsUnderRoot(inputFull, outRootFull))
        {
            rel = Path.GetRelativePath(outRootFull, inputFull).Replace('\\', '/');
            var row = rows.FirstOrDefault(r => string.Equals(r.RelativePath, rel, StringComparison.OrdinalIgnoreCase));
            var fallback = Path.Combine(extractRootFull, rel.Replace('/', Path.DirectorySeparatorChar));
            if (!string.IsNullOrWhiteSpace(row.SourceFile) && File.Exists(row.SourceFile))
                source = row.SourceFile;
            else if (File.Exists(fallback))
                source = fallback;
            else
                source = !string.IsNullOrWhiteSpace(row.SourceFile) ? row.SourceFile : fallback;
            bucket = row.Bucket;
        }
        else
        {
            ManifestRow row = default;
            foreach (var r in rows)
            {
                if (string.IsNullOrWhiteSpace(r.SourceFile))
                    continue;
                try
                {
                    if (string.Equals(Path.GetFullPath(r.SourceFile), inputFull, StringComparison.OrdinalIgnoreCase))
                    {
                        row = r;
                        break;
                    }
                }
                catch
                {
                }
            }
            if (!string.IsNullOrWhiteSpace(row.RelativePath))
            {
                rel = row.RelativePath;
                var fallback = Path.Combine(extractRootFull, rel.Replace('/', Path.DirectorySeparatorChar));
                if (!string.IsNullOrWhiteSpace(row.SourceFile) && File.Exists(row.SourceFile))
                    source = row.SourceFile;
                else if (File.Exists(fallback))
                    source = fallback;
                else
                    source = row.SourceFile;
                bucket = row.Bucket;
            }
        }

        if (string.IsNullOrWhiteSpace(rel) || string.IsNullOrWhiteSpace(source))
            throw new InvalidOperationException(L("error.partialInputNotInRun"));
        if (!File.Exists(source))
        {
            var fallback = Path.Combine(extractRootFull, rel.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(fallback))
                source = fallback;
        }
        if (!File.Exists(source))
            throw new FileNotFoundException(L("error.partialSourceMissing"), source);

        var selectedBucket = string.Equals(bucketOverride, "ero", StringComparison.OrdinalIgnoreCase) ? "ero" : "normal";
        if (string.IsNullOrWhiteSpace(bucketOverride) && !string.IsNullOrWhiteSpace(bucket))
            selectedBucket = bucket;

        return new PartialInputResolved(rel, source, selectedBucket);
    }
}












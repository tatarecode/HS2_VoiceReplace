namespace HS2VoiceReplace;

// Provides pure freshness and pending-row decisions used by resumable conversion flows.
internal static class VoiceReplaceFreshnessUtil
{
    internal readonly record struct SignatureMapRow(
        string RelativePath,
        string Bucket,
        string OutputFile,
        string SigNormal,
        string SigEro,
        string SigUsed,
        string SampleNormalName = "",
        string SampleEroName = "",
        string SampleUsedName = "",
        string SeedVcSummary = "");
    internal readonly record struct PendingReasonSummary(int MissingFileOnly, int SigMismatchOnly, int MissingAndSigMismatch);

    public static (List<(string RelativePath, string Bucket, string SourceFile)> PendingRows, PendingReasonSummary Summary) BuildPendingRows(
        IEnumerable<(string RelativePath, string Bucket, string SourceFile)> rows,
        string outWavRoot,
        IReadOnlyDictionary<string, SignatureMapRow> sigMap,
        string currentNormalSig,
        string currentEroSig,
        bool allowExistingFileFallbackWithoutSignature = false)
    {
        var pendingRows = new List<(string RelativePath, string Bucket, string SourceFile)>();
        var missingFileOnly = 0;
        var sigMismatchOnly = 0;
        var missingAndSigMismatch = 0;

        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.RelativePath))
                continue;

            var rel = row.RelativePath.Replace('\\', '/');
            var bucket = string.Equals(row.Bucket, "ero", StringComparison.OrdinalIgnoreCase) ? "ero" : "normal";
            var expectedSig = bucket == "ero" ? currentEroSig : currentNormalSig;
            var dst = Path.Combine(outWavRoot, rel.Replace('/', Path.DirectorySeparatorChar));
            var fileExists = File.Exists(dst);

            sigMap.TryGetValue(rel, out var mapRow);
            var sigMatched = !string.IsNullOrWhiteSpace(mapRow.SigUsed) &&
                string.Equals(mapRow.SigUsed, expectedSig, StringComparison.OrdinalIgnoreCase);
            var canReuseWithoutSignature =
                allowExistingFileFallbackWithoutSignature &&
                fileExists &&
                string.IsNullOrWhiteSpace(mapRow.SigUsed);

            if (fileExists && (sigMatched || canReuseWithoutSignature))
                continue;

            if (!fileExists && !sigMatched) missingAndSigMismatch++;
            else if (!fileExists) missingFileOnly++;
            else sigMismatchOnly++;

            pendingRows.Add((row.RelativePath, row.Bucket, row.SourceFile));
        }

        return (pendingRows, new PendingReasonSummary(missingFileOnly, sigMismatchOnly, missingAndSigMismatch));
    }
}


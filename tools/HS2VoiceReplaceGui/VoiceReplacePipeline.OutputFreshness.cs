using System.Globalization;

namespace HS2VoiceReplace;

internal static partial class VoiceReplacePipeline
{
    // Freshness checks are kept separate from signature builders because they compare

    // materialized artifacts on disk rather than logical input identity.

private static bool HasExpectedConvertedWavs(string manifestCsv, string outWavRoot)
    {
        foreach (var row in LoadManifestRows(manifestCsv))
        {
            if (string.IsNullOrWhiteSpace(row.RelativePath))
                continue;
            var dst = Path.Combine(outWavRoot, row.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(dst))
                return false;
        }
        return true;
    }

    private static bool IsManifestReadyForPersonality(string manifestCsv, int personalityId, string expectedExtractWavRoot)
    {
        if (!File.Exists(manifestCsv))
            return false;
        var fi = new FileInfo(manifestCsv);
        if (fi.Length <= 0)
            return false;

        List<ManifestRow> rows;
        try
        {
            rows = LoadManifestRows(manifestCsv);
        }
        catch
        {
            return false;
        }
        if (rows.Count == 0)
            return false;

        var expectedRoot = Path.GetFullPath(expectedExtractWavRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        var hasToken = false;
        foreach (var row in rows)
        {
            var rel = (row.RelativePath ?? "").Replace('\\', '/');
            var file = Path.GetFileName(rel);
            var m = PersonalityClipRegex.Match(file);
            if (m.Success && int.TryParse(m.Groups["id"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            {
                hasToken = true;
                if (id != personalityId)
                    return false;
            }

            if (!string.IsNullOrWhiteSpace(row.SourceFile))
            {
                try
                {
                    var src = Path.GetFullPath(row.SourceFile);
                    if (!src.StartsWith(expectedRoot, StringComparison.OrdinalIgnoreCase))
                        return false;
                }
                catch
                {
                    return false;
                }
            }
        }

        return hasToken;
    }

    private static bool HasMatchingPersonalityWavs(string wavDir, int personalityId, out int total, out int matched)
    {
        total = 0;
        matched = 0;
        if (!Directory.Exists(wavDir))
            return false;

        foreach (var wav in Directory.GetFiles(wavDir, "*.wav", SearchOption.AllDirectories))
        {
            total++;
            var file = Path.GetFileName(wav);
            var m = PersonalityClipRegex.Match(file);
            if (!m.Success)
                continue;
            if (int.TryParse(m.Groups["id"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) && id == personalityId)
                matched++;
        }

        return total > 0 && matched > 0;
    }

    private static bool HasExpectedRebuiltBundles(IReadOnlyList<BundleTarget> targets, string replaceInputRoot)
    {
        foreach (var t in targets)
        {
            var dstBundle = Path.Combine(replaceInputRoot, t.DstRel.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(dstBundle))
                return false;
        }
        return true;
    }

    private static bool HasExpectedRebuiltBundlesFresh(IReadOnlyList<BundleTarget> targets, string replaceInputRoot, string outWavRoot)
        => VoiceReplaceOutputFreshnessUtil.HasExpectedRebuiltBundlesFresh(
            targets.Select(t => (t.DstRel, t.WavRel)),
            replaceInputRoot,
            outWavRoot);

    private static bool HasExpectedSplitZipmods(IReadOnlyList<BundleTarget> targets, string splitOutRoot, string pid)
    {
        foreach (var t in targets)
        {
            var zip = Path.Combine(splitOutRoot, $"HS2VoiceReplace_{pid}_{t.Key}.zipmod");
            if (!File.Exists(zip))
                return false;
        }
        return true;
    }

    private static bool HasExpectedSplitZipmodsFresh(IReadOnlyList<BundleTarget> targets, string splitOutRoot, string pid, string replaceInputRoot)
        => VoiceReplaceOutputFreshnessUtil.HasExpectedSplitZipmodsFresh(
            targets.Select(t => (t.Key, t.DstRel)),
            splitOutRoot,
            pid,
            replaceInputRoot);
}












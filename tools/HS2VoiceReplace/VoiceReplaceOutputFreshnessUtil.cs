namespace HS2VoiceReplace;

// Provides disk-freshness checks for rebuilt bundles and split zipmods without UI or process dependencies.
internal static class VoiceReplaceOutputFreshnessUtil
{
    public static bool HasExpectedRebuiltBundlesFresh(IEnumerable<(string DstRel, string WavRel)> targets, string replaceInputRoot, string outWavRoot)
    {
        foreach (var t in targets)
        {
            var dstBundle = Path.Combine(replaceInputRoot, t.DstRel.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(dstBundle))
                return false;

            var wavDir = Path.Combine(outWavRoot, t.WavRel.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(wavDir))
                return false;

            var latestWavUtc = Directory.GetFiles(wavDir, "*.wav", SearchOption.AllDirectories)
                .Select(p => File.GetLastWriteTimeUtc(p))
                .DefaultIfEmpty(DateTime.MinValue)
                .Max();
            if (latestWavUtc == DateTime.MinValue)
                return false;

            if (File.GetLastWriteTimeUtc(dstBundle) < latestWavUtc)
                return false;
        }

        return true;
    }

    public static bool HasExpectedSplitZipmodsFresh(IEnumerable<(string Key, string DstRel)> targets, string splitOutRoot, string pid, string replaceInputRoot)
    {
        foreach (var t in targets)
        {
            var zip = Path.Combine(splitOutRoot, $"HS2VoiceReplace_{pid}_{t.Key}.zipmod");
            var srcBundle = Path.Combine(replaceInputRoot, t.DstRel.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(zip) || !File.Exists(srcBundle))
                return false;
            if (File.GetLastWriteTimeUtc(zip) < File.GetLastWriteTimeUtc(srcBundle))
                return false;
        }

        return true;
    }
}


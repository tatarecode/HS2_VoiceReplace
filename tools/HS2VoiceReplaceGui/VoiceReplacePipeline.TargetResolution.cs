using System.Globalization;
using System.Text.RegularExpressions;

namespace HS2VoiceReplace;

internal static partial class VoiceReplacePipeline
{
    // Target resolution converts abstract content buckets into concrete source bundles for a given personality,

    // including the runtime-specific file-name mapping differences between personalities.

private static string BuildSourceBundlePath(string sourceHs2Root, string pid, BundleTarget t)
    {
        if (t.IsCustomSource)
        {
            var rel = t.SrcRel.Replace('\\', '/');
            if (rel.StartsWith("custom/", StringComparison.OrdinalIgnoreCase))
                rel = rel["custom/".Length..];
            return Path.Combine(sourceHs2Root, "abdata", "sound", "data", "custom", rel.Replace('/', Path.DirectorySeparatorChar));
        }
        return Path.Combine(sourceHs2Root, "abdata", "sound", "data", "pcm", pid, t.SrcRel.Replace('/', Path.DirectorySeparatorChar));
    }

    private static BundleTarget ResolveTargetFromRelativePath(string relativePath)
    {
        var rel = relativePath.Replace('\\', '/');
        foreach (var t in BuildTargets())
        {
            if (string.Equals(rel, t.WavRel, StringComparison.OrdinalIgnoreCase) ||
                rel.StartsWith(t.WavRel + "/", StringComparison.OrdinalIgnoreCase))
                return t;
        }
        throw new InvalidOperationException(L("error.targetBundleResolveFailed", relativePath));
    }

    private static int ResolvePersonalityIdFromRunRoot(string runRoot, int fallback)
        => VoiceReplaceTargetResolutionUtil.ResolvePersonalityIdFromRunRoot(runRoot, fallback);

    private static List<BundleTarget> BuildTargets() => new()
    {
        new("adv", "adv", "adv/30.unity3d", "adv/30.unity3d"),
        new("custom", "etc", "custom/30.unity3d", "custom/30.unity3d", true),
        new("etc", "etc", "etc/30.unity3d", "etc/30.unity3d"),
        new("h_bre", "h/bre", "h/bre/30.unity3d", "h/bre/30.unity3d"),
        new("h_car", "h/car", "h/car/30.unity3d", "h/car/30.unity3d"),
        new("h_eve", "h/eve", "h/eve/30.unity3d", "h/eve/30.unity3d"),
        new("h_f3p", "h/f3p", "h/f3p/30.unity3d", "h/f3p/30.unity3d"),
        new("h_int", "h/int", "h/int/30.unity3d", "h/int/30.unity3d"),
        new("h_m3p", "h/m3p", "h/m3p/30.unity3d", "h/m3p/30.unity3d"),
        new("h_rez", "h/rez", "h/rez/30.unity3d", "h/rez/30.unity3d"),
        new("h_ser", "h/ser", "h/ser/30.unity3d", "h/ser/30.unity3d"),
        new("h_sp", "h/sp", "h/sp/30.unity3d", "h/sp/30.unity3d"),
        new("h_wait", "h/wait", "h/wait/30.unity3d", "h/wait/30.unity3d"),
    };

    private static List<BundleTarget> ResolveTargetsForPersonality(PipelineOptions o, string pid, Action<string> log)
    {
        var resolved = new List<BundleTarget>();
        foreach (var t in BuildTargets())
            resolved.Add(ResolveSingleTarget(o.Hs2Root, pid, t, log));
        return resolved;
    }

    private static BundleTarget ResolveSingleTarget(string sourceHs2Root, string pid, BundleTarget baseTarget, Action<string> log)
    {
        var srcBase = baseTarget.IsCustomSource
            ? Path.Combine(sourceHs2Root, "abdata", "sound", "data", "custom")
            : Path.Combine(sourceHs2Root, "abdata", "sound", "data", "pcm", pid);

        var srcRelForLookup = baseTarget.SrcRel.Replace('\\', '/');
        if (baseTarget.IsCustomSource && srcRelForLookup.StartsWith("custom/", StringComparison.OrdinalIgnoreCase))
            srcRelForLookup = srcRelForLookup["custom/".Length..];

        var srcRelNorm = srcRelForLookup.Replace('/', Path.DirectorySeparatorChar);
        var srcFull = Path.Combine(srcBase, srcRelNorm);

        var relDir = Path.GetDirectoryName(srcRelForLookup)?.Replace('\\', '/') ?? "";
        var dirFull = string.IsNullOrWhiteSpace(relDir) ? srcBase : Path.Combine(srcBase, relDir.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(dirFull))
            throw new FileNotFoundException(L("error.bundleDirectoryNotFound", dirFull));

        var cands = Directory.GetFiles(dirFull, "*.unity3d", SearchOption.TopDirectoryOnly)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (cands.Length == 0)
            throw new FileNotFoundException(L("error.bundleNotFound", dirFull));

        var chosenFileName = VoiceReplaceTargetResolutionUtil.ChooseBundleFileName(cands, srcRelForLookup, pid);
        var chosen = cands.First(x => string.Equals(Path.GetFileName(x), chosenFileName, StringComparison.OrdinalIgnoreCase));
        var mappedInnerRel = string.IsNullOrWhiteSpace(relDir) ? chosenFileName : (relDir + "/" + chosenFileName).Replace('\\', '/');
        var mappedSrcRel = baseTarget.IsCustomSource ? ("custom/" + mappedInnerRel).Replace('\\', '/') : mappedInnerRel;
        var mappedDstRel = mappedSrcRel;
        log($"  [bundle-map] {baseTarget.Key}: {baseTarget.SrcRel} -> {mappedSrcRel}");
        return new BundleTarget(baseTarget.Key, baseTarget.WavRel, mappedSrcRel, mappedDstRel, baseTarget.IsCustomSource);
    }

    private static bool TryParsePersonalityId(string pid, out int value)
        => VoiceReplaceTargetResolutionUtil.TryParsePersonalityId(pid, out value);
}












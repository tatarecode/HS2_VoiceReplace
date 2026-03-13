using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace HS2VoiceReplace;

internal static partial class VoiceReplacePipeline
{
    // Zipmod packaging is treated as a pure staging concern: it copies a prepared template,

    // injects rebuilt bundles, rewrites metadata, and emits deployable archives.

private static List<string> BuildSplitZipmods(
        string templateRoot,
        string voiceBundleRoot,
        string outDir,
        string pid,
        IEnumerable<string>? packFilter = null,
        IReadOnlyList<BundleTarget>? resolvedTargets = null)
    {
        if (!Directory.Exists(templateRoot)) throw new DirectoryNotFoundException(templateRoot);
        Directory.CreateDirectory(outDir);

        List<(string Name, string Src, string Dst)> packs;
        if (resolvedTargets != null && resolvedTargets.Count > 0)
        {
            packs = resolvedTargets
                .Select(t =>
                {
                    var src = t.DstRel.Replace('\\', '/');
                    string dst;
                    if (t.IsCustomSource)
                    {
                        var file = Path.GetFileName(src);
                        dst = $"abdata/sound/data/custom/{file}";
                    }
                    else
                    {
                        dst = $"abdata/sound/data/pcm/{pid}/{src}";
                    }
                    return (t.Key, src, dst);
                })
                .ToList();
        }
        else
        {
            throw new InvalidOperationException("Resolved bundle targets are required for zipmod packaging.");
        }

        var stageBase = Path.Combine(outDir, "stage");
        if (Directory.Exists(stageBase)) Directory.Delete(stageBase, true);
        Directory.CreateDirectory(stageBase);

        var filters = packFilter == null
            ? null
            : new HashSet<string>(packFilter, StringComparer.OrdinalIgnoreCase);

        var built = new List<string>();
        foreach (var p in packs)
        {
            if (filters != null && !filters.Contains(p.Name))
                continue;

            var stage = Path.Combine(stageBase, p.Name);
            CopyDirectory(templateRoot, stage);

            var src = Path.Combine(voiceBundleRoot, p.Src.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(src)) throw new FileNotFoundException(src);

            var dst = Path.Combine(stage, p.Dst.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
            File.Copy(src, dst, true);
            SetUnityBundleCabUnique(dst);

            var manifest = Path.Combine(stage, "manifest.xml");
            if (File.Exists(manifest))
            {
                var x = XDocument.Load(manifest);
                var m = x.Root;
                if (m != null)
                {
                    var g = m.Element("guid");
                    if (g != null) g.Value = $"com.hs2voicereplace.{pid}.{p.Name}";
                    var n = m.Element("name");
                    if (n != null) n.Value = $"HS2 Voice Replace {pid.ToUpperInvariant()} ({p.Name})";
                    var v = m.Element("version");
                    if (v != null) v.Value = "1.0.0";
                    x.Save(manifest);
                }
            }

            var zip = Path.Combine(outDir, $"HS2VoiceReplace_{pid}_{p.Name}.zipmod");
            if (File.Exists(zip)) File.Delete(zip);
            ZipFile.CreateFromDirectory(stage, zip, CompressionLevel.Optimal, false);
            built.Add(zip);
        }

        return built;
    }

    private static void CopyDirectory(string src, string dst)
    {
        Directory.CreateDirectory(dst);
        foreach (var d in Directory.GetDirectories(src, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(dst, Path.GetRelativePath(src, d)));

        foreach (var f in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
        {
            var outFile = Path.Combine(dst, Path.GetRelativePath(src, f));
            Directory.CreateDirectory(Path.GetDirectoryName(outFile)!);
            File.Copy(f, outFile, true);
        }
    }

    private static readonly Regex CabRegex = new("CAB-[0-9a-f]{32}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static void SetUnityBundleCabUnique(string bundlePath)
    {
        var bytes = File.ReadAllBytes(bundlePath);
        var text = Encoding.ASCII.GetString(bytes);
        var tokens = CabRegex.Matches(text).Select(m => m.Value).Distinct(StringComparer.Ordinal).ToArray();
        if (tokens.Length == 0) return;

        foreach (var oldToken in tokens)
        {
            var newToken = "CAB-" + Guid.NewGuid().ToString("N");
            var oldBytes = Encoding.ASCII.GetBytes(oldToken);
            var newBytes = Encoding.ASCII.GetBytes(newToken);
            for (int i = 0; i <= bytes.Length - oldBytes.Length; i++)
            {
                var matched = true;
                for (int j = 0; j < oldBytes.Length; j++)
                {
                    if (bytes[i + j] != oldBytes[j]) { matched = false; break; }
                }
                if (!matched) continue;
                for (int j = 0; j < newBytes.Length; j++) bytes[i + j] = newBytes[j];
                i += oldBytes.Length - 1;
            }
        }

        File.WriteAllBytes(bundlePath, bytes);
    }
}













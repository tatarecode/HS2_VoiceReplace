using System.Globalization;
using System.Text.RegularExpressions;

namespace HS2VoiceReplace;

internal static partial class VoiceReplacePipeline
{
    // Dependency resolution stays separate from execution so tool lookup logic can evolve

    // without increasing the complexity of the pipeline modes themselves.

private static string GetSeedVcInferScriptName(SeedVcUiSettings seed)
        => seed.Engine == SeedVcEngine.V1 ? "seed_vc_v1_inprocess_batch.py" : "seed_vc_v2_inprocess_batch.py";

    private static string ResolveSeedVcRoot(PipelineOptions o, SeedVcUiSettings seed)
    {
        var preferred = seed.Engine == SeedVcEngine.V1
            ? new[] { "seed_vc_v2", "seed_vc_v1" }
            : new[] { "seed_vc_v2", "seed_vc_v1" };
        foreach (var rel in preferred)
        {
            if (DependencyExists(o, rel))
                return ResolveDependencyPath(o, rel);
        }
        return ResolveDependencyPath(o, "seed_vc_v2");
    }

    private static string ResolveRuntimePluginPath(PipelineOptions o)
    {
        foreach (var fileName in EnumerateRuntimePluginFileNames())
        {
            foreach (var root in EnumerateDependencyRoots(o))
            {
                var p = Path.Combine(root, "plugins", fileName);
                if (File.Exists(p))
                    return p;
            }
        }

        return Path.Combine(o.BundleRoot, "plugins", RuntimePluginFileName);
    }

    private static bool RuntimePluginExists(PipelineOptions o)
    {
        foreach (var fileName in EnumerateRuntimePluginFileNames())
        {
            foreach (var root in EnumerateDependencyRoots(o))
            {
                var p = Path.Combine(root, "plugins", fileName);
                if (File.Exists(p))
                    return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> EnumerateRuntimePluginFileNames()
    {
        yield return RuntimePluginFileName;
        yield return LegacyRuntimePluginFileName;
    }

    private static string ResolveDependencyPath(PipelineOptions o, params string[] relParts)
    {
        var relCandidates = BuildDependencyRelativeCandidates(relParts);
        foreach (var rel in relCandidates)
        {
            foreach (var root in EnumerateDependencyRoots(o))
            {
                var p = Path.Combine(root, rel);
                if (File.Exists(p) || Directory.Exists(p))
                    return p;
            }
        }
        return Path.Combine(o.BundleRoot, relCandidates[0]);
    }

    private static bool DependencyExists(PipelineOptions o, string rel)
    {
        var relCandidates = BuildDependencyRelativeCandidates(rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        foreach (var root in EnumerateDependencyRoots(o))
        {
            foreach (var candidate in relCandidates)
            {
                var p = Path.Combine(root, candidate);
                if (File.Exists(p) || Directory.Exists(p)) return true;
            }
        }
        return false;
    }

    private static List<string> BuildDependencyRelativeCandidates(params string[] relParts)
    {
        var parts = relParts?.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray() ?? Array.Empty<string>();
        var rel = Path.Combine(parts);
        var list = new List<string> { rel };
        if (parts.Length == 2 &&
            string.Equals(parts[0], "scripts", StringComparison.OrdinalIgnoreCase))
        {
            list.Add(Path.Combine("tools", parts[1]));
        }
        return list;
    }

    private static string ResolveWorkingDirectory(PipelineOptions o)
    {
        foreach (var root in EnumerateDependencyRoots(o))
            if (Directory.Exists(root))
                return root;
        return AppContext.BaseDirectory;
    }

    private static string ResolvePythonExe(PipelineOptions o)
    {
        var roots = EnumerateDependencyRoots(o).ToList();

        var relCandidates = new[]
        {
            Path.Combine("rvc_venv", "Scripts", "python.exe"),
            Path.Combine("rvc_venv", ".venv", "Scripts", "python.exe"),
            Path.Combine("python", "python.exe"),
            Path.Combine("seed_vc_v2", ".venv", "Scripts", "python.exe"),
        };

        foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var rel in relCandidates)
            {
                var c = Path.Combine(root, rel);
                if (File.Exists(c)) return c;
            }
        }

        throw new InvalidOperationException(L("error.pipelinePythonMissing"));
    }

    private static IEnumerable<string> EnumerateDependencyRoots(PipelineOptions o)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var yieldRoots = new List<string>();

        void Add(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            var full = Path.GetFullPath(path);
            if (seen.Add(full)) yieldRoots.Add(full);
        }
        Add(o.ExternalToolsRoot);
        Add(o.BundleRoot);

        // Dev fallback roots help local builds find checked-in dependencies when running from the repo.
        Add(Directory.GetCurrentDirectory());
        Add(AppContext.BaseDirectory);

        foreach (var basePath in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            try
            {
                var d = new DirectoryInfo(basePath);
                for (int i = 0; i < 8 && d != null; i++, d = d.Parent)
                {
                    if (d == null) break;
                    Add(d.FullName);
                }
            }
            catch
            {
            }
        }

        return yieldRoots;
    }
}












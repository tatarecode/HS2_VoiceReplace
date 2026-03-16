using System.Text;

namespace HS2VoiceReplace;

// Handles copying or mirroring repository-bundled assets into the external tools area used by the GUI runtime.

internal static partial class DependencyBootstrapper
{
    private static async Task EnsureAuxiliaryFilesAsync(string externalRoot, string bundledRoot, Action<string> log, CancellationToken ct)
    {
        var roots = EnumerateSourceRoots(bundledRoot).ToArray();

        await EnsureScriptAsync(externalRoot, roots, "python_cli_common.py");
        await EnsureScriptAsync(externalRoot, roots, "seed_vc_batch_common.py");
        await EnsureScriptAsync(externalRoot, roots, "seed_vc_v1_inprocess_batch.py");
        await EnsureScriptAsync(externalRoot, roots, "seed_vc_v2_inprocess_batch.py");
        await EnsureScriptAsync(externalRoot, roots, "select_voice_style_segment.py");
        EnsureTemplateAsync(externalRoot, roots, log);
        await EnsurePatcherAsync(externalRoot, roots, log, ct);
        EnsureRuntimePluginAsync(externalRoot, roots, log);
    }

    private static Task EnsureScriptAsync(string externalRoot, string[] roots, string scriptName)
    {
        var dst = Path.Combine(externalRoot, "scripts", scriptName);
        foreach (var root in roots)
        {
            var candidates = new[]
            {
                Path.Combine(root, "scripts", scriptName),
                Path.Combine(root, "tools", scriptName),
            };
            foreach (var c in candidates)
            {
                if (!File.Exists(c)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                File.Copy(c, dst, true);
                return Task.CompletedTask;
            }
        }

        throw new InvalidOperationException(L("error.requiredScriptMissing", scriptName));
    }

    private static async Task EnsurePatcherAsync(string externalRoot, string[] roots, Action<string> log, CancellationToken ct)
    {
        var dstExe = Path.Combine(externalRoot, "tools", "UabAudioClipPatcher", "UabAudioClipPatcher.exe");
        if (File.Exists(dstExe)) return;

        foreach (var root in roots)
        {
            var prebuilt = Path.Combine(root, "tools", "UabAudioClipPatcher", "bin", "Release", "net8.0");
            if (Directory.Exists(prebuilt) && File.Exists(Path.Combine(prebuilt, "UabAudioClipPatcher.exe")))
            {
                CopyDirectory(prebuilt, Path.GetDirectoryName(dstExe)!);
                return;
            }

            var csproj = Path.Combine(root, "tools", "UabAudioClipPatcher", "UabAudioClipPatcher.csproj");
            if (File.Exists(csproj))
            {
                var projectDir = Path.GetDirectoryName(csproj)!;
                var assetsToolsPath = AssetsToolsReferenceUtil.ResolveBuildReferencePath(projectDir, out var fromEnvironment);
                if (assetsToolsPath == null)
                    throw new InvalidOperationException(L(
                        "error.assetsToolsNetMissingForPatcherBuild",
                        AssetsToolsReferenceUtil.DefaultRelativePath,
                        AssetsToolsReferenceUtil.EnvVarName));

                if (fromEnvironment)
                    log(L("log.assetsToolsNetOverrideUsed", assetsToolsPath));

                await ProcessUtil.RunAsync(
                    "dotnet",
                    $"build \"{csproj}\" -c Release -p:AssetsToolsNetPath=\"{assetsToolsPath}\"",
                    projectDir,
                    log,
                    ct);
                var built = Path.Combine(projectDir, "bin", "Release", "net8.0");
                if (Directory.Exists(built) && File.Exists(Path.Combine(built, "UabAudioClipPatcher.exe")))
                {
                    CopyDirectory(built, Path.GetDirectoryName(dstExe)!);
                    return;
                }
            }
        }

        throw new InvalidOperationException(L("error.uabAudioClipPatcherMissing"));
    }

    private static void EnsureTemplateAsync(string externalRoot, string[] roots, Action<string> log)
    {
        var dst = Path.Combine(externalRoot, "mods_template", VoiceReplaceNames.RuntimeTemplateDirName);
        var manifest = Path.Combine(dst, "manifest.xml");
        if (File.Exists(manifest)) return;

        foreach (var root in roots)
        {
            var candidates = new[]
            {
                Path.Combine(root, "mods_template", VoiceReplaceNames.RuntimeTemplateDirName),
                Path.Combine(root, "mods_src", VoiceReplaceNames.RuntimeTemplateSourceDirName),
            };
            foreach (var c in candidates)
            {
                if (!Directory.Exists(c)) continue;
                CopyDirectory(c, dst);
                return;
            }
        }

        Directory.CreateDirectory(dst);
        File.WriteAllText(manifest,
            "<manifest><guid>com.hs2voicereplace.template</guid><name>HS2 Voice Replace Template</name><version>1.0.0</version></manifest>",
            new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(dst, "README.txt"), "Template generated by setup.", new UTF8Encoding(false));
        log(L("log.modsTemplateGenerated"));
    }

    private static void EnsureRuntimePluginAsync(string externalRoot, string[] roots, Action<string> log)
    {
        var dst = Path.Combine(externalRoot, "plugins", VoiceReplaceNames.RuntimePluginFileName);
        if (File.Exists(dst)) return;

        foreach (var root in roots)
        {
            var candidates = new[]
            {
                Path.Combine(root, "plugins", VoiceReplaceNames.RuntimePluginFileName),
                Path.Combine(root, "plugins", VoiceReplaceNames.LegacyRuntimePluginFileName),
                Path.Combine(root, "runtime", "HS2VoiceReplace.Runtime", "bin", "Release", "net472", VoiceReplaceNames.RuntimePluginFileName),
                Path.Combine(root, "runtime", "HS2VoiceReplace.Runtime", "bin", "Release", "net472", VoiceReplaceNames.LegacyRuntimePluginFileName),
            };
            foreach (var c in candidates)
            {
                if (!File.Exists(c)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                File.Copy(c, dst, true);
                return;
            }
        }

        log(L("log.runtimePluginSkipped", VoiceReplaceNames.RuntimePluginFileName));
    }

    private static IEnumerable<string> EnumerateSourceRoots(string bundledRoot)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var roots = new List<string>();
        void Add(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            var full = Path.GetFullPath(path);
            if (seen.Add(full))
                roots.Add(full);
        }

        Add(bundledRoot);
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
            catch { }
        }

        return roots;
    }

    private static async Task MirrorBundledToolsAsync(string externalRoot, string bundledRoot, Action<string> log)
    {
        var rels = new[]
        {
            Path.Combine("scripts", "python_cli_common.py"),
            Path.Combine("scripts", "seed_vc_batch_common.py"),
            Path.Combine("scripts", "seed_vc_v1_inprocess_batch.py"),
            Path.Combine("scripts", "seed_vc_v2_inprocess_batch.py"),
            Path.Combine("scripts", "select_voice_style_segment.py"),
            Path.Combine("tools", "UabAudioClipPatcher"),
            Path.Combine("mods_template", VoiceReplaceNames.RuntimeTemplateDirName),
            Path.Combine("plugins", VoiceReplaceNames.RuntimePluginFileName),
        };

        foreach (var rel in rels)
        {
            var dst = Path.Combine(externalRoot, rel);
            if (File.Exists(dst) || Directory.Exists(dst))
                continue;

            var src = Path.Combine(bundledRoot, rel);
            if (!File.Exists(src) && !Directory.Exists(src))
                continue;

            if (Directory.Exists(src))
                CopyDirectory(src, dst);
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                File.Copy(src, dst, true);
            }

            log($"bundled mirror: {rel}");
        }

        await Task.CompletedTask;
    }
}




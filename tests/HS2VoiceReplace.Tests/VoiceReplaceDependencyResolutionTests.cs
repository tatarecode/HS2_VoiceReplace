using System.Reflection;
using System.Text;
using Xunit;

namespace HS2VoiceReplace.Tests;

public sealed class VoiceReplaceDependencyResolutionTests
{
    [Fact]
    public void BuildDependencyRelativeCandidates_IncludesModsSrcFallback_ForRuntimeTemplate()
    {
        var method = typeof(HS2VoiceReplace.MainForm)
            .Assembly
            .GetType("HS2VoiceReplace.VoiceReplacePipeline", throwOnError: true)!
            .GetMethod("BuildDependencyRelativeCandidates", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var actual = (List<string>)method!.Invoke(null, new object[] { new[] { "mods_template", "HS2VoiceReplaceRuntime" } })!;

        Assert.Equal(
            new[]
            {
                Path.Combine("mods_template", "HS2VoiceReplaceRuntime"),
                Path.Combine("mods_src", "HS2VoiceReplaceRuntimeTemplate"),
            },
            actual);
    }

    [Fact]
    public async Task EnsurePatcherAsync_AcceptsBundledReleaseLayout()
    {
        var bootstrapperType = typeof(HS2VoiceReplace.MainForm)
            .Assembly
            .GetType("HS2VoiceReplace.DependencyBootstrapper", throwOnError: true)!;
        var method = bootstrapperType.GetMethod("EnsurePatcherAsync", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var tempRoot = Path.Combine(Path.GetTempPath(), "hs2vr_patcher_test_" + Guid.NewGuid().ToString("N"));
        var externalRoot = Path.Combine(tempRoot, "external");
        var bundledRoot = Path.Combine(tempRoot, "bundle");
        var bundledToolDir = Path.Combine(bundledRoot, "tools", "UabAudioClipPatcher");
        var bundledExe = Path.Combine(bundledToolDir, "UabAudioClipPatcher.exe");
        var bundledDeps = Path.Combine(bundledToolDir, "UabAudioClipPatcher.deps.json");

        Directory.CreateDirectory(bundledToolDir);
        File.WriteAllText(bundledExe, "exe", new UTF8Encoding(false));
        File.WriteAllText(bundledDeps, "deps", new UTF8Encoding(false));

        try
        {
            var task = (Task)method!.Invoke(null, new object[]
            {
                externalRoot,
                new[] { bundledRoot },
                new Action<string>(_ => { }),
                CancellationToken.None,
            })!;
            await task;

            Assert.True(File.Exists(Path.Combine(externalRoot, "tools", "UabAudioClipPatcher", "UabAudioClipPatcher.exe")));
            Assert.True(File.Exists(Path.Combine(externalRoot, "tools", "UabAudioClipPatcher", "UabAudioClipPatcher.deps.json")));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }
}

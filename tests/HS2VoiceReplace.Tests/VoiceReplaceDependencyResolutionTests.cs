using System.Reflection;
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
}

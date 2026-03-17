using Xunit;

namespace HS2VoiceReplace.Tests;

public sealed class RunRootSelectionUtilTests
{
    [Fact]
    public void ResolveSuggestedRunRoot_UsesDefaultResumeRoot_WhenCurrentIsEmpty()
    {
        var actual = RunRootSelectionUtil.ResolveSuggestedRunRoot(
            currentRunRoot: "",
            activeOutputRoot: @"C:\work\.hs2voicereplace",
            targetPersonalityId: 1);

        Assert.Equal(
            Path.GetFullPath(@"C:\work\.hs2voicereplace\gui_runs\resume_c01"),
            actual);
    }

    [Fact]
    public void ResolveSuggestedRunRoot_ReplacesAutoResumeRoot_WhenPersonalityChanges()
    {
        var actual = RunRootSelectionUtil.ResolveSuggestedRunRoot(
            currentRunRoot: @"C:\work\.hs2voicereplace\gui_runs\resume_c13",
            activeOutputRoot: @"C:\work\.hs2voicereplace",
            targetPersonalityId: 0);

        Assert.Equal(
            Path.GetFullPath(@"C:\work\.hs2voicereplace\gui_runs\resume_c00"),
            actual);
    }

    [Fact]
    public void ResolveSuggestedRunRoot_PreservesExplicitRunRoot()
    {
        var actual = RunRootSelectionUtil.ResolveSuggestedRunRoot(
            currentRunRoot: @"C:\work\custom_runs\session_a",
            activeOutputRoot: @"C:\work\.hs2voicereplace",
            targetPersonalityId: 0);

        Assert.Equal(
            Path.GetFullPath(@"C:\work\custom_runs\session_a"),
            actual);
    }
}

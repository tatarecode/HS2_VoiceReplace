using HS2VoiceReplace;
using Xunit;

namespace HS2VoiceReplace.Tests;

public sealed class VoiceReplaceTargetResolutionUtilTests
{
    [Theory]
    [InlineData(@"C:\work\resume_c02", 13, 2)]
    [InlineData(@"C:\work\foo-c12", 13, 12)]
    [InlineData(@"C:\work\run", 13, 13)]
    public void ResolvePersonalityIdFromRunRoot_UsesRunRootNameWhenAvailable(string runRoot, int fallback, int expected)
    {
        var actual = VoiceReplaceTargetResolutionUtil.ResolvePersonalityIdFromRunRoot(runRoot, fallback);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("02", true, 2)]
    [InlineData("c12", true, 12)]
    [InlineData("C13", true, 13)]
    [InlineData("x", false, -1)]
    public void TryParsePersonalityId_ParsesOptionalPrefix(string input, bool expectedOk, int expectedValue)
    {
        var ok = VoiceReplaceTargetResolutionUtil.TryParsePersonalityId(input, out var value);

        Assert.Equal(expectedOk, ok);
        Assert.Equal(expectedValue, value);
    }

    [Fact]
    public void ChooseBundleFileName_Prefers30ForLowerPersonalityIds()
    {
        var candidates = new[]
        {
            @"C:\tmp\50.unity3d",
            @"C:\tmp\30.unity3d",
        };

        var chosen = VoiceReplaceTargetResolutionUtil.ChooseBundleFileName(candidates, "adv/50.unity3d", "c02");

        Assert.Equal("30.unity3d", chosen);
    }

    [Fact]
    public void ChooseBundleFileName_Prefers50ForHigherPersonalityIds()
    {
        var candidates = new[]
        {
            @"C:\tmp\30.unity3d",
            @"C:\tmp\50.unity3d",
        };

        var chosen = VoiceReplaceTargetResolutionUtil.ChooseBundleFileName(candidates, "adv/30.unity3d", "c12");

        Assert.Equal("50.unity3d", chosen);
    }

    [Fact]
    public void ChooseBundleFileName_FallsBackToHighestNumericStem()
    {
        var candidates = new[]
        {
            @"C:\tmp\10.unity3d",
            @"C:\tmp\70.unity3d",
            @"C:\tmp\20.unity3d",
        };

        var chosen = VoiceReplaceTargetResolutionUtil.ChooseBundleFileName(candidates, "adv/50.unity3d", "c12");

        Assert.Equal("70.unity3d", chosen);
    }

    [Fact]
    public void ChooseBundleFileName_FallsBackTo30_When50DoesNotExistForHigherPersonality()
    {
        var candidates = new[]
        {
            @"C:\tmp\30.unity3d",
        };

        var chosen = VoiceReplaceTargetResolutionUtil.ChooseBundleFileName(candidates, "adv/30.unity3d", "c12");

        Assert.Equal("30.unity3d", chosen);
    }
}


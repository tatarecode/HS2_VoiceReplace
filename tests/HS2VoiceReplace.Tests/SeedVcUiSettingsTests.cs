using HS2VoiceReplace;
using Xunit;

namespace HS2VoiceReplace.Tests;

public sealed class SeedVcUiSettingsTests
{
    [Fact]
    public void Clone_CreatesIndependentCopy()
    {
        var original = new SeedVcUiSettings
        {
            Engine = SeedVcEngine.V2,
            DiffusionSteps = 42,
            GlobalHfBlend = false,
            AudioSrPost = true,
        };

        var clone = original.Clone();
        clone.DiffusionSteps = 7;
        clone.GlobalHfBlend = true;

        Assert.Equal(42, original.DiffusionSteps);
        Assert.False(original.GlobalHfBlend);
        Assert.NotSame(original, clone);
    }

    [Fact]
    public void ToSummaryString_ContainsCriticalUserFacingSettings()
    {
        var settings = new SeedVcUiSettings
        {
            Engine = SeedVcEngine.V1,
            DiffusionSteps = 25,
            IntelligibilityCfgRate = 0.7,
            SimilarityCfgRate = 0.7,
            AudioSrPost = false,
        };

        var summary = settings.ToSummaryString();

        Assert.Contains("engine=V1", summary);
        Assert.Contains("steps=25", summary);
        Assert.Contains("int=0.70", summary);
        Assert.Contains("sim=0.70", summary);
        Assert.Contains("audiosr=False", summary);
    }
}


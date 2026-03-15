using HS2VoiceReplace;
using Xunit;

namespace HS2VoiceReplace.Tests;

public sealed class AdvScenarioVoiceLineExtractorTests
{
    [Fact]
    public void BuildVoiceLineMapFromScenarioCommands_Pairs_Voice_With_Nearby_Dialogue()
    {
        var commands = new[]
        {
            new AdvScenarioVoiceLineExtractor.ScenarioCommand(104, new[] { "0", "標準" }),
            new AdvScenarioVoiceLineExtractor.ScenarioCommand(17, new[] { "0", "sound/data/pcm/c00/adv/30.unity3d", "hsa_00_022_04_00_00_00" }),
            new AdvScenarioVoiceLineExtractor.ScenarioCommand(16, new[] { "[H]", "私とお話を？" }),
        };

        var map = AdvScenarioVoiceLineExtractor.BuildVoiceLineMapFromScenarioCommands(commands, "c00");

        Assert.Equal("私とお話を？", map["adv/hsa_00_022_04_00_00_00.wav"]);
    }

    [Fact]
    public void BuildVoiceLineMapFromScenarioCommands_Stops_At_Strong_Boundaries()
    {
        var commands = new[]
        {
            new AdvScenarioVoiceLineExtractor.ScenarioCommand(17, new[] { "0", "sound/data/pcm/c00/adv/30.unity3d", "hsa_00_022_04_00_00_00" }),
            new AdvScenarioVoiceLineExtractor.ScenarioCommand(12, new[] { "普通1" }),
            new AdvScenarioVoiceLineExtractor.ScenarioCommand(16, new[] { "[H]", "拾われない" }),
        };

        var map = AdvScenarioVoiceLineExtractor.BuildVoiceLineMapFromScenarioCommands(commands, "c00");

        Assert.Empty(map);
    }

    [Fact]
    public void BuildVoiceLineMapFromScenarioCommands_Ignores_Other_Personalities()
    {
        var commands = new[]
        {
            new AdvScenarioVoiceLineExtractor.ScenarioCommand(17, new[] { "0", "sound/data/pcm/c01/adv/30.unity3d", "hsa_01_022_04_00_00_00" }),
            new AdvScenarioVoiceLineExtractor.ScenarioCommand(16, new[] { "[H]", "別人格" }),
        };

        var map = AdvScenarioVoiceLineExtractor.BuildVoiceLineMapFromScenarioCommands(commands, "c00");

        Assert.Empty(map);
    }
}

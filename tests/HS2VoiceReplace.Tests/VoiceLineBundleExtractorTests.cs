using HS2VoiceReplace;
using Xunit;

namespace HS2VoiceReplace.Tests;

public sealed class VoiceLineBundleExtractorTests
{
    [Fact]
    public void EnumerateVoiceLineBundleFiles_Finds_30_And_50_Bundles()
    {
        var tempRoot = Directory.CreateTempSubdirectory("hs2vr_voicebundle_");
        try
        {
            var voiceRoot = Path.Combine(tempRoot.FullName, "abdata", "list", "h", "sound", "voice");
            Directory.CreateDirectory(voiceRoot);
            File.WriteAllText(Path.Combine(voiceRoot, "30.unity3d"), "");
            File.WriteAllText(Path.Combine(voiceRoot, "50.unity3d"), "");
            File.WriteAllText(Path.Combine(voiceRoot, "34.unity3d"), "");

            var files = VoiceLineBundleExtractor.EnumerateVoiceLineBundleFiles(tempRoot.FullName);

            Assert.Equal(2, files.Count);
            Assert.Contains(files, p => p.EndsWith(Path.Combine("voice", "30.unity3d"), StringComparison.OrdinalIgnoreCase));
            Assert.Contains(files, p => p.EndsWith(Path.Combine("voice", "50.unity3d"), StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            tempRoot.Delete(true);
        }
    }

    [Theory]
    [InlineData(@"C:\hs2\abdata\list\h\sound\voice\30.unity3d", "30")]
    [InlineData(@"C:\hs2\abdata\list\h\sound\voice\50.unity3d", "50")]
    [InlineData(@"C:\hs2\abdata\list\h\sound\voice\unexpected.unity3d", "30")]
    public void GetVoiceLinePhaseName_Uses_Bundle_File_Name(string bundlePath, string expected)
    {
        var actual = VoiceLineBundleExtractor.GetVoiceLinePhaseName(bundlePath);

        Assert.Equal(expected, actual);
    }
}

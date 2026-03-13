using HS2VoiceReplace;
using Xunit;

namespace HS2VoiceReplace.Tests;

public sealed class VoiceReplaceOutputFreshnessUtilTests
{
    [Fact]
    public void HasExpectedRebuiltBundlesFresh_ReturnsTrue_WhenBundleIsNewerThanWavs()
    {
        var tempRoot = Directory.CreateTempSubdirectory("hs2vr_fresh_bundle_");
        try
        {
            var replaceInputRoot = Path.Combine(tempRoot.FullName, "replace");
            var outWavRoot = Path.Combine(tempRoot.FullName, "wav");
            Directory.CreateDirectory(Path.Combine(replaceInputRoot, "adv"));
            Directory.CreateDirectory(Path.Combine(outWavRoot, "adv"));

            var bundle = Path.Combine(replaceInputRoot, "adv", "50.unity3d");
            var wav = Path.Combine(outWavRoot, "adv", "a.wav");
            File.WriteAllText(wav, "wav");
            File.WriteAllText(bundle, "bundle");
            File.SetLastWriteTimeUtc(wav, DateTime.UtcNow.AddMinutes(-2));
            File.SetLastWriteTimeUtc(bundle, DateTime.UtcNow);

            var ok = VoiceReplaceOutputFreshnessUtil.HasExpectedRebuiltBundlesFresh(
                new[] { ("adv/50.unity3d", "adv") },
                replaceInputRoot,
                outWavRoot);

            Assert.True(ok);
        }
        finally
        {
            tempRoot.Delete(true);
        }
    }

    [Fact]
    public void HasExpectedSplitZipmodsFresh_ReturnsFalse_WhenZipIsOlderThanBundle()
    {
        var tempRoot = Directory.CreateTempSubdirectory("hs2vr_fresh_zip_");
        try
        {
            var splitOutRoot = Path.Combine(tempRoot.FullName, "zip");
            var replaceInputRoot = Path.Combine(tempRoot.FullName, "replace");
            Directory.CreateDirectory(splitOutRoot);
            Directory.CreateDirectory(Path.Combine(replaceInputRoot, "adv"));

            var zip = Path.Combine(splitOutRoot, "HS2VoiceReplace_c02_adv.zipmod");
            var bundle = Path.Combine(replaceInputRoot, "adv", "50.unity3d");
            File.WriteAllText(zip, "zip");
            File.WriteAllText(bundle, "bundle");
            File.SetLastWriteTimeUtc(zip, DateTime.UtcNow.AddMinutes(-3));
            File.SetLastWriteTimeUtc(bundle, DateTime.UtcNow);

            var ok = VoiceReplaceOutputFreshnessUtil.HasExpectedSplitZipmodsFresh(
                new[] { ("adv", "adv/50.unity3d") },
                splitOutRoot,
                "c02",
                replaceInputRoot);

            Assert.False(ok);
        }
        finally
        {
            tempRoot.Delete(true);
        }
    }
}


using HS2VoiceReplace;
using Xunit;

namespace HS2VoiceReplace.Tests;

public sealed class VoiceReplaceFreshnessUtilTests
{
    [Fact]
    public void BuildPendingRows_ClassifiesMissingAndSignatureMismatchReasons()
    {
        var tempRoot = Directory.CreateTempSubdirectory("hs2vr_pending_");
        try
        {
            var outRoot = Path.Combine(tempRoot.FullName, "voice_replace_wav");
            Directory.CreateDirectory(Path.Combine(outRoot, "adv"));
            var okPath = Path.Combine(outRoot, "adv", "ok.wav");
            var mismatchPath = Path.Combine(outRoot, "adv", "mismatch.wav");
            File.WriteAllText(okPath, "ok");
            File.WriteAllText(mismatchPath, "mismatch");

            var rows = new[]
            {
                ("adv/ok.wav", "normal", "src1.wav"),
                ("adv/missing.wav", "normal", "src2.wav"),
                ("adv/mismatch.wav", "ero", "src3.wav"),
                ("adv/both.wav", "ero", "src4.wav"),
            };

            var sigMap = new Dictionary<string, VoiceReplaceFreshnessUtil.SignatureMapRow>(StringComparer.OrdinalIgnoreCase)
            {
                ["adv/ok.wav"] = new("adv/ok.wav", "normal", okPath, "n", "e", "n"),
                ["adv/missing.wav"] = new("adv/missing.wav", "normal", Path.Combine(outRoot, "adv", "missing.wav"), "n", "e", "n"),
                ["adv/mismatch.wav"] = new("adv/mismatch.wav", "ero", mismatchPath, "n", "e", "wrong"),
            };

            var (pending, summary) = VoiceReplaceFreshnessUtil.BuildPendingRows(rows, outRoot, sigMap, "n", "e");

            Assert.Equal(3, pending.Count);
            Assert.Equal(1, summary.MissingFileOnly);
            Assert.Equal(1, summary.SigMismatchOnly);
            Assert.Equal(1, summary.MissingAndSigMismatch);
            Assert.DoesNotContain(pending, row => row.RelativePath == "adv/ok.wav");
        }
        finally
        {
            tempRoot.Delete(true);
        }
    }

    [Fact]
    public void BuildPendingRows_ReusesExistingFilesWhenResumeFallbackIsEnabled()
    {
        var tempRoot = Directory.CreateTempSubdirectory("hs2vr_pending_resume_");
        try
        {
            var outRoot = Path.Combine(tempRoot.FullName, "voice_replace_wav");
            Directory.CreateDirectory(Path.Combine(outRoot, "adv"));
            var existingPath = Path.Combine(outRoot, "adv", "existing.wav");
            File.WriteAllText(existingPath, "ok");

            var rows = new[]
            {
                ("adv/existing.wav", "normal", "src1.wav"),
                ("adv/missing.wav", "normal", "src2.wav"),
            };

            var sigMap = new Dictionary<string, VoiceReplaceFreshnessUtil.SignatureMapRow>(StringComparer.OrdinalIgnoreCase)
            {
                ["adv/existing.wav"] = new("adv/existing.wav", "normal", existingPath, "", "", ""),
            };

            var (pending, summary) = VoiceReplaceFreshnessUtil.BuildPendingRows(
                rows,
                outRoot,
                sigMap,
                "n",
                "e",
                allowExistingFileFallbackWithoutSignature: true);

            Assert.Single(pending);
            Assert.Equal("adv/missing.wav", pending[0].RelativePath);
            Assert.Equal(0, summary.MissingFileOnly);
            Assert.Equal(0, summary.SigMismatchOnly);
            Assert.Equal(1, summary.MissingAndSigMismatch);
        }
        finally
        {
            tempRoot.Delete(true);
        }
    }
}


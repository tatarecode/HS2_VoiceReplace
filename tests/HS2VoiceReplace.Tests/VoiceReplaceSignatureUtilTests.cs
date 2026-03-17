using HS2VoiceReplace;
using Xunit;

namespace HS2VoiceReplace.Tests;

public sealed class VoiceReplaceSignatureUtilTests
{
    [Fact]
    public void NormalizeSeedVcSignature_IgnoresLegacyHeaderAndTrailingTicks()
    {
        var legacy = string.Join(
            "\n",
            "seedvc_signature_v3",
            "style_signature_v2",
            @"adv/test.wav|normal|C:\tmp\src.wav|123|1234");

        var normalized = VoiceReplaceSignatureUtil.NormalizeSeedVcSignature(legacy);

        Assert.Equal(
            string.Join(
                "\n",
                "seedvc_signature",
                "style_signature_v2",
                @"adv/test.wav|normal|size=1234"),
            normalized);
    }

    [Fact]
    public void BuildSeedVcSignature_DoesNotDependOnFileModificationTime()
    {
        var tempRoot = Directory.CreateTempSubdirectory("hs2vr_sigtest_");
        try
        {
            var src = Path.Combine(tempRoot.FullName, "source.wav");
            File.WriteAllText(src, "same content");

            var options = new PipelineOptions
            {
                StyleNormalSample = src,
                StyleEroSample = src,
            };
            var rows = new[] { ("adv/test.wav", "normal", src) };
            var styleSig = "style_sig";

            var sig1 = VoiceReplaceSignatureUtil.BuildSeedVcSignature(options, rows, styleSig);
            File.SetLastWriteTimeUtc(src, DateTime.UtcNow.AddHours(1));
            var sig2 = VoiceReplaceSignatureUtil.BuildSeedVcSignature(options, rows, styleSig);

            Assert.Equal(sig1, sig2);
        }
        finally
        {
            tempRoot.Delete(true);
        }
    }

    [Fact]
    public void BuildSampleInputSignature_ReflectsSegmentSelection()
    {
        var tempRoot = Directory.CreateTempSubdirectory("hs2vr_sample_sig_");
        try
        {
            var wav = Path.Combine(tempRoot.FullName, "style.wav");
            File.WriteAllText(wav, "abc");

            var baseOptions = new PipelineOptions
            {
                StyleNormalSample = wav,
                StyleEroSample = wav,
            };

            var noSeg = VoiceReplaceSignatureUtil.BuildSampleInputSignature(baseOptions, isEro: false);
            var withSeg = VoiceReplaceSignatureUtil.BuildSampleInputSignature(
                new PipelineOptions
                {
                    StyleNormalSample = wav,
                    StyleEroSample = wav,
                    StyleNormalSegment = new StyleSegmentSelection
                    {
                        SourceFile = wav,
                        StartSec = 1.5,
                        DurationSec = 2.5,
                    }
                },
                isEro: false);

            Assert.NotEqual(noSeg, withSeg);
            Assert.Contains("normalSeg=", withSeg);
            Assert.Contains("1.5", withSeg);
        }
        finally
        {
            tempRoot.Delete(true);
        }
    }

    [Fact]
    public void BuildSeedVcSignature_DoesNotDependOnAbsoluteSourcePath()
    {
        var tempRoot = Directory.CreateTempSubdirectory("hs2vr_seed_path_");
        try
        {
            var srcRootA = Path.Combine(tempRoot.FullName, "a");
            var srcRootB = Path.Combine(tempRoot.FullName, "b");
            Directory.CreateDirectory(srcRootA);
            Directory.CreateDirectory(srcRootB);

            var srcA = Path.Combine(srcRootA, "source.wav");
            var srcB = Path.Combine(srcRootB, "source.wav");
            File.WriteAllText(srcA, "same content");
            File.WriteAllText(srcB, "same content");

            var options = new PipelineOptions
            {
                StyleNormalSample = srcA,
                StyleEroSample = srcA,
            };
            var rowsA = new[] { ("adv/test.wav", "normal", srcA) };
            var rowsB = new[] { ("adv/test.wav", "normal", srcB) };

            var sigA = VoiceReplaceSignatureUtil.BuildSeedVcSignature(options, rowsA, "style_sig");
            var sigB = VoiceReplaceSignatureUtil.BuildSeedVcSignature(options, rowsB, "style_sig");

            Assert.Equal(sigA, sigB);
        }
        finally
        {
            tempRoot.Delete(true);
        }
    }
}


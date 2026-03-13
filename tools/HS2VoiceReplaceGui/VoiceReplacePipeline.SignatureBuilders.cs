namespace HS2VoiceReplace;

internal static partial class VoiceReplacePipeline
{
    // Signature builders define the content-addressed identity used for skip/resume decisions.
    // They intentionally focus on content and stable source identity instead of timestamps.

    private static string BuildStyleSignature(PipelineOptions o) => VoiceReplaceSignatureUtil.BuildStyleSignature(o);

    private static string BuildSampleInputSignature(PipelineOptions o, bool isEro) => VoiceReplaceSignatureUtil.BuildSampleInputSignature(o, isEro);

    private static string ComputeTextSha256(string text) => VoiceReplaceSignatureUtil.ComputeTextSha256(text);

    private static string NormalizeSeedVcSignature(string? text) => VoiceReplaceSignatureUtil.NormalizeSeedVcSignature(text);

    private static string BuildSeedVcSignature(PipelineOptions o, string manifestCsv, string styleSigCurrent)
        => VoiceReplaceSignatureUtil.BuildSeedVcSignature(
            o,
            LoadManifestRows(manifestCsv).Select(row => (row.RelativePath, row.Bucket, row.SourceFile)),
            styleSigCurrent);
}


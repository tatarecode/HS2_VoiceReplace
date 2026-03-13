namespace HS2VoiceReplace;

internal static partial class VoiceReplacePipeline
{
    // Manifest rows are shared across signature calculation, pending detection,
    // partial rebuild resolution, and bundle target mapping.
    private readonly record struct ManifestRow(string RelativePath, string Bucket, string SourceFile);

    // Partial rebuild resolves an arbitrary source/destination wav back to the
    // manifest-relative path and the original extracted source wav.
    private readonly record struct PartialInputResolved(string RelativePath, string SourceFile, string Bucket);
}



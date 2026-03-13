namespace HS2VoiceReplace;

// Preview service owns audition-only work. The preview flow writes to isolated output folders,
// so the main conversion/deploy state remains untouched while users compare samples.
internal sealed class PreviewService : IPreviewService
{
    public Task<PipelineRunResult> RunPreviewAsync(PipelineOptions options, Action<string> log, CancellationToken ct)
        => VoiceReplacePipeline.RunPreviewAsync(options, log, ct);
}


namespace HS2VoiceReplace;

// Extract service is intentionally narrow so future extraction logic can move out of the
// static pipeline without changing the WinForms-facing contract.
internal sealed class ExtractService : IExtractService
{
    public Task<PipelineRunResult> RunExtractAsync(PipelineOptions options, Action<string> log, CancellationToken ct)
        => VoiceReplacePipeline.RunExtractAsync(options, log, ct);
}


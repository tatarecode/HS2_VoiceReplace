namespace HS2VoiceReplace;

// Build/deploy keeps the widest surface because the grid dialog needs partial rebuild
// and deploy-state queries in addition to the main "run all" entry points.
internal sealed class BuildDeployService : IBuildDeployService
{
    public Task<PipelineRunResult> RunBuildAsync(PipelineOptions options, Action<string> log, CancellationToken ct)
        => VoiceReplacePipeline.RunBuildAsync(options, log, ct);

    public Task<PipelineRunResult> RunDeployAsync(PipelineOptions options, Action<string> log, CancellationToken ct)
        => VoiceReplacePipeline.RunDeployAsync(options, log, ct);

    public void RunUndeploy(PipelineOptions options, Action<string> log, CancellationToken ct)
        => VoiceReplacePipeline.RunUndeploy(options, log, ct);

    public Task<string> RebuildRelativeInFullRunAsync(
        PipelineOptions options,
        string runRoot,
        string relativePath,
        string modelBucket,
        Action<string> log,
        CancellationToken ct)
        => VoiceReplacePipeline.RebuildRelativeInFullRunAsync(options, runRoot, relativePath, modelBucket, log, ct);

    public bool HasInstalledDeployArtifacts(string deployRoot, int personalityId)
        => VoiceReplacePipeline.HasInstalledDeployArtifacts(deployRoot, personalityId);
}


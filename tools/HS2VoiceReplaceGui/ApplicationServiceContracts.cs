namespace HS2VoiceReplace;

// These interfaces define the application-level seams the WinForms layer talks to.
// They intentionally hide the static pipeline/bootstrap implementation details.
internal interface IDependencySetupService
{
    Task SetupAsync(string externalToolsRoot, string bundleRoot, Action<string> log, CancellationToken ct);
}

internal interface IExtractService
{
    Task<PipelineRunResult> RunExtractAsync(PipelineOptions options, Action<string> log, CancellationToken ct);
}

internal interface IPreviewService
{
    Task<PipelineRunResult> RunPreviewAsync(PipelineOptions options, Action<string> log, CancellationToken ct);
}

internal interface IBuildDeployService
{
    Task<PipelineRunResult> RunBuildAsync(PipelineOptions options, Action<string> log, CancellationToken ct);
    Task<PipelineRunResult> RunDeployAsync(PipelineOptions options, Action<string> log, CancellationToken ct);
    void RunUndeploy(PipelineOptions options, Action<string> log, CancellationToken ct);
    Task<string> RebuildRelativeInFullRunAsync(
        PipelineOptions options,
        string runRoot,
        string relativePath,
        string modelBucket,
        Action<string> log,
        CancellationToken ct);
    bool HasInstalledDeployArtifacts(string deployRoot, int personalityId);
}

internal interface IVoiceReplaceApplicationService
{
    Task SetupDependenciesAsync(string externalToolsRoot, string bundleRoot, Action<string> log, CancellationToken ct);
    Task<PipelineRunResult> RunExtractAsync(PipelineOptions options, Action<string> log, CancellationToken ct);
    Task<PipelineRunResult> RunPreviewAsync(PipelineOptions options, Action<string> log, CancellationToken ct);
    Task<PipelineRunResult> RunBuildAsync(PipelineOptions options, Action<string> log, CancellationToken ct);
    Task<PipelineRunResult> RunDeployAsync(PipelineOptions options, Action<string> log, CancellationToken ct);
    void RunUndeploy(PipelineOptions options, Action<string> log, CancellationToken ct);
    Task<string> RebuildRelativeInFullRunAsync(
        PipelineOptions options,
        string runRoot,
        string relativePath,
        string modelBucket,
        Action<string> log,
        CancellationToken ct);
    bool HasInstalledDeployArtifacts(string deployRoot, int personalityId);
}


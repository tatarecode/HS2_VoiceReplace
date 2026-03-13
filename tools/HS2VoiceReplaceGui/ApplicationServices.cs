namespace HS2VoiceReplace;

internal sealed class VoiceReplaceApplicationService : IVoiceReplaceApplicationService
{
    private readonly IDependencySetupService _dependencySetupService;
    private readonly IExtractService _extractService;
    private readonly IPreviewService _previewService;
    private readonly IBuildDeployService _buildDeployService;

    public VoiceReplaceApplicationService(
        IDependencySetupService? dependencySetupService = null,
        IExtractService? extractService = null,
        IPreviewService? previewService = null,
        IBuildDeployService? buildDeployService = null)
    {
        // Constructor injection keeps the form easy to wire today and easy to fake in tests later.
        _dependencySetupService = dependencySetupService ?? new DependencySetupService();
        _extractService = extractService ?? new ExtractService();
        _previewService = previewService ?? new PreviewService();
        _buildDeployService = buildDeployService ?? new BuildDeployService();
    }

    public Task SetupDependenciesAsync(string externalToolsRoot, string bundleRoot, Action<string> log, CancellationToken ct)
        => _dependencySetupService.SetupAsync(externalToolsRoot, bundleRoot, log, ct);

    public Task<PipelineRunResult> RunExtractAsync(PipelineOptions options, Action<string> log, CancellationToken ct)
        => _extractService.RunExtractAsync(options, log, ct);

    public Task<PipelineRunResult> RunPreviewAsync(PipelineOptions options, Action<string> log, CancellationToken ct)
        => _previewService.RunPreviewAsync(options, log, ct);

    public Task<PipelineRunResult> RunBuildAsync(PipelineOptions options, Action<string> log, CancellationToken ct)
        => _buildDeployService.RunBuildAsync(options, log, ct);

    public Task<PipelineRunResult> RunDeployAsync(PipelineOptions options, Action<string> log, CancellationToken ct)
        => _buildDeployService.RunDeployAsync(options, log, ct);

    public void RunUndeploy(PipelineOptions options, Action<string> log, CancellationToken ct)
        => _buildDeployService.RunUndeploy(options, log, ct);

    public Task<string> RebuildRelativeInFullRunAsync(
        PipelineOptions options,
        string runRoot,
        string relativePath,
        string modelBucket,
        Action<string> log,
        CancellationToken ct)
        => _buildDeployService.RebuildRelativeInFullRunAsync(options, runRoot, relativePath, modelBucket, log, ct);

    public bool HasInstalledDeployArtifacts(string deployRoot, int personalityId)
        => _buildDeployService.HasInstalledDeployArtifacts(deployRoot, personalityId);
}


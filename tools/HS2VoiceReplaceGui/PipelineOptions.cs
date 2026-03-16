namespace HS2VoiceReplace;

// Represents the normalized pipeline input model shared by extract, preview, build, and deploy use-cases.

internal sealed class PipelineOptions
{
    public string BundleRoot { get; init; } = "";
    public string ExternalToolsRoot { get; init; } = "";
    public string OutputBaseRoot { get; init; } = "";
    public string Hs2Root { get; init; } = "";
    public int TargetPersonalityId { get; init; }
    public string StyleNormalSample { get; init; } = "";
    public string StyleEroSample { get; init; } = "";
    public bool DeployAfterBuild { get; init; }
    public bool SkipCompletedProcesses { get; init; }
    public SeedVcUiSettings SeedVc { get; init; } = SeedVcUiSettings.CreateDefault();
    public string StyleNormalSampleDisplayName { get; init; } = "";
    public string StyleEroSampleDisplayName { get; init; } = "";
    public string SeedVcSummary { get; init; } = "";
    public StyleSegmentSelection? StyleNormalSegment { get; init; }
    public StyleSegmentSelection? StyleEroSegment { get; init; }
    public string ResumeRunRoot { get; init; } = "";
}



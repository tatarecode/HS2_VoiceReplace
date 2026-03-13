namespace HS2VoiceReplace;

// Represents the persisted deploy-state document used to support reversible per-personality deployment.

internal sealed class DeployStateManifest
{
    public string RuntimeDllFileName { get; set; } = VoiceReplaceNames.RuntimePluginFileName;
    public List<string> DeployedZipmods { get; set; } = new();
    public List<string> DisabledZipmods { get; set; } = new();
    public int PersonalityId { get; set; }
    public string? RunRoot { get; set; }
    public DateTime DeployedAtUtc { get; set; } = DateTime.UtcNow;
}



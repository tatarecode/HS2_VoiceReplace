namespace HS2VoiceReplace;

// Thin application service that exposes dependency setup as a use-case for the UI layer.

internal sealed class DependencySetupService : IDependencySetupService
{
    public Task SetupAsync(string externalToolsRoot, string bundleRoot, Action<string> log, CancellationToken ct)
        => DependencyBootstrapper.SetupAsync(externalToolsRoot, bundleRoot, log, ct);
}



using System.Text;

namespace HS2VoiceReplace;

public sealed partial class MainForm
{
    private void LoadBootstrapSettings()
    {
        try
        {
            if (!File.Exists(BootstrapSettingsPath))
                return;

            var json = File.ReadAllText(BootstrapSettingsPath, Encoding.UTF8);
            var s = System.Text.Json.JsonSerializer.Deserialize<BootstrapUiSettings>(json);
            var desired = ResolveConfiguredOutputRoot(s?.OutputRoot);
            ApplyOutputRootChange(desired, reloadSampleAssets: false);
        }
        catch (Exception ex)
        {
            AppendLog(T("log.settingsLoadFailed", ex.Message));
        }
    }

    private void SaveBootstrapSettings()
    {
        try
        {
            Directory.CreateDirectory(AppContext.BaseDirectory);

            if (string.Equals(_activeOutputRoot, _defaultOutputRoot, StringComparison.OrdinalIgnoreCase))
            {
                if (File.Exists(BootstrapSettingsPath))
                    File.Delete(BootstrapSettingsPath);
                return;
            }

            var s = new BootstrapUiSettings
            {
                OutputRoot = _activeOutputRoot,
            };
            var json = System.Text.Json.JsonSerializer.Serialize(s, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(BootstrapSettingsPath, json, new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            AppendLog(T("log.settingsSaveFailed", ex.Message));
        }
    }

    private void ApplyOutputRootChangeFromUi(bool reloadSampleAssets)
    {
        var desired = ResolveConfiguredOutputRoot(_txtOutputRoot.Text);
        ApplyOutputRootChange(desired, reloadSampleAssets);
    }

    private void ApplyOutputRootChange(string desiredRoot, bool reloadSampleAssets)
    {
        var normalized = ResolveConfiguredOutputRoot(desiredRoot);
        var oldRoot = _activeOutputRoot;
        if (string.Equals(oldRoot, normalized, StringComparison.OrdinalIgnoreCase))
        {
            _txtOutputRoot.Text = normalized;
            return;
        }

        var oldExternalDefault = Path.Combine(oldRoot, "external_tools");
        var newExternalDefault = Path.Combine(normalized, "external_tools");
        var currentExternal = string.IsNullOrWhiteSpace(_txtExternalToolsRoot.Text)
            ? string.Empty
            : Path.GetFullPath(Environment.ExpandEnvironmentVariables(_txtExternalToolsRoot.Text.Trim()));

        _activeOutputRoot = normalized;
        _txtOutputRoot.Text = normalized;
        Directory.CreateDirectory(_activeOutputRoot);

        if (string.IsNullOrWhiteSpace(currentExternal) ||
            string.Equals(currentExternal, oldExternalDefault, StringComparison.OrdinalIgnoreCase))
        {
            _txtExternalToolsRoot.Text = newExternalDefault;
        }

        var oldGuiRunsRoot = Path.Combine(oldRoot, "gui_runs");
        if (string.IsNullOrWhiteSpace(_lastGridRunRoot) ||
            _lastGridRunRoot.StartsWith(oldGuiRunsRoot, StringComparison.OrdinalIgnoreCase))
        {
            var relativeRunRoot = string.IsNullOrWhiteSpace(_lastGridRunRoot) || !Directory.Exists(oldGuiRunsRoot)
                ? string.Empty
                : Path.GetRelativePath(oldGuiRunsRoot, _lastGridRunRoot);
            _lastGridRunRoot = string.IsNullOrWhiteSpace(relativeRunRoot) ||
                               string.Equals(relativeRunRoot, ".", StringComparison.OrdinalIgnoreCase)
                ? Path.Combine(_activeOutputRoot, "gui_runs", $"resume_c{GetSelectedPersonalityId():00}")
                : Path.Combine(_activeOutputRoot, "gui_runs", relativeRunRoot);
        }

        if (reloadSampleAssets)
        {
            LoadSampleAssets();
            SyncSelectedSampleAssetsToTextFields();
            _embeddedGrid?.SetRunRoot(_lastGridRunRoot, reload: true);
            RefreshSampleSignatureDisplay();
            RefreshActionAvailability();
        }
    }

    private string ResolveConfiguredOutputRoot(string? text)
    {
        var raw = string.IsNullOrWhiteSpace(text) ? _defaultOutputRoot : Environment.ExpandEnvironmentVariables(text.Trim());
        return Path.GetFullPath(raw);
    }

    private static string BuildDefaultOutputRoot(string baseDirectory)
    {
        var repoRoot = TryFindRepositoryRoot(baseDirectory) ?? Path.GetFullPath(baseDirectory);
        return Path.Combine(repoRoot, ".hs2voicereplace");
    }

    private static string? TryFindRepositoryRoot(string startPath)
    {
        var current = new DirectoryInfo(Path.GetFullPath(startPath));
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "HS2VoiceReplace.sln")) &&
                Directory.Exists(Path.Combine(current.FullName, "tools")) &&
                Directory.Exists(Path.Combine(current.FullName, "mods_src")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }
}

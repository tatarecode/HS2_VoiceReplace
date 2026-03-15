using System.Text;

namespace HS2VoiceReplace;

// Loads and saves persisted UI/application settings so runtime state can survive between launches.

public sealed partial class MainForm
{
    private void LoadUiSettings()
    {
        try
        {
            ApplyOutputRootChangeFromUi(reloadSampleAssets: false);
            if (!File.Exists(UiSettingsPath))
                return;
            var json = File.ReadAllText(UiSettingsPath, Encoding.UTF8);
            var s = System.Text.Json.JsonSerializer.Deserialize<PersistedUiSettings>(json);
            if (s == null)
                return;

            var loadedLang = ParseUiLanguageCode(s.UiLanguageCode);
            if (loadedLang != _uiLanguage)
                ChangeUiLanguage(loadedLang);

            if (!string.IsNullOrWhiteSpace(s.ExternalToolsRoot)) _txtExternalToolsRoot.Text = s.ExternalToolsRoot!;
            var hs2Root = !string.IsNullOrWhiteSpace(s.Hs2Root)
                ? s.Hs2Root
                : !string.IsNullOrWhiteSpace(s.SourceHs2Root)
                    ? s.SourceHs2Root
                    : s.DeployHs2Root;
            if (!string.IsNullOrWhiteSpace(hs2Root))
                SetConfiguredHs2Root(hs2Root);
            if (!string.IsNullOrWhiteSpace(s.NormalSample)) _txtNormalSample.Text = s.NormalSample!;
            if (!string.IsNullOrWhiteSpace(s.EroSample)) _txtEroSample.Text = s.EroSample!;
            if (!string.IsNullOrWhiteSpace(s.NormalSampleAssetId)) _normalSampleAssetId = s.NormalSampleAssetId!;
            if (!string.IsNullOrWhiteSpace(s.EroSampleAssetId)) _eroSampleAssetId = s.EroSampleAssetId!;
            if (!string.IsNullOrWhiteSpace(s.LastGridRunRoot)) _lastGridRunRoot = s.LastGridRunRoot!;

            if (s.SkipCompleted.HasValue) _chkSkipCompleted.Checked = s.SkipCompleted.Value;

            if (s.TargetPersonalityId.HasValue)
            {
                var idx = Array.FindIndex(PersonalityChoices, x => x.Id == s.TargetPersonalityId.Value);
                if (idx >= 0) _cmbPersonality.SelectedIndex = idx;
            }

            if (s.SeedVc != null)
            {
                _seedVc = s.SeedVc.Clone();
                _lblSeedVcSummary.Text = _seedVc.ToSummaryString();
            }

            _manualNormalSegment = s.ManualNormalSegment?.Clone();
            _manualEroSegment = s.ManualEroSegment?.Clone();
            _txtNormalSegment.Text = _manualNormalSegment?.ToShortString() ?? string.Empty;
            _txtEroSegment.Text = _manualEroSegment?.ToShortString() ?? string.Empty;

            if (s.WindowWidth.HasValue && s.WindowHeight.HasValue)
            {
                Width = Math.Max(MinimumSize.Width, s.WindowWidth.Value);
                Height = Math.Max(MinimumSize.Height, s.WindowHeight.Value);
            }
            RefreshSampleSignatureDisplay();
        }
        catch (Exception ex)
        {
            AppendLog(T("log.settingsLoadFailed", ex.Message));
        }
    }

    private void SaveUiSettings()
    {
        try
        {
            ApplyOutputRootChangeFromUi(reloadSampleAssets: false);
            EnsureSelectedSampleAssets();
            SyncSelectedSampleAssetsToTextFields();
            Directory.CreateDirectory(_activeOutputRoot);
            SaveBootstrapSettings();
            if (_embeddedGrid is { IsDisposed: false } && !string.IsNullOrWhiteSpace(_embeddedGrid.RunRoot))
                _lastGridRunRoot = _embeddedGrid.RunRoot;
            var s = new PersistedUiSettings
            {
                Hs2Root = GetConfiguredHs2Root(),
                ExternalToolsRoot = _txtExternalToolsRoot.Text.Trim(),
                SourceHs2Root = GetConfiguredHs2Root(),
                DeployHs2Root = GetConfiguredHs2Root(),
                TargetPersonalityId = GetSelectedPersonalityId(),
                NormalSample = _txtNormalSample.Text.Trim(),
                EroSample = _txtEroSample.Text.Trim(),
                SkipCompleted = _chkSkipCompleted.Checked,
                SeedVc = _seedVc.Clone(),
                ManualNormalSegment = _manualNormalSegment?.Clone(),
                ManualEroSegment = _manualEroSegment?.Clone(),
                LastGridRunRoot = _lastGridRunRoot,
                WindowWidth = Width,
                WindowHeight = Height,
                UiLanguageCode = GetUiLanguageCode(_uiLanguage),
                NormalSampleAssetId = _normalSampleAssetId,
                EroSampleAssetId = _eroSampleAssetId,
            };
            var json = System.Text.Json.JsonSerializer.Serialize(s, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(UiSettingsPath, json, new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            AppendLog(T("log.settingsSaveFailed", ex.Message));
        }
    }
}



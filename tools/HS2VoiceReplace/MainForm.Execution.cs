using System.Text;

namespace HS2VoiceReplace;

// Contains run/stop command handlers that bridge UI actions to application services without embedding pipeline logic directly in the main form.

public sealed partial class MainForm
{
    private async Task RunSetupAsync()
    {
        SetBusyState(isBusy: true);
        _cts = new CancellationTokenSource();

        try
        {
            var root = string.IsNullOrWhiteSpace(_txtExternalToolsRoot.Text)
                ? Path.Combine(_outputRootFixed, "external_tools")
                : Path.GetFullPath(_txtExternalToolsRoot.Text.Trim());

            AppendLog($"setup root: {root}");
            await _appService.SetupDependenciesAsync(root, _bundleRootFixed, AppendLog, _cts.Token);
            _txtExternalToolsRoot.Text = root;
            AppendLog(T("setup.completed"));
            MessageBox.Show(this, T("setup.completedMessage"), T("app.title"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (OperationCanceledException)
        {
            AppendLog(T("setup.cancelled"));
        }
        catch (Exception ex)
        {
            AppendLog("[error] " + ex.Message);
            MessageBox.Show(this, ex.ToString(), T("setup.error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusyState(isBusy: false);
            _cts?.Dispose();
            _cts = null;
        }
    }

    private async Task RunExtractAsync()
    {
        SetBusyState(isBusy: true);
        _txtLog.Clear();
        _cts = new CancellationTokenSource();

        try
        {
            var runRoot = GetCurrentRunRoot();
            var options = BuildOptions(runRoot);
            var result = await _appService.RunExtractAsync(options, AppendLog, _cts.Token);
            _lastGridRunRoot = result.RunRoot;
            _embeddedGrid?.SetRunRoot(result.RunRoot, reload: true);
            AppendLog(T("extract.completed"));
            MessageBox.Show(this, T("extract.completedMessage"), T("app.title"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (OperationCanceledException)
        {
            AppendLog(T("extract.cancelled"));
        }
        catch (Exception ex)
        {
            AppendLog("[error] " + ex.Message);
            MessageBox.Show(this, ex.ToString(), T("extract.error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusyState(isBusy: false);
            _cts?.Dispose();
            _cts = null;
        }
    }

    private async Task RunPreviewAsync()
    {
        SetBusyState(isBusy: true);
        _txtLog.Clear();
        _cts = new CancellationTokenSource();
        var sig = ComputeCurrentSampleSignatures();
        AppendLog(UiTextCatalog.Get(_uiLanguage, "log.sampleSignature", sig.Normal, sig.Ero));

        try
        {
            TouchSelectedSampleAssetsAsUsed();
            var previewRunRoot = Path.Combine(_outputRootFixed, "gui_runs", $"preview_c{GetSelectedPersonalityId():00}");
            var options = BuildOptions(previewRunRoot);
            var result = await _appService.RunPreviewAsync(options, AppendLog, _cts.Token);

            _lastPreviewNormalPath = result.PreviewNormalWav ?? string.Empty;
            _lastPreviewEroPath = result.PreviewEroWav ?? string.Empty;
            _txtPreviewNormal.Text = _lastPreviewNormalPath;
            _txtPreviewEro.Text = _lastPreviewEroPath;
            _btnPlayPreviewNormal.Enabled = File.Exists(_lastPreviewNormalPath);
            _btnPlayPreviewEro.Enabled = File.Exists(_lastPreviewEroPath);

            AppendLog(T("preview.completed"));
            MessageBox.Show(this, T("preview.completedMessage"), T("app.title"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (OperationCanceledException)
        {
            AppendLog(T("preview.cancelled"));
        }
        catch (Exception ex)
        {
            AppendLog("[error] " + ex.Message);
            MessageBox.Show(this, ex.ToString(), T("preview.error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusyState(isBusy: false);
            _cts?.Dispose();
            _cts = null;
        }
    }

    private async Task<string> RunPipelineAsync(Action<string>? onProgressLine = null, string? runRootOverride = null)
    {
        SetBusyState(isBusy: true);
        _txtLog.Clear();
        _cts = new CancellationTokenSource();
        var sig = ComputeCurrentSampleSignatures();

        try
        {
            var options = BuildOptions(runRootOverride);
            void LogAndReport(string line)
            {
                AppendLog(line);
                onProgressLine?.Invoke(line);
            }

            TouchSelectedSampleAssetsAsUsed();
            LogAndReport(UiTextCatalog.Get(_uiLanguage, "log.sampleSignature", sig.Normal, sig.Ero));
            var result = await _appService.RunBuildAsync(options, LogAndReport, _cts.Token);
            LogAndReport(T("build.completed"));
            _embeddedGrid?.SetRunRoot(result.RunRoot, reload: true);
            return result.RunRoot;
        }
        catch (OperationCanceledException)
        {
            var line = T("build.cancelled");
            AppendLog(line);
            onProgressLine?.Invoke(line);
            throw;
        }
        catch (Exception ex)
        {
            var line = "[error] " + ex.Message;
            AppendLog(line);
            onProgressLine?.Invoke(line);
            throw;
        }
        finally
        {
            SetBusyState(isBusy: false);
            _cts?.Dispose();
            _cts = null;
        }
        throw new InvalidOperationException(T("build.notCompleted"));
    }

    private async Task RunDeployAsync()
    {
        SetBusyState(isBusy: true);
        _txtLog.Clear();
        _cts = new CancellationTokenSource();

        try
        {
            var runRoot = GetCurrentRunRoot();
            var options = BuildOptions(runRoot, deployToBackup: true);
            var result = await _appService.RunDeployAsync(options, AppendLog, _cts.Token);
            _lastGridRunRoot = result.RunRoot;
            AppendLog(T("deploy.completed"));
            MessageBox.Show(this, T("deploy.completedMessage"), T("app.title"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (OperationCanceledException)
        {
            AppendLog(T("deploy.cancelled"));
        }
        catch (Exception ex)
        {
            AppendLog("[error] " + ex.Message);
            MessageBox.Show(this, ex.ToString(), T("deploy.error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusyState(isBusy: false);
            _cts?.Dispose();
            _cts = null;
        }
    }

    private async Task RunUndeployAsync()
    {
        SetBusyState(isBusy: true);
        _txtLog.Clear();
        _cts = new CancellationTokenSource();

        try
        {
            var options = BuildOptions(GetCurrentRunRoot());
            await Task.Run(() => _appService.RunUndeploy(options, AppendLog, _cts.Token), _cts.Token);
            AppendLog(T("undeploy.completed"));
            MessageBox.Show(this, T("undeploy.completedMessage"), T("app.title"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (OperationCanceledException)
        {
            AppendLog(T("undeploy.cancelled"));
        }
        catch (Exception ex)
        {
            AppendLog("[error] " + ex.Message);
            MessageBox.Show(this, ex.ToString(), T("undeploy.error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusyState(isBusy: false);
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void SetBusyState(bool isBusy)
    {
        _btnSeedVcSettings.Enabled = true;
        if (_btnSampleDialogCancel != null && !_btnSampleDialogCancel.IsDisposed)
            _btnSampleDialogCancel.Enabled = isBusy;
        _btnEditNormalSegment.Enabled = true;
        _btnEditEroSegment.Enabled = true;
        _btnClearNormalSegment.Enabled = true;
        _btnClearEroSegment.Enabled = true;
        RefreshActionAvailability();
    }

    private PipelineOptions BuildOptions(string? runRootOverride = null, bool deployToBackup = false)
    {
        string ResolveUserPath(string p) => Path.GetFullPath(Environment.ExpandEnvironmentVariables(p));
        EnsureSelectedSampleAssets();
        SyncSelectedSampleAssetsToTextFields();
        var normalAsset = GetSampleAssetById(_normalSampleAssetId);
        var eroAsset = GetSampleAssetById(_eroSampleAssetId) ?? normalAsset;
        var normalPathRaw = normalAsset != null ? GetAssetAbsolutePath(normalAsset) : _txtNormalSample.Text.Trim();
        var eroPathRaw = eroAsset != null ? GetAssetAbsolutePath(eroAsset) : _txtEroSample.Text.Trim();
        if (string.IsNullOrWhiteSpace(normalPathRaw))
            throw new InvalidOperationException(T("error.normalSampleMissing"));
        var normal = ResolveUserPath(normalPathRaw);
        var ero = string.IsNullOrWhiteSpace(eroPathRaw) ? normal : ResolveUserPath(eroPathRaw);
        var seed = _seedVc.Clone();

        return new PipelineOptions
        {
            BundleRoot = _bundleRootFixed,
            ExternalToolsRoot = string.IsNullOrWhiteSpace(_txtExternalToolsRoot.Text) ? "" : ResolveUserPath(_txtExternalToolsRoot.Text.Trim()),
            OutputBaseRoot = _outputRootFixed,
            SourceHs2Root = Path.GetFullPath(_txtSourceHs2Root.Text.Trim()),
            DeployHs2Root = Path.GetFullPath(_txtDeployRoot.Text.Trim()),
            TargetPersonalityId = GetSelectedPersonalityId(),
            StyleNormalSample = normal,
            StyleEroSample = ero,
            DeployToBackup = deployToBackup,
            SkipCompletedProcesses = _chkSkipCompleted.Checked,
            SeedVc = seed,
            StyleNormalSegment = null,
            StyleEroSegment = null,
            ResumeRunRoot = string.IsNullOrWhiteSpace(runRootOverride) ? "" : Path.GetFullPath(runRootOverride.Trim()),
        };
    }

    private int GetSelectedPersonalityId()
    {
        if (_cmbPersonality.SelectedItem is PersonalityChoiceItem item)
            return item.Id;
        return 13;
    }

    private string GetCurrentRunRoot()
        => Path.Combine(_outputRootFixed, "gui_runs", $"resume_c{GetSelectedPersonalityId():00}");

    private bool HasAssignedSampleAudio()
    {
        try
        {
            EnsureSelectedSampleAssets();
            SyncSelectedSampleAssetsToTextFields();
            var normalAsset = GetSampleAssetById(_normalSampleAssetId);
            var eroAsset = GetSampleAssetById(_eroSampleAssetId) ?? normalAsset;
            var normalPath = normalAsset != null ? GetAssetAbsolutePath(normalAsset) : _txtNormalSample.Text.Trim();
            var eroPath = eroAsset != null ? GetAssetAbsolutePath(eroAsset) : _txtEroSample.Text.Trim();
            if (string.IsNullOrWhiteSpace(normalPath))
                return false;
            if (!File.Exists(Path.GetFullPath(normalPath)))
                return false;
            if (string.IsNullOrWhiteSpace(eroPath))
                return true;
            return File.Exists(Path.GetFullPath(eroPath));
        }
        catch
        {
            return false;
        }
    }

    private bool HasExtractedDataForCurrentPersonality()
    {
        try
        {
            var runRoot = GetCurrentRunRoot();
            var pid = $"c{GetSelectedPersonalityId():00}";
            var manifest = Path.Combine(runRoot, "rvc_batches", "routing_manifest.csv");
            var wavRoot = Path.Combine(runRoot, "voice_extract_wav", pid);
            return File.Exists(manifest) &&
                   Directory.Exists(wavRoot) &&
                   Directory.GetFiles(wavRoot, "*.wav", SearchOption.AllDirectories).Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private bool HasDeployArtifactsForCurrentPersonality()
    {
        try
        {
            var runRoot = GetCurrentRunRoot();
            var pid = $"c{GetSelectedPersonalityId():00}";
            var splitRoot = Path.Combine(runRoot, "voice_replace_mod_split");
            return Directory.Exists(splitRoot) &&
                   Directory.GetFiles(splitRoot, $"HS2VoiceReplace_{pid}_*.zipmod", SearchOption.TopDirectoryOnly).Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private bool CanPreview()
        => Directory.Exists(_txtSourceHs2Root.Text.Trim()) && HasAssignedSampleAudio();

    private bool CanRunAll()
        => HasExtractedDataForCurrentPersonality() && HasAssignedSampleAudio();

    private bool CanDeploy()
        => Directory.Exists(_txtDeployRoot.Text.Trim()) && HasDeployArtifactsForCurrentPersonality();

    private bool CanUndeploy()
        => Directory.Exists(_txtDeployRoot.Text.Trim()) && _appService.HasInstalledDeployArtifacts(_txtDeployRoot.Text.Trim(), GetSelectedPersonalityId());

    private void RefreshActionAvailability()
    {
        var isBusy = _cts != null;
        _btnSetup.Enabled = !isBusy;
        _btnExtract.Enabled = !isBusy && Directory.Exists(_txtSourceHs2Root.Text.Trim());
        _btnPreview.Enabled = !isBusy && CanPreview();
        _btnDeploy.Enabled = !isBusy && CanDeploy();
        _btnUndeploy.Enabled = !isBusy && CanUndeploy();
        _btnCancel.Enabled = isBusy;
        _embeddedGrid?.RefreshCommandAvailability();
    }

    private bool RequestCancelCurrentOperation()
    {
        if (_cts == null || _cts.IsCancellationRequested)
            return false;
        _cts.Cancel();
        AppendLog(T("log.cancelRequested"));
        return true;
    }
}



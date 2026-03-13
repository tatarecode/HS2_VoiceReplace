namespace HS2VoiceReplace;

public sealed partial class MainForm
{
    private void RecreateEmbeddedGrid()
    {
        try
        {
            _gridHost.Controls.Clear();
            _embeddedGrid?.Dispose();

            SyncGridRunRootWithSelectedPersonality(updateEmbeddedGrid: false);
            var suggested = string.IsNullOrWhiteSpace(_lastGridRunRoot)
                ? Path.Combine(_activeOutputRoot, "gui_runs", $"resume_c{GetSelectedPersonalityId():00}")
                : _lastGridRunRoot;

            _embeddedGrid = new PartialRebuildGridDialog(
                suggested,
                AppendLog,
                async row =>
                {
                    var options = BuildOptions();
                    var outWav = await _appService.RebuildRelativeInFullRunAsync(
                        options,
                        row.RunRoot,
                        row.RelativePath,
                        row.Bucket,
                        AppendLog,
                        CancellationToken.None);
                    return outWav;
                },
                async progress =>
                {
                    var runRoot = await RunPipelineAsync(progress, GetCurrentRunRoot());
                    _lastGridRunRoot = runRoot;
                    return runRoot;
                },
                CanRunAll,
                RequestCancelCurrentOperation,
                showCloseButton: false,
                useEnglish: _uiLanguage == UiLanguage.En)
            {
                TopLevel = false,
                FormBorderStyle = FormBorderStyle.None,
                Dock = DockStyle.Fill,
            };
            _gridHost.Controls.Add(_embeddedGrid);
            _embeddedGrid.Show();
            _embeddedGrid.SetRunRoot(suggested, reload: true);
        }
        catch (Exception ex)
        {
            AppendLog(T("log.embeddedGridInitFailed", ex.Message));
        }
    }

    private void ReflowLayout()
    {
        if (_mainSplit != null && WindowState != FormWindowState.Minimized)
        {
            var splitHeight = _mainSplit.Height;
            var splitter = _mainSplit.SplitterWidth;
            if (splitHeight > splitter)
            {
                var desiredLog = Math.Max(_mainSplit.Panel2MinSize, Math.Min(260, ClientSize.Height / 3));
                var desiredTop = ClientSize.Height - desiredLog;

                var minTop = Math.Max(0, _mainSplit.Panel1MinSize);
                var maxTop = splitHeight - splitter - _mainSplit.Panel2MinSize;

                if (maxTop < 0)
                    maxTop = splitHeight - splitter;
                if (maxTop < 0)
                    maxTop = 0;
                if (minTop > maxTop)
                    minTop = maxTop;

                var next = Math.Clamp(desiredTop, minTop, maxTop);
                if (_mainSplit.SplitterDistance != next)
                {
                    try
                    {
                        _mainSplit.SplitterDistance = next;
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        // Ignore transient invalid ranges during minimize/restore layout transitions.
                    }
                }
            }
        }
        var wrap = Math.Max(420, ClientSize.Width - 220);
        _lblSeedVcSummary.MaximumSize = new Size(wrap, 0);
        _lblSampleSignature.MaximumSize = new Size(wrap, 0);
    }

    private void SyncGridRunRootWithSelectedPersonality(bool updateEmbeddedGrid)
    {
        var suggested = Path.Combine(_activeOutputRoot, "gui_runs", $"resume_c{GetSelectedPersonalityId():00}");
        _lastGridRunRoot = suggested;
        if (updateEmbeddedGrid)
            _embeddedGrid?.SetRunRoot(_lastGridRunRoot, reload: true);
    }

    private void SetDefaults()
    {
        _txtBundleRoot.Text = _bundleRootFixed;
        var defaultExternalRoot = Path.Combine(_activeOutputRoot, "external_tools");
        _txtExternalToolsRoot.Text = Environment.GetEnvironmentVariable("HS2VR_TOOLS_ROOT") ?? defaultExternalRoot;
        _txtOutputRoot.Text = _activeOutputRoot;
        _txtSourceHs2Root.Text = string.Empty;
        _txtDeployRoot.Text = string.Empty;
        ApplyV1NaturalPreset(_seedVc);
        _lblSeedVcSummary.Text = _seedVc.ToSummaryString();
        _cmbPersonality.Items.Clear();
        foreach (var p in PersonalityChoices)
            _cmbPersonality.Items.Add(p);
        var defaultIndex = Array.FindIndex(PersonalityChoices, p => p.Id == 0);
        _cmbPersonality.SelectedIndex = defaultIndex >= 0 ? defaultIndex : 0;
        _lastGridRunRoot = Path.Combine(_activeOutputRoot, "gui_runs", $"resume_c{GetSelectedPersonalityId():00}");
        RefreshSampleSignatureDisplay();
    }

}


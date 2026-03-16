namespace HS2VoiceReplace;

// Builds the waveform range-selector UI and keeps its control creation isolated from audio behavior.

internal sealed partial class SampleRangeSelectorDialog
{
    public SampleRangeSelectorDialog(string sourceFile, StyleSegmentSelection? initialSelection, string? ffmpegExe)
    {
        _sourceFile = Path.GetFullPath(sourceFile);
        _ffmpegExe = ffmpegExe;
        _initialSelection = initialSelection?.Clone();

        Text = T("dialog.rangeEditor.title");
        StartPosition = FormStartPosition.CenterParent;
        UiSizeHelper.ApplyDialogSize(this, new Size(1240, 920), new Size(980, 760), fixedSize: true);
        UiSizeHelper.FitButton(_btnPlaySelection, 120, 36);
        UiSizeHelper.FitButton(_btnStopPlayback, 90, 36);
        _waveBox.MinimumSize = new Size(0, 480);
        _waveBox.Height = 520;
        _waveBox.Dock = DockStyle.Top;
        _startTrack.Dock = DockStyle.Top;

        var shell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
        };
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(shell);

        var host = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(10) };
        shell.Controls.Add(host, 0, 0);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 4,
            Width = Math.Max(760, ClientSize.Width - 40),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        host.Controls.Add(root);

        _lblInfo.Text = T("dialog.rangeEditor.input", _sourceFile);
        _lblLoading.Text = T("dialog.rangeEditor.loading");
        var infoPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0, 0, 0, 8),
        };
        infoPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        infoPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        infoPanel.Controls.Add(_lblLoading, 0, 0);
        infoPanel.Controls.Add(_lblInfo, 0, 1);
        root.Controls.Add(infoPanel, 0, 0);
        root.Controls.Add(_waveBox, 0, 1);
        root.Controls.Add(_startTrack, 0, 2);

        var ctrl = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        ctrl.Controls.Add(new Label { AutoSize = true, Text = T("dialog.rangeEditor.start") });
        ctrl.Controls.Add(_numStart);
        ctrl.Controls.Add(new Label { AutoSize = true, Text = T("dialog.rangeEditor.length") });
        ctrl.Controls.Add(_numDuration);
        ctrl.Controls.Add(_btnPlaySelection);
        ctrl.Controls.Add(_btnStopPlayback);
        ctrl.Controls.Add(_lblRange);
        root.Controls.Add(ctrl, 0, 3);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(10),
        };
        var ok = new Button { Text = T("button.apply"), DialogResult = DialogResult.OK, Width = 120, Height = 36 };
        var cancel = new Button { Text = T("button.cancel"), DialogResult = DialogResult.Cancel, Width = 120, Height = 36 };
        UiSizeHelper.FitButton(ok, 90, 36);
        UiSizeHelper.FitButton(cancel, 100, 36);
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        shell.Controls.Add(buttons, 0, 1);
        AcceptButton = ok;
        CancelButton = cancel;

        void FitDialogToContent()
        {
            var workingArea = Screen.FromPoint(Cursor.Position).WorkingArea;
            var chrome = SizeFromClientSize(Size.Empty);
            var maxClientWidth = Math.Max(900, workingArea.Width - 64 - chrome.Width);
            var maxClientHeight = Math.Max(720, workingArea.Height - 64 - chrome.Height);
            var footerPreferred = buttons.GetPreferredSize(new Size(maxClientWidth, 0));
            var hostPadding = host.Padding.Horizontal + 24;
            var hostVerticalPadding = host.Padding.Vertical + 24;

            var probeWidth = Math.Min(1240, maxClientWidth);
            root.Width = Math.Max(760, probeWidth - hostPadding);
            _lblInfo.MaximumSize = new Size(root.Width - 16, 0);
            root.PerformLayout();

            var preferred = root.GetPreferredSize(new Size(root.Width, 0));
            var desiredWidth = Math.Clamp(preferred.Width + hostPadding, 980, maxClientWidth);
            var desiredHeight = Math.Clamp(preferred.Height + hostVerticalPadding + footerPreferred.Height, 760, maxClientHeight);

            ClientSize = new Size(desiredWidth, desiredHeight);
            RefreshScrollableLayout();
        }

        void RefreshScrollableLayout()
        {
            var contentWidth = Math.Max(760, host.ClientSize.Width - host.Padding.Horizontal - 8);
            root.Width = contentWidth;
            _lblInfo.MaximumSize = new Size(Math.Max(320, contentWidth - 16), 0);
            root.PerformLayout();
            var preferred = root.GetPreferredSize(new Size(contentWidth, 0));
            host.AutoScrollMinSize = new Size(preferred.Width, preferred.Height + 12);
        }

        host.SizeChanged += (_, _) => RefreshScrollableLayout();
        Shown += (_, _) => FitDialogToContent();

        _numStart.ValueChanged += (_, _) => { if (!_syncing) OnStartChangedByNumeric(); };
        _numDuration.ValueChanged += (_, _) => { if (!_syncing) OnDurationChanged(); };
        _startTrack.ValueChanged += (_, _) => { if (!_syncing) OnStartChangedByTrack(); };
        _waveBox.Paint += (_, e) => DrawWaveform(e.Graphics, _waveBox.ClientRectangle);
        _waveBox.MouseDown += (_, e) => SeekFromMouse(e.X);
        _waveBox.MouseMove += (_, e) => { if (e.Button == MouseButtons.Left) SeekFromMouse(e.X); };
        _btnPlaySelection.Click += async (_, _) => await PlaySelectionAsync();
        _btnStopPlayback.Click += (_, _) => StopPlayback();

        SetWaveControlsEnabled(false);
        _lblRange.Text = T("dialog.rangeEditor.selectionEmpty");

        Shown += async (_, _) => await LoadWaveDataAsync();

        FormClosing += (_, _) =>
        {
            if (DialogResult == DialogResult.OK)
            {
                Selection = new StyleSegmentSelection
                {
                    SourceFile = _sourceFile,
                    StartSec = (double)_numStart.Value,
                    DurationSec = (double)_numDuration.Value,
                };
            }
            StopPlayback();
            CleanupTemp();
        };
    }

    private void SetWaveControlsEnabled(bool enabled)
    {
        _startTrack.Enabled = enabled;
        _numStart.Enabled = enabled;
        _numDuration.Enabled = enabled;
        _btnPlaySelection.Enabled = enabled;
        _waveBox.Enabled = enabled;
    }

    private void OnStartChangedByNumeric()
    {
        SyncTrackFromStart();
        UpdateRangeLabel();
        _waveBox.Invalidate();
    }

    private void OnDurationChanged()
    {
        ApplyDurationRange();
        UpdateRangeLabel();
        _waveBox.Invalidate();
    }

    private void OnStartChangedByTrack()
    {
        var maxStart = Math.Max(0.0, _totalSec - (double)_numDuration.Value);
        var ratio = _startTrack.Maximum <= 0 ? 0.0 : _startTrack.Value / (double)_startTrack.Maximum;
        _syncing = true;
        _numStart.Value = ClampToDecimal((decimal)(ratio * maxStart), _numStart.Minimum, _numStart.Maximum);
        _syncing = false;
        UpdateRangeLabel();
        _waveBox.Invalidate();
    }

    private void ApplyDurationRange()
    {
        var maxStart = Math.Max(0.0, _totalSec - (double)_numDuration.Value);
        _syncing = true;
        _numStart.Maximum = ClampToDecimal((decimal)maxStart, 0, 999999);
        if (_numStart.Value > _numStart.Maximum)
            _numStart.Value = _numStart.Maximum;
        _syncing = false;
        SyncTrackFromStart();
    }

    private void SyncTrackFromStart()
    {
        var maxStart = Math.Max(0.0, _totalSec - (double)_numDuration.Value);
        var ratio = maxStart <= 0.0001 ? 0.0 : (double)_numStart.Value / maxStart;
        var tv = (int)Math.Round(Math.Clamp(ratio, 0.0, 1.0) * _startTrack.Maximum);
        _syncing = true;
        _startTrack.Value = Math.Clamp(tv, _startTrack.Minimum, _startTrack.Maximum);
        _syncing = false;
    }

    private void SeekFromMouse(int x)
    {
        if (!_waveReady) return;
        if (_waveBox.Width <= 2 || _totalSec <= 0.01) return;
        var ratio = Math.Clamp(x / (double)_waveBox.Width, 0.0, 1.0);
        var maxStart = Math.Max(0.0, _totalSec - (double)_numDuration.Value);
        _syncing = true;
        _numStart.Value = ClampToDecimal((decimal)(ratio * maxStart), _numStart.Minimum, _numStart.Maximum);
        _syncing = false;
        SyncTrackFromStart();
        UpdateRangeLabel();
        _waveBox.Invalidate();
    }

    private void UpdateRangeLabel()
    {
        if (!_waveReady)
        {
            _lblRange.Text = T("dialog.rangeEditor.selectionEmpty");
            return;
        }
        var st = (double)_numStart.Value;
        var du = (double)_numDuration.Value;
        var ed = st + du;
        _lblRange.Text = T("dialog.rangeEditor.selection", FormatSec(st), FormatSec(ed), FormatSec(_totalSec));
    }
}



namespace HS2VoiceReplace;

// Builds the spreadsheet-like partial rebuild dialog UI and wires its visual structure separately from behavior.

internal sealed partial class PartialRebuildGridDialog
{
    public PartialRebuildGridDialog(
        string initialRunRoot,
        Action<string> onLog,
        Func<PartialRebuildGridRow, Task<string>> onRebuild,
        Func<Action<string>, Task<string>> onRunFull,
        Func<bool> canRunFull,
        Func<bool> onCancelFull,
        bool showCloseButton = true,
        bool useEnglish = false)
    {
        _onLog = onLog;
        _onRebuild = onRebuild;
        _onRunFull = onRunFull;
        _canRunFull = canRunFull;
        _onCancelFull = onCancelFull;
        _useEnglish = useEnglish;

        Text = T("dialog.partialGrid.title");
        _btnReload.Text = T("button.reload");
        _btnRunFull.Text = T("button.runAll");
        _btnStopFull.Text = T("button.stop");
        UiSizeHelper.FitButton(_btnReload, 100, 36);
        UiSizeHelper.FitButton(_btnRunFull, 120, 36);
        UiSizeHelper.FitButton(_btnStopFull, 90, 36);
        Width = 1780;
        Height = 980;
        MinimumSize = new Size(1320, 780);
        StartPosition = FormStartPosition.CenterParent;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(10) };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        var head = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = showCloseButton ? 5 : 4, AutoSize = true };
        head.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        head.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        head.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        head.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        if (showCloseButton)
            head.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        head.Controls.Add(_txtRunRoot, 0, 0);
        head.Controls.Add(_btnReload, 1, 0);
        head.Controls.Add(_btnRunFull, 2, 0);
        head.Controls.Add(_btnStopFull, 3, 0);
        Button? close = null;
        if (showCloseButton)
        {
            close = new Button { Text = T("button.close"), DialogResult = DialogResult.OK, Width = 110, Height = 36 };
            UiSizeHelper.FitButton(close, 90, 36);
            head.Controls.Add(close, 4, 0);
        }
        root.Controls.Add(head, 0, 0);

        SetupGrid();
        root.Controls.Add(_grid, 0, 1);
        root.Controls.Add(_pbFull, 0, 2);
        root.Controls.Add(_lblStatus, 0, 3);

        _txtRunRoot.Text = string.IsNullOrWhiteSpace(initialRunRoot) ? "" : Path.GetFullPath(initialRunRoot);
        _btnReload.Click += (_, _) => ReloadRows();
        _btnRunFull.Click += async (_, _) => await RunFullAsync();
        _btnStopFull.Click += (_, _) => RequestStopFullRun();
        _grid.CellContentClick += async (_, e) => await OnGridCellContentClickAsync(e);
        _grid.CellFormatting += (_, e) => OnGridCellFormatting(e);
        FormClosing += (_, _) => { try { _activePlayer?.Stop(); _activePlayer?.Dispose(); } catch { } _activePlayer = null; };
        _grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_grid.IsCurrentCellDirty)
                _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };

        if (close != null)
            AcceptButton = close;
        ReloadRows();
    }

    public void SetRunRoot(string runRoot, bool reload = true)
    {
        _txtRunRoot.Text = string.IsNullOrWhiteSpace(runRoot) ? "" : Path.GetFullPath(runRoot);
        if (reload)
            ReloadRows();
    }

    private void SetupGrid()
    {
        _grid.AutoGenerateColumns = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = false;
        _grid.ReadOnly = false;
        _grid.ScrollBars = ScrollBars.Both;
        _grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
        _grid.RowTemplate.Height = 24;
        _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        _grid.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
        _grid.MouseEnter += (_, _) => _grid.Focus();

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(PartialRebuildGridRow.RelativePath),
            HeaderText = T("dialog.partialGrid.column.relativePath"),
            ReadOnly = true,
            Width = 360,
        });

        var bucketCol = new DataGridViewComboBoxColumn
        {
            DataPropertyName = nameof(PartialRebuildGridRow.Bucket),
            HeaderText = T("dialog.partialGrid.column.bucket"),
            Width = 90,
            FlatStyle = FlatStyle.Flat
        };
        bucketCol.Items.AddRange("normal", "ero");
        _grid.Columns.Add(bucketCol);

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(PartialRebuildGridRow.SourceFile),
            HeaderText = T("dialog.partialGrid.column.sourceWav"),
            ReadOnly = true,
            Width = 500,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(PartialRebuildGridRow.ConvertedFile),
            HeaderText = T("dialog.partialGrid.column.convertedWav"),
            ReadOnly = true,
            Width = 500,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(PartialRebuildGridRow.SourceState),
            HeaderText = T("dialog.partialGrid.column.source"),
            ReadOnly = true,
            Width = 60
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(PartialRebuildGridRow.ConvertedState),
            HeaderText = T("dialog.partialGrid.column.output"),
            ReadOnly = true,
            Width = 60
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(PartialRebuildGridRow.Status),
            HeaderText = T("dialog.partialGrid.column.status"),
            ReadOnly = true,
            Width = 180
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(PartialRebuildGridRow.VoiceLine),
            HeaderText = T("dialog.partialGrid.column.line"),
            ReadOnly = true,
            Width = 420
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(PartialRebuildGridRow.SampleSignatureUsed),
            HeaderText = T("dialog.partialGrid.column.signatureUsed"),
            ReadOnly = true,
            Width = 260
        });
        _grid.Columns.Add(new DataGridViewButtonColumn
        {
            HeaderText = T("dialog.partialGrid.column.playSrc"),
            Text = T("button.playSrc"),
            UseColumnTextForButtonValue = true,
            Width = 110,
            Name = "btnPlaySrc"
        });
        _grid.Columns.Add(new DataGridViewButtonColumn
        {
            HeaderText = T("dialog.partialGrid.column.playDst"),
            Text = T("button.playDst"),
            UseColumnTextForButtonValue = true,
            Width = 130,
            Name = "btnPlayDst"
        });
        _grid.Columns.Add(new DataGridViewButtonColumn
        {
            HeaderText = T("dialog.partialGrid.column.rebuild"),
            Text = T("button.rebuildRow"),
            UseColumnTextForButtonValue = true,
            Width = 150,
            Name = "btnRebuildRow"
        });
        _grid.Columns.Add(new DataGridViewButtonColumn
        {
            HeaderText = T("dialog.partialGrid.column.discard"),
            Text = T("button.discardDst"),
            UseColumnTextForButtonValue = true,
            Width = 130,
            Name = "btnDiscardDst"
        });

        _grid.DataSource = _rows;
    }

    private void SetBusyControls()
    {
        _grid.Enabled = true;
        _btnReload.Enabled = !_busy;
        _btnRunFull.Enabled = !_busy && _canRunFull();
        _btnStopFull.Enabled = _busy && _fullRunExecuting;
    }

    public void RefreshCommandAvailability()
    {
        if (IsDisposed)
            return;
        if (InvokeRequired)
        {
            BeginInvoke(new Action(RefreshCommandAvailability));
            return;
        }
        SetBusyControls();
    }

    private void SetFullProgressMarquee()
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(SetFullProgressMarquee));
            return;
        }

        _pbFull.Style = ProgressBarStyle.Marquee;
        _pbFull.MarqueeAnimationSpeed = 25;
        _pbFull.Value = 0;
    }

    private void SetFullProgressCompleted()
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(SetFullProgressCompleted));
            return;
        }

        _pbFull.Style = ProgressBarStyle.Continuous;
        _pbFull.MarqueeAnimationSpeed = 0;
        _pbFull.Maximum = 100;
        _pbFull.Value = 100;
    }

    private void SetFullProgressError()
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(SetFullProgressError));
            return;
        }

        if (_pbFull.Style != ProgressBarStyle.Continuous)
        {
            _pbFull.Style = ProgressBarStyle.Continuous;
            _pbFull.MarqueeAnimationSpeed = 0;
            _pbFull.Maximum = 100;
            _pbFull.Value = 0;
        }
    }
}



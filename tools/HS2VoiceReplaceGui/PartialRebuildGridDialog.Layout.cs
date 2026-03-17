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
        Func<bool> isOwnerBusy,
        Func<bool> onCancelFull,
        bool showCloseButton = true,
        bool useEnglish = false)
    {
        _onLog = onLog;
        _onRebuild = onRebuild;
        _onRunFull = onRunFull;
        _canRunFull = canRunFull;
        _isOwnerBusy = isOwnerBusy;
        _onCancelFull = onCancelFull;
        _useEnglish = useEnglish;

        Text = T("dialog.partialGrid.title");
        _btnReload.Text = T("button.reload");
        _btnRunFull.Text = T("button.runAll");
        _btnStopFull.Text = T("button.stop");
        UiSizeHelper.FitButton(_btnReload, 100, 36);
        UiSizeHelper.FitButton(_btnRunFull, 120, 36);
        UiSizeHelper.FitButton(_btnStopFull, 90, 36);
        _btnReload.Margin = new Padding(0, 0, 8, 0);
        _btnRunFull.Margin = new Padding(0, 0, 8, 0);
        _btnStopFull.Margin = new Padding(0);
        Width = 1780;
        Height = 980;
        MinimumSize = new Size(1320, 780);
        StartPosition = FormStartPosition.CenterParent;

        UiSizeHelper.FitButton(_btnPlaySelectedSrc, 110, 36);
        UiSizeHelper.FitButton(_btnPlaySelectedDst, 130, 36);
        UiSizeHelper.FitButton(_btnRebuildSelected, 140, 36);
        UiSizeHelper.FitButton(_btnDiscardSelected, 130, 36);
        _btnPlaySelectedSrc.Margin = new Padding(8, 0, 0, 0);
        _btnPlaySelectedDst.Margin = new Padding(8, 0, 0, 0);
        _btnRebuildSelected.Margin = new Padding(8, 0, 0, 0);
        _btnDiscardSelected.Margin = new Padding(8, 0, 0, 0);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(10) };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        var head = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = showCloseButton ? 3 : 2, AutoSize = true, Margin = new Padding(0) };
        head.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        head.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        if (showCloseButton)
            head.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        head.Controls.Add(_txtRunRoot, 0, 0);
        var globalActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0),
            Padding = new Padding(0),
            FlowDirection = FlowDirection.RightToLeft,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        globalActions.Controls.Add(_btnStopFull);
        globalActions.Controls.Add(_btnRunFull);
        globalActions.Controls.Add(_btnReload);
        globalActions.Controls.Add(new Label
        {
            AutoSize = true,
            Text = T("dialog.partialGrid.group.global"),
            Margin = new Padding(0, 10, 8, 0),
        });
        head.Controls.Add(globalActions, 1, 0);
        Button? close = null;
        if (showCloseButton)
        {
            close = new Button { Text = T("button.close"), DialogResult = DialogResult.OK, Width = 110, Height = 36 };
            UiSizeHelper.FitButton(close, 90, 36);
            close.Margin = new Padding(8, 0, 0, 0);
            head.Controls.Add(close, 2, 0);
        }
        root.Controls.Add(head, 0, 0);

        var selectionRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Margin = new Padding(0, 6, 0, 6),
            Padding = new Padding(0),
            WrapContents = false,
            FlowDirection = FlowDirection.RightToLeft,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        selectionRow.Controls.Add(_btnPlaySelectedDst);
        selectionRow.Controls.Add(_btnPlaySelectedSrc);
        selectionRow.Controls.Add(_btnDiscardSelected);
        selectionRow.Controls.Add(_btnRebuildSelected);
        selectionRow.Controls.Add(new Label
        {
            AutoSize = true,
            Text = T("dialog.partialGrid.group.selection"),
            Margin = new Padding(0, 10, 8, 0),
        });
        root.Controls.Add(selectionRow, 0, 1);

        SetupGrid();
        _lblEmptyState.Text = T("dialog.partialGrid.empty.extractFirst");
        _gridPanel.Margin = new Padding(0);
        _gridPanel.Padding = new Padding(0);
        _gridPanel.Controls.Add(_grid);
        _gridPanel.Controls.Add(_lblEmptyState);
        root.Controls.Add(_gridPanel, 0, 2);
        root.Controls.Add(_pbFull, 0, 3);
        root.Controls.Add(_lblStatus, 0, 4);

        _txtRunRoot.Text = string.IsNullOrWhiteSpace(initialRunRoot) ? "" : Path.GetFullPath(initialRunRoot);
        _btnReload.Click += (_, _) => ReloadRows();
        _btnRunFull.Click += async (_, _) => await RunFullAsync();
        _btnStopFull.Click += (_, _) => RequestStopFullRun();
        _btnPlaySelectedSrc.Click += (_, _) => PlaySelectedSource();
        _btnPlaySelectedDst.Click += (_, _) => PlaySelectedConverted();
        _btnRebuildSelected.Click += async (_, _) => await RebuildSelectedRowsAsync();
        _btnDiscardSelected.Click += (_, _) => DiscardSelectedRows();
        _grid.CellFormatting += (_, e) => OnGridCellFormatting(e);
        _grid.SelectionChanged += (_, _) => RefreshSelectionActionAvailability();
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
        _grid.MultiSelect = true;
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
            DataPropertyName = nameof(PartialRebuildGridRow.SampleNameUsed),
            HeaderText = T("dialog.partialGrid.column.sampleUsed"),
            ReadOnly = true,
            Width = 180
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(PartialRebuildGridRow.SampleSignatureUsed),
            HeaderText = T("dialog.partialGrid.column.signatureUsed"),
            ReadOnly = true,
            Width = 260
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(PartialRebuildGridRow.SeedVcSummary),
            HeaderText = T("dialog.partialGrid.column.seedVcSummary"),
            ReadOnly = true,
            Width = 360
        });

        _grid.DataSource = _rows;
    }

    public Dictionary<string, int> GetColumnWidths()
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (DataGridViewColumn column in _grid.Columns)
        {
            var key = !string.IsNullOrWhiteSpace(column.DataPropertyName)
                ? column.DataPropertyName
                : column.Name;
            if (string.IsNullOrWhiteSpace(key) || column.Width <= 0)
                continue;
            map[key] = column.Width;
        }
        return map;
    }

    public void ApplyColumnWidths(IReadOnlyDictionary<string, int>? widths)
    {
        if (widths == null || widths.Count == 0)
            return;

        foreach (DataGridViewColumn column in _grid.Columns)
        {
            var key = !string.IsNullOrWhiteSpace(column.DataPropertyName)
                ? column.DataPropertyName
                : column.Name;
            if (string.IsNullOrWhiteSpace(key))
                continue;
            if (!widths.TryGetValue(key, out var width) || width <= 0)
                continue;
            column.Width = width;
        }
    }

    private void SetBusyControls()
    {
        var ownerBusy = _isOwnerBusy();
        _grid.Enabled = true;
        _grid.ReadOnly = ownerBusy || _busy;
        _btnReload.Enabled = !ownerBusy && !_busy;
        _btnRunFull.Enabled = !ownerBusy && !_busy && _canRunFull();
        _btnStopFull.Enabled = _busy && _fullRunExecuting;
        RefreshSelectionActionAvailability();
    }

    private void RefreshSelectionActionAvailability()
    {
        var selectedRows = GetSelectedRows();
        var singleSelected = selectedRows.Count == 1;
        var anySelected = selectedRows.Count > 0;
        var canPlaySrc = singleSelected && selectedRows[0].SourceExists;
        var canPlayDst = singleSelected && selectedRows[0].ConvertedExists;
        var canRebuild = anySelected && selectedRows.All(x => x.SourceExists);
        var canDiscard = selectedRows.Any(x => x.ConvertedExists);
        var canMutate = !_isOwnerBusy() && !_busy;

        _btnPlaySelectedSrc.Enabled = canPlaySrc;
        _btnPlaySelectedDst.Enabled = canPlayDst;
        _btnRebuildSelected.Enabled = canMutate && canRebuild;
        _btnDiscardSelected.Enabled = canMutate && canDiscard;
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



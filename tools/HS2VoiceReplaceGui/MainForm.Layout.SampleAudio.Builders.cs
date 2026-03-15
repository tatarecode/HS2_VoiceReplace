namespace HS2VoiceReplace;

public sealed partial class MainForm
{
    // Building the sample-audio dialog in dedicated helpers keeps the main method focused
    // on state wiring and event flow rather than raw control construction.
    private Form CreateSampleAudioDialogShell(out TableLayoutPanel root)
    {
        var dlg = new Form
        {
            Text = T("dialog.sampleAudio.title"),
            Width = 1320,
            Height = 900,
            MinimumSize = new Size(1060, 680),
            StartPosition = FormStartPosition.CenterParent,
        };

        root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(10),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        dlg.Controls.Add(root);
        return dlg;
    }

    private TableLayoutPanel CreateSampleAudioAssignPanel(out ComboBox cmbNormal, out ComboBox cmbEro)
    {
        var assign = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 4,
        };
        assign.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        assign.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        assign.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        assign.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        cmbNormal = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 420 };
        cmbEro = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 420 };
        assign.Controls.Add(new Label { AutoSize = true, Text = T("dialog.sampleAudio.normalAssignment"), Anchor = AnchorStyles.Left }, 0, 0);
        assign.Controls.Add(cmbNormal, 1, 0);
        assign.Controls.Add(new Label { AutoSize = true, Text = T("dialog.sampleAudio.eroAssignment"), Anchor = AnchorStyles.Left }, 2, 0);
        assign.Controls.Add(cmbEro, 3, 0);
        _lblSampleSignatureInDialog = new Label { AutoSize = true, Text = _lblSampleSignature.Text, Margin = new Padding(0, 6, 0, 0) };
        assign.Controls.Add(new Label { AutoSize = true, Text = T("dialog.sampleAudio.conversionSampleSignature"), Margin = new Padding(0, 6, 6, 0) }, 0, 1);
        assign.Controls.Add(_lblSampleSignatureInDialog, 1, 1);
        assign.SetColumnSpan(_lblSampleSignatureInDialog, 3);
        return assign;
    }

    private FlowLayoutPanel CreateSampleAudioActionsPanel(
        out Button btnAdd,
        out Button btnRename,
        out Button btnTrash,
        out Button btnRestore,
        out Button btnDeleteHard,
        out Button btnPlayAsset,
        out CheckBox chkShowTrash)
    {
        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
        btnAdd = new Button { Text = T("button.addRangeExtract"), Width = 180, Height = 36 };
        btnRename = new Button { Text = T("button.rename"), Width = 120, Height = 36 };
        btnTrash = new Button { Text = T("button.deleteToTrash"), Width = 140, Height = 36 };
        btnRestore = new Button { Text = T("button.restore"), Width = 100, Height = 36 };
        btnDeleteHard = new Button { Text = T("button.deletePermanent"), Width = 150, Height = 36 };
        btnPlayAsset = new Button { Text = T("button.playSample"), Width = 130, Height = 36 };
        UiSizeHelper.FitButton(btnAdd, 170, 36);
        UiSizeHelper.FitButton(btnRename, 100, 36);
        UiSizeHelper.FitButton(btnTrash, 130, 36);
        UiSizeHelper.FitButton(btnRestore, 90, 36);
        UiSizeHelper.FitButton(btnDeleteHard, 160, 36);
        UiSizeHelper.FitButton(btnPlayAsset, 120, 36);
        chkShowTrash = new CheckBox { Text = T("checkbox.showTrash"), AutoSize = true, Margin = new Padding(18, 9, 6, 0) };
        actions.Controls.Add(btnAdd);
        actions.Controls.Add(btnRename);
        actions.Controls.Add(btnTrash);
        actions.Controls.Add(btnRestore);
        actions.Controls.Add(btnDeleteHard);
        actions.Controls.Add(btnPlayAsset);
        actions.Controls.Add(chkShowTrash);
        return actions;
    }

    private DataGridView CreateSampleAudioGrid()
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoGenerateColumns = false,
            RowHeadersVisible = false,
        };
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(SampleAssetGridRow.Name), HeaderText = T("dialog.sampleAudio.column.name"), Width = 170 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(SampleAssetGridRow.State), HeaderText = T("dialog.sampleAudio.column.state"), Width = 90 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(SampleAssetGridRow.Signature), HeaderText = T("dialog.sampleAudio.column.signature"), Width = 230 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(SampleAssetGridRow.HashAlgorithmVersion), HeaderText = T("dialog.sampleAudio.column.hashVer"), Width = 120 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(SampleAssetGridRow.LengthSec), HeaderText = T("dialog.sampleAudio.column.length"), Width = 80 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(SampleAssetGridRow.SampleRateHz), HeaderText = T("dialog.sampleAudio.column.hz"), Width = 70 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(SampleAssetGridRow.Channels), HeaderText = T("dialog.sampleAudio.column.ch"), Width = 55 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(SampleAssetGridRow.Source), HeaderText = T("dialog.sampleAudio.column.source"), Width = 260 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(SampleAssetGridRow.Range), HeaderText = T("dialog.sampleAudio.column.range"), Width = 150 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(SampleAssetGridRow.ExtractedAt), HeaderText = T("dialog.sampleAudio.column.extractedAt"), Width = 160 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(SampleAssetGridRow.LastUsedAt), HeaderText = T("dialog.sampleAudio.column.lastUsedAt"), Width = 160 });
        return grid;
    }

    private TableLayoutPanel CreateSampleAudioBottomPanel(Form dlg, out Button btnClose)
    {
        var bottom = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 3,
        };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        var previewPanel = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 3 };
        previewPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        previewPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        previewPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        previewPanel.Controls.Add(new Label { AutoSize = true, Text = T("dialog.sampleAudio.previewNormal"), Anchor = AnchorStyles.Left }, 0, 0);
        previewPanel.Controls.Add(_txtPreviewNormal, 1, 0);
        previewPanel.Controls.Add(_btnPlayPreviewNormal, 2, 0);
        previewPanel.Controls.Add(new Label { AutoSize = true, Text = T("dialog.sampleAudio.previewEro"), Anchor = AnchorStyles.Left }, 0, 1);
        previewPanel.Controls.Add(_txtPreviewEro, 1, 1);
        previewPanel.Controls.Add(_btnPlayPreviewEro, 2, 1);
        var previewAction = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        previewAction.Controls.Add(_btnPreview);
        _btnSampleDialogCancel = new Button { Text = T("button.stop"), Width = 120, Height = 36, Enabled = _isBusy };
        UiSizeHelper.FitButton(_btnSampleDialogCancel, 90, 36);
        _btnSampleDialogCancel.Click += (_, _) => RequestCancelCurrentOperation();
        previewAction.Controls.Add(_btnSampleDialogCancel);
        previewAction.Controls.Add(new Label
        {
            AutoSize = true,
            Text = T("dialog.sampleAudio.previewIsolated"),
            Margin = new Padding(10, 10, 0, 0),
        });
        previewPanel.Controls.Add(previewAction, 1, 2);
        previewPanel.SetColumnSpan(previewAction, 2);
        bottom.Controls.Add(previewPanel, 0, 0);
        btnClose = new Button { Text = T("button.close"), Width = 120, Height = 36 };
        UiSizeHelper.FitButton(btnClose, 100, 36);
        btnClose.Click += (_, _) =>
        {
            SaveSampleAssetsCatalog();
            SaveUiSettings();
            dlg.Hide();
        };
        bottom.Controls.Add(btnClose, 2, 0);
        return bottom;
    }
}

